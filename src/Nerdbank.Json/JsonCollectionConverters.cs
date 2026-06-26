// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable IL2087 // Reflection-based collection construction is intentional for mutable collection fallback.

using System;
using System.Collections.Generic;

namespace Nerdbank.Json;

internal sealed class JsonListConverter<TCollection, TElement> : JsonConverter<TCollection>
	where TCollection : IEnumerable<TElement>
{
	private readonly JsonConverterCache owner;

	public JsonListConverter(JsonConverterCache owner)
	{
		this.owner = owner;
	}

	internal override void Write(ref JsonWriter writer, TCollection? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		JsonConverter<TElement> elementConverter = this.owner.GetOrAddConverter<TElement>();
		writer.WriteStartArray();
		bool first = true;
		foreach (TElement element in value)
		{
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			elementConverter.Write(ref writer, element, serializer);
		}

		writer.WriteEndArray();
	}

	internal override TCollection? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		if (Activator.CreateInstance(typeof(TCollection)) is not ICollection<TElement> collection)
		{
			throw new NotSupportedException($"Collection type {typeof(TCollection).FullName} must implement ICollection<{typeof(TElement).Name}>.");
		}

		JsonConverter<TElement> elementConverter = this.owner.GetOrAddConverter<TElement>();
		reader.ReadStartArray();
		if (reader.TryReadEndArray())
		{
			return (TCollection)collection;
		}

		while (true)
		{
			collection.Add(elementConverter.Read(ref reader, serializer)!);
			if (reader.TryReadEndArray())
			{
				break;
			}

			reader.ReadValueSeparator();
		}

		return (TCollection)collection;
	}
}

internal sealed class JsonStringKeyDictionaryConverter<TDictionary, TValue> : JsonConverter<TDictionary>
	where TDictionary : IEnumerable<KeyValuePair<string, TValue>>
{
	private readonly JsonConverterCache owner;

	public JsonStringKeyDictionaryConverter(JsonConverterCache owner)
	{
		this.owner = owner;
	}

	internal override void Write(ref JsonWriter writer, TDictionary? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		JsonConverter<TValue> valueConverter = this.owner.GetOrAddConverter<TValue>();
		writer.WriteStartObject();
		bool first = true;
		foreach (KeyValuePair<string, TValue> entry in value)
		{
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			writer.WritePropertyName(this.owner.GetSerializedDictionaryKey(entry.Key));
			valueConverter.Write(ref writer, entry.Value, serializer);
		}

		writer.WriteEndObject();
	}

	internal override TDictionary? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		if (Activator.CreateInstance(typeof(TDictionary)) is not IDictionary<string, TValue> dictionary)
		{
			throw new NotSupportedException($"Dictionary type {typeof(TDictionary).FullName} must implement IDictionary<string, {typeof(TValue).Name}>.");
		}

		JsonConverter<TValue> valueConverter = this.owner.GetOrAddConverter<TValue>();
		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			return (TDictionary)dictionary;
		}

		while (true)
		{
			string key = reader.ReadRequiredString();
			reader.ReadNameSeparator();
			dictionary.Add(key, valueConverter.Read(ref reader, serializer)!);
			if (reader.TryReadEndObject())
			{
				break;
			}

			reader.ReadValueSeparator();
		}

		return (TDictionary)dictionary;
	}
}
