// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1649 // File name should match first type name

using System;
using System.Collections.Generic;

namespace Nerdbank.Json;

internal sealed class JsonObjectConverter<T> : JsonConverter<T>
{
	private readonly Func<T> factory;
	private readonly JsonProperty<T>[] properties;
	private readonly Dictionary<string, JsonProperty<T>> propertiesByName;

	internal JsonObjectConverter(Func<T> factory, JsonProperty<T>[] properties, StringComparer propertyNameComparer)
	{
		this.factory = factory;
		this.properties = properties;
		this.propertiesByName = new Dictionary<string, JsonProperty<T>>(properties.Length, propertyNameComparer);
		for (int i = 0; i < properties.Length; i++)
		{
			this.propertiesByName[properties[i].Name] = properties[i];
		}
	}

	public override void Write(ref JsonWriter writer, T? value, JsonSerializer serializer)
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
			JsonProperty<T> property = this.properties[i];
			if (!property.CanSerialize)
			{
				continue;
			}

			if (property.Write(ref writer, value, serializer, first))
			{
				first = false;
			}
		}

		writer.WriteEndObject();
	}

	public override T? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (!typeof(T).IsValueType && reader.TryReadNull())
		{
			return default;
		}

		T result = this.factory();
		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			return result;
		}

		while (true)
		{
			string propertyName = reader.ReadRequiredString();
			reader.ReadNameSeparator();

			if (this.propertiesByName.TryGetValue(propertyName, out JsonProperty<T>? property) && property.CanDeserialize)
			{
				property.Read(ref reader, ref result, serializer);
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

		return result;
	}
}

internal abstract class JsonProperty<TDeclaring>
{
	internal JsonProperty(string name) => this.Name = name;

	internal string Name { get; }

	internal abstract bool CanSerialize { get; }

	internal abstract bool CanDeserialize { get; }

	internal abstract bool Write(ref JsonWriter writer, TDeclaring container, JsonSerializer serializer, bool first);

	internal abstract void Read(ref JsonReader reader, ref TDeclaring container, JsonSerializer serializer);
}

internal sealed class JsonProperty<TDeclaring, TProperty> : JsonProperty<TDeclaring>
{
	private readonly string memberName;
	private readonly Getter<TDeclaring, TProperty>? getter;
	private readonly Setter<TDeclaring, TProperty>? setter;
	private readonly JsonConverter<TProperty> converter;
	private readonly bool deserializeIntoExistingInstance;
	private readonly bool isRequired;
	private readonly bool isNonNullableReferenceType;

	internal JsonProperty(string name, string memberName, Getter<TDeclaring, TProperty>? getter, Setter<TDeclaring, TProperty>? setter, JsonConverter<TProperty> converter, bool deserializeIntoExistingInstance = false, bool isRequired = false, bool isNonNullableReferenceType = false)
		: base(name)
	{
		this.memberName = memberName;
		this.getter = getter;
		this.setter = setter;
		this.converter = converter;
		this.deserializeIntoExistingInstance = deserializeIntoExistingInstance;
		this.isRequired = isRequired;
		this.isNonNullableReferenceType = isNonNullableReferenceType;
	}

	internal override bool CanSerialize => this.getter is not null;

	internal override bool CanDeserialize => this.setter is not null || this.deserializeIntoExistingInstance;

	internal override bool Write(ref JsonWriter writer, TDeclaring container, JsonSerializer serializer, bool first)
	{
		if (this.getter is null)
		{
			throw new InvalidOperationException("Property has no getter.");
		}

		TProperty? value = this.getter(ref container);
		if (!this.ShouldSerializeValue(value, serializer.SerializeDefaultValues))
		{
			return false;
		}

		if (!first)
		{
			writer.WriteValueSeparator();
		}

		writer.WritePropertyName(this.Name);
		this.converter.Write(ref writer, value, serializer);
		return true;
	}

	internal override void Read(ref JsonReader reader, ref TDeclaring container, JsonSerializer serializer)
	{
		if (this.setter is not null)
		{
			TProperty? value = this.converter.Read(ref reader, serializer);
			if (this.isNonNullableReferenceType
				&& value is null
				&& !typeof(TProperty).IsValueType
				&& (serializer.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties) != DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties)
			{
				throw new FormatException($"Property '{this.memberName}' does not allow null values.");
			}

			this.setter(ref container, value!);
			return;
		}

		if (this.deserializeIntoExistingInstance && this.getter is not null && this.converter is IJsonDeserializeInto<TProperty> deserializeInto)
		{
			if (!typeof(TProperty).IsValueType && reader.TryReadNull())
			{
				return;
			}

			TProperty collection = this.getter(ref container);
			deserializeInto.DeserializeInto(ref reader, ref collection, serializer);
			return;
		}

		if (this.setter is null)
		{
			reader.SkipValue();
			return;
		}
	}

	private bool ShouldSerializeValue(TProperty? value, SerializeDefaultValuesPolicy policy)
	{
		if (policy == SerializeDefaultValuesPolicy.Always)
		{
			return true;
		}

		if (!EqualityComparer<TProperty>.Default.Equals(value!, default!))
		{
			return true;
		}

		if (this.isRequired && (policy & SerializeDefaultValuesPolicy.Required) == SerializeDefaultValuesPolicy.Required)
		{
			return true;
		}

		if (value is null)
		{
			return (policy & SerializeDefaultValuesPolicy.ReferenceTypes) == SerializeDefaultValuesPolicy.ReferenceTypes;
		}

		if (typeof(TProperty).IsValueType)
		{
			return (policy & SerializeDefaultValuesPolicy.ValueTypes) == SerializeDefaultValuesPolicy.ValueTypes;
		}

		return (policy & SerializeDefaultValuesPolicy.ReferenceTypes) == SerializeDefaultValuesPolicy.ReferenceTypes;
	}
}
