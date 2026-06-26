// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1649 // File name should not need to match the first type in this multi-type helper file.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Nerdbank.Json;

internal readonly struct JsonUnionCaseMetadata<TUnion>
{
	private readonly string? stringAlias;
	private readonly int intAlias;
	private readonly bool usesIntegerAlias;

	private JsonUnionCaseMetadata(string alias, JsonConverter converter)
	{
		this.stringAlias = alias;
		this.intAlias = default;
		this.usesIntegerAlias = false;
		this.Converter = converter;
	}

	private JsonUnionCaseMetadata(int alias, JsonConverter converter)
	{
		this.stringAlias = null;
		this.intAlias = alias;
		this.usesIntegerAlias = true;
		this.Converter = converter;
	}

	internal JsonConverter Converter { get; }

	internal static JsonUnionCaseMetadata<TUnion> Create(string alias, JsonConverter converter) => new(alias, converter);

	internal static JsonUnionCaseMetadata<TUnion> Create(int alias, JsonConverter converter) => new(alias, converter);

	internal void WriteAlias(ref JsonWriter writer)
	{
		if (this.usesIntegerAlias)
		{
			writer.WriteNumberValue(this.intAlias);
		}
		else
		{
			writer.WriteStringValue(this.stringAlias);
		}
	}
}

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

	public override void Write(ref JsonWriter writer, TOptional? value, JsonSerializer serializer)
	{
		if (!this.deconstructor(value, out TElement? element))
		{
			writer.WriteNullValue();
			return;
		}

		this.elementConverter.Write(ref writer, element, serializer);
	}

	public override TOptional? Read(ref JsonReader reader, JsonSerializer serializer)
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
	where TUnderlying : struct
{
	private readonly Dictionary<string, TUnderlying>? valuesByName;
	private readonly Dictionary<TUnderlying, string>? namesByValue;
	private readonly JsonConverter<TUnderlying> underlyingConverter;

	internal JsonEnumConverter(JsonConverter<TUnderlying> underlyingConverter, IReadOnlyDictionary<string, TUnderlying> members, bool serializeByName)
	{
		this.underlyingConverter = underlyingConverter;
		if (serializeByName)
		{
			(this.valuesByName, this.namesByValue) = CreateNameMaps(members);
		}
	}

	public override void Write(ref JsonWriter writer, TEnum value, JsonSerializer serializer)
	{
		TUnderlying underlyingValue = (TUnderlying)(object)value;
		if (this.namesByValue?.TryGetValue(underlyingValue, out string? name) == true)
		{
			writer.WriteStringValue(name);
			return;
		}

		this.underlyingConverter.Write(ref writer, underlyingValue, serializer);
	}

	public override TEnum Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (reader.PeekValueToken() == '"')
		{
			string name = reader.ReadRequiredString();
			if (this.valuesByName?.TryGetValue(name, out TUnderlying value) == true)
			{
				return (TEnum)(object)value;
			}

			throw new FormatException($"Unrecognized enum value name '{name}'.");
		}

		return (TEnum)(object)this.underlyingConverter.Read(ref reader, serializer)!;
	}

	private static (Dictionary<string, TUnderlying> ValuesByName, Dictionary<TUnderlying, string> NamesByValue) CreateNameMaps(IReadOnlyDictionary<string, TUnderlying> members)
	{
		Dictionary<string, TUnderlying> valuesByName = new(StringComparer.OrdinalIgnoreCase);
		Dictionary<TUnderlying, string> namesByValue = new();

		if (!TryPopulate(valuesByName, namesByValue))
		{
			valuesByName = new(StringComparer.Ordinal);
			namesByValue = new();
			if (!TryPopulate(valuesByName, namesByValue))
			{
				throw new InvalidOperationException($"Failed to build enum name map for {typeof(TEnum).FullName}.");
			}
		}

		return (valuesByName, namesByValue);

		bool TryPopulate(Dictionary<string, TUnderlying> nameMap, Dictionary<TUnderlying, string> reverseMap)
		{
			foreach (KeyValuePair<string, TUnderlying> pair in members)
			{
				if (nameMap.ContainsKey(pair.Key))
				{
					if (!EqualityComparer<TUnderlying>.Default.Equals(nameMap[pair.Key], pair.Value))
					{
						return false;
					}
				}
				else
				{
					nameMap.Add(pair.Key, pair.Value);
				}

				if (!reverseMap.ContainsKey(pair.Value))
				{
					reverseMap.Add(pair.Value, pair.Key);
				}
			}

			return true;
		}
	}
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

	public override void Write(ref JsonWriter writer, T? value, JsonSerializer serializer)
		=> this.surrogateConverter.Write(ref writer, this.shape.Marshaler.Marshal(value), serializer);

	public override T? Read(ref JsonReader reader, JsonSerializer serializer)
		=> this.shape.Marshaler.Unmarshal(this.surrogateConverter.Read(ref reader, serializer));
}

internal sealed class JsonUnionConverter<TUnion> : JsonConverter<TUnion>
{
	private readonly JsonConverter<TUnion> baseConverter;
	private readonly Getter<TUnion, int> getUnionCaseIndex;
	private readonly JsonUnionCaseMetadata<TUnion>[] serializers;
	private readonly IReadOnlyDictionary<int, JsonConverter> deserializersByIntAlias;
	private readonly IReadOnlyDictionary<string, JsonConverter> deserializersByStringAlias;

	internal JsonUnionConverter(JsonConverter<TUnion> baseConverter, Getter<TUnion, int> getUnionCaseIndex, JsonUnionCaseMetadata<TUnion>[] serializers, IReadOnlyDictionary<int, JsonConverter> deserializersByIntAlias, IReadOnlyDictionary<string, JsonConverter> deserializersByStringAlias)
	{
		this.baseConverter = baseConverter;
		this.getUnionCaseIndex = getUnionCaseIndex;
		this.serializers = serializers;
		this.deserializersByIntAlias = deserializersByIntAlias;
		this.deserializersByStringAlias = deserializersByStringAlias;
	}

	public override void Write(ref JsonWriter writer, TUnion? value, JsonSerializer serializer)
	{
		if (!typeof(TUnion).IsValueType && value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStartArray();
		JsonConverter converter = this.baseConverter;
		if (value is not null && this.TryGetSerializer(value, out JsonUnionCaseMetadata<TUnion> unionCase))
		{
			unionCase.WriteAlias(ref writer);
			converter = unionCase.Converter;
		}
		else
		{
			writer.WriteNullValue();
		}

		writer.WriteValueSeparator();
		converter.WriteObject(ref writer, value, serializer);
		writer.WriteEndArray();
	}

	public override TUnion? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (!typeof(TUnion).IsValueType && reader.TryReadNull())
		{
			return default;
		}

		reader.ReadStartArray();
		JsonConverter converter;
		if (reader.TryReadNull())
		{
			converter = this.baseConverter;
		}
		else
		{
			converter = reader.PeekValueToken() == '"'
				? this.ResolveStringAlias(reader.ReadRequiredString())
				: this.ResolveIntegerAlias(int.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture));
		}

		reader.ReadValueSeparator();
		TUnion? value = (TUnion?)converter.ReadObject(ref reader, serializer);
		reader.ReadEndArray();
		return value;
	}

	private bool TryGetSerializer(TUnion value, out JsonUnionCaseMetadata<TUnion> unionCase)
	{
		int index = this.getUnionCaseIndex(ref value);
		if (index >= 0 && index < this.serializers.Length)
		{
			unionCase = this.serializers[index];
			return true;
		}

		unionCase = default;
		return false;
	}

	private JsonConverter ResolveIntegerAlias(int alias)
	{
		if (!this.deserializersByIntAlias.TryGetValue(alias, out JsonConverter? converter))
		{
			throw new FormatException($"Unrecognized union discriminator '{alias}'.");
		}

		return converter;
	}

	private JsonConverter ResolveStringAlias(string alias)
	{
		if (!this.deserializersByStringAlias.TryGetValue(alias, out JsonConverter? converter))
		{
			throw new FormatException($"Unrecognized union discriminator '{alias}'.");
		}

		return converter;
	}
}

internal sealed class JsonUnionCaseConverter<TUnionCase, TUnion> : JsonConverter<TUnion>
{
	private readonly JsonConverter<TUnionCase> inner;
	private readonly IMarshaler<TUnionCase, TUnion> marshaler;

	internal JsonUnionCaseConverter(JsonConverter<TUnionCase> inner, IMarshaler<TUnionCase, TUnion> marshaler)
	{
		this.inner = inner;
		this.marshaler = marshaler;
	}

	public override void Write(ref JsonWriter writer, TUnion? value, JsonSerializer serializer)
		=> this.inner.Write(ref writer, this.marshaler.Unmarshal(value), serializer);

	public override TUnion? Read(ref JsonReader reader, JsonSerializer serializer)
		=> this.marshaler.Marshal(this.inner.Read(ref reader, serializer));
}

internal sealed class JsonConstructorVisitorState<TDeclaring>
{
	private readonly Dictionary<string, string> serializedPropertyNamesByClrName;

	internal JsonConstructorVisitorState(JsonProperty<TDeclaring>[] properties, Dictionary<string, string> serializedPropertyNamesByClrName, JsonExtensionData<TDeclaring>? extensionData = null)
	{
		this.Properties = properties;
		this.serializedPropertyNamesByClrName = serializedPropertyNamesByClrName;
		this.ExtensionData = extensionData;
	}

	internal JsonProperty<TDeclaring>[] Properties { get; }

	internal JsonExtensionData<TDeclaring>? ExtensionData { get; }

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
	internal JsonConstructorParameter(string parameterName, string serializedPropertyName, bool isRequired)
	{
		this.ParameterName = parameterName;
		this.SerializedPropertyName = serializedPropertyName;
		this.IsRequired = isRequired;
	}

	internal string ParameterName { get; }

	internal string SerializedPropertyName { get; }

	internal bool IsRequired { get; }

	internal abstract void Read(ref JsonReader reader, ref TArgumentState argumentState, JsonSerializer serializer);
}

internal sealed class JsonConstructorParameter<TArgumentState, TParameter> : JsonConstructorParameter<TArgumentState>
{
	private readonly Setter<TArgumentState, TParameter> setter;
	private readonly JsonConverter<TParameter> converter;
	private readonly bool isNonNullableReferenceType;

	internal JsonConstructorParameter(string parameterName, string serializedPropertyName, bool isRequired, Setter<TArgumentState, TParameter> setter, JsonConverter<TParameter> converter, bool isNonNullableReferenceType)
		: base(parameterName, serializedPropertyName, isRequired)
	{
		this.setter = setter;
		this.converter = converter;
		this.isNonNullableReferenceType = isNonNullableReferenceType;
	}

	internal override void Read(ref JsonReader reader, ref TArgumentState argumentState, JsonSerializer serializer)
	{
		TParameter? value = this.converter.Read(ref reader, serializer);
		if (this.isNonNullableReferenceType
			&& value is null
			&& (serializer.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties) != DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties)
		{
			throw new FormatException($"Constructor parameter '{this.ParameterName}' does not allow null values.");
		}

		this.setter(ref argumentState, value!);
	}
}

internal sealed class JsonObjectWithConstructorConverter<TDeclaring, TArgumentState> : JsonConverter<TDeclaring>
{
	private readonly JsonExtensionData<TDeclaring>? extensionData;
	private readonly JsonProperty<TDeclaring>[] properties;
	private readonly Func<TArgumentState> argumentStateFactory;
	private readonly Constructor<TArgumentState, TDeclaring> constructor;
	private readonly JsonConstructorParameter<TArgumentState>[] parameters;
	private readonly Dictionary<string, JsonConstructorParameter<TArgumentState>> parametersByName;

	internal JsonObjectWithConstructorConverter(JsonProperty<TDeclaring>[] properties, Func<TArgumentState> argumentStateFactory, Constructor<TArgumentState, TDeclaring> constructor, JsonConstructorParameter<TArgumentState>[] parameters, Dictionary<string, JsonConstructorParameter<TArgumentState>> parametersByName, JsonExtensionData<TDeclaring>? extensionData = null)
	{
		this.extensionData = extensionData;
		this.properties = properties;
		this.argumentStateFactory = argumentStateFactory;
		this.constructor = constructor;
		this.parameters = parameters;
		this.parametersByName = parametersByName;
	}

	public override void Write(ref JsonWriter writer, TDeclaring? value, JsonSerializer serializer)
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

			if (property.Write(ref writer, value, serializer, first))
			{
				first = false;
			}
		}

		if (this.extensionData is not null)
		{
			this.extensionData.Write(ref writer, value, ref first);
		}

		writer.WriteEndObject();
	}

	public override TDeclaring? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (!typeof(TDeclaring).IsValueType && reader.TryReadNull())
		{
			return default;
		}

		TArgumentState argumentState = this.argumentStateFactory();
		HashSet<string> assignedParameters = new(StringComparer.Ordinal);
		Dictionary<string, string>? extensionData = null;
		reader.ReadStartObject();
		if (!reader.TryReadEndObject())
		{
			while (true)
			{
				string propertyName = reader.ReadRequiredString();
				reader.ReadNameSeparator();

				if (this.parametersByName.TryGetValue(propertyName, out JsonConstructorParameter<TArgumentState>? parameter))
				{
					if (!assignedParameters.Add(parameter.SerializedPropertyName))
					{
						throw new FormatException($"The constructor parameter '{parameter.ParameterName}' has already been assigned a value.");
					}

					parameter.Read(ref reader, ref argumentState, serializer);
				}
				else if (this.extensionData is not null)
				{
					(extensionData ??= new Dictionary<string, string>(StringComparer.Ordinal))[propertyName] = reader.ReadRawValue();
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

		if (assignedParameters.Count < this.parameters.Length
			&& (serializer.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties) != DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties)
		{
			List<string>? missingRequiredParameters = null;
			for (int i = 0; i < this.parameters.Length; i++)
			{
				JsonConstructorParameter<TArgumentState> parameter = this.parameters[i];
				if (parameter.IsRequired && !assignedParameters.Contains(parameter.SerializedPropertyName))
				{
					missingRequiredParameters ??= new List<string>();
					missingRequiredParameters.Add(parameter.ParameterName);
				}
			}

			if (missingRequiredParameters is not null)
			{
				throw new FormatException($"Missing required constructor parameters: {string.Join(", ", missingRequiredParameters)}.");
			}
		}

		TDeclaring result = this.constructor(ref argumentState);
		if (this.extensionData is not null && extensionData is not null)
		{
			this.extensionData.Apply(result, extensionData);
		}

		return result;
	}
}
