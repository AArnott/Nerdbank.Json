// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1649 // File name should not need to match the first type in this multi-type helper file.

using System;
using System.Collections.Generic;

namespace Nerdbank.Json;

internal sealed class JsonOptionalConverter<TOptional, TElement> : JsonConverter<TOptional>
{
	private readonly JsonConverter<TElement> elementConverter;
	private readonly OptionDeconstructor<TOptional, TElement> deconstructor;
	private readonly Func<TOptional> createNone;
	private readonly Func<TElement, TOptional> createSome;

	internal JsonOptionalConverter(JsonConverter<TElement> elementConverter, OptionDeconstructor<TOptional, TElement> deconstructor, Func<TOptional> createNone, Func<TElement, TOptional> createSome)
	{
		this.elementConverter = elementConverter;
		this.deconstructor = deconstructor;
		this.createNone = createNone;
		this.createSome = createSome;
	}

	internal override void Write(ref JsonWriter writer, TOptional? value, JsonSerializer serializer)
	{
		if (!this.deconstructor(value, out TElement? element))
		{
			writer.WriteNullValue();
			return;
		}

		this.elementConverter.Write(ref writer, element, serializer);
	}

	internal override TOptional? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (reader.TryReadNull())
		{
			return this.createNone();
		}

		return this.createSome(this.elementConverter.Read(ref reader, serializer)!);
	}
}

internal sealed class JsonEnumConverter<TEnum, TUnderlying> : JsonConverter<TEnum>
	where TEnum : struct
{
	private readonly JsonConverter<TUnderlying> underlyingConverter;

	internal JsonEnumConverter(JsonConverter<TUnderlying> underlyingConverter)
	{
		this.underlyingConverter = underlyingConverter;
	}

	internal override void Write(ref JsonWriter writer, TEnum value, JsonSerializer serializer)
		=> this.underlyingConverter.Write(ref writer, (TUnderlying)(object)value, serializer);

	internal override TEnum Read(ref JsonReader reader, JsonSerializer serializer)
		=> (TEnum)(object)this.underlyingConverter.Read(ref reader, serializer)!;
}

internal sealed class JsonSurrogateConverter<T, TSurrogate> : JsonConverter<T>
{
	private readonly ISurrogateTypeShape<T, TSurrogate> shape;
	private readonly JsonConverter<TSurrogate> surrogateConverter;

	internal JsonSurrogateConverter(ISurrogateTypeShape<T, TSurrogate> shape, JsonConverter<TSurrogate> surrogateConverter)
	{
		this.shape = shape;
		this.surrogateConverter = surrogateConverter;
	}

	internal override void Write(ref JsonWriter writer, T? value, JsonSerializer serializer)
		=> this.surrogateConverter.Write(ref writer, this.shape.Marshaler.Marshal(value), serializer);

	internal override T? Read(ref JsonReader reader, JsonSerializer serializer)
		=> this.shape.Marshaler.Unmarshal(this.surrogateConverter.Read(ref reader, serializer));
}

internal sealed class JsonConstructorVisitorState<TDeclaring>
{
	private readonly Dictionary<string, string> serializedPropertyNamesByClrName;

	internal JsonConstructorVisitorState(JsonProperty<TDeclaring>[] properties, Dictionary<string, string> serializedPropertyNamesByClrName)
	{
		this.Properties = properties;
		this.serializedPropertyNamesByClrName = serializedPropertyNamesByClrName;
	}

	internal JsonProperty<TDeclaring>[] Properties { get; }

	internal bool TryGetSerializedPropertyName(string clrName, out string serializedPropertyName)
		=> this.serializedPropertyNamesByClrName.TryGetValue(clrName, out serializedPropertyName!);
}

internal sealed class JsonParameterVisitorState
{
	internal JsonParameterVisitorState(string serializedPropertyName)
	{
		this.SerializedPropertyName = serializedPropertyName;
	}

	internal string SerializedPropertyName { get; }
}

internal abstract class JsonConstructorParameter<TArgumentState>
{
	internal JsonConstructorParameter(string serializedPropertyName)
	{
		this.SerializedPropertyName = serializedPropertyName;
	}

	internal string SerializedPropertyName { get; }

	internal abstract void Read(ref JsonReader reader, ref TArgumentState argumentState, JsonSerializer serializer);
}

internal sealed class JsonConstructorParameter<TArgumentState, TParameter> : JsonConstructorParameter<TArgumentState>
{
	private readonly Setter<TArgumentState, TParameter> setter;
	private readonly JsonConverter<TParameter> converter;

	internal JsonConstructorParameter(string serializedPropertyName, Setter<TArgumentState, TParameter> setter, JsonConverter<TParameter> converter)
		: base(serializedPropertyName)
	{
		this.setter = setter;
		this.converter = converter;
	}

	internal override void Read(ref JsonReader reader, ref TArgumentState argumentState, JsonSerializer serializer)
		=> this.setter(ref argumentState, this.converter.Read(ref reader, serializer)!);
}

internal sealed class JsonObjectWithConstructorConverter<TDeclaring, TArgumentState> : JsonConverter<TDeclaring>
{
	private readonly JsonProperty<TDeclaring>[] properties;
	private readonly Func<TArgumentState> argumentStateFactory;
	private readonly Constructor<TArgumentState, TDeclaring> constructor;
	private readonly Dictionary<string, JsonConstructorParameter<TArgumentState>> parametersByName;

	internal JsonObjectWithConstructorConverter(JsonProperty<TDeclaring>[] properties, Func<TArgumentState> argumentStateFactory, Constructor<TArgumentState, TDeclaring> constructor, Dictionary<string, JsonConstructorParameter<TArgumentState>> parametersByName)
	{
		this.properties = properties;
		this.argumentStateFactory = argumentStateFactory;
		this.constructor = constructor;
		this.parametersByName = parametersByName;
	}

	internal override void Write(ref JsonWriter writer, TDeclaring? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStartObject();
		bool first = true;
		for (int i = 0; i < this.properties.Length; i++)
		{
			JsonProperty<TDeclaring> property = this.properties[i];
			if (!property.CanSerialize)
			{
				continue;
			}

			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			property.Write(ref writer, value, serializer);
		}

		writer.WriteEndObject();
	}

	internal override TDeclaring? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (!typeof(TDeclaring).IsValueType && reader.TryReadNull())
		{
			return default;
		}

		TArgumentState argumentState = this.argumentStateFactory();
		reader.ReadStartObject();
		if (!reader.TryReadEndObject())
		{
			while (true)
			{
				string propertyName = reader.ReadRequiredString();
				reader.ReadNameSeparator();

				if (this.parametersByName.TryGetValue(propertyName, out JsonConstructorParameter<TArgumentState>? parameter))
				{
					parameter.Read(ref reader, ref argumentState, serializer);
				}
				else
				{
					reader.SkipValue();
				}

				if (reader.TryReadEndObject())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		return this.constructor(ref argumentState);
	}
}
