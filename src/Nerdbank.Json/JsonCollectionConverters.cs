// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1649 // File name should match first type name

namespace Nerdbank.Json;

internal interface IJsonDeserializeInto<TCollection>
{
	void DeserializeInto(ref JsonReader reader, ref TCollection collection, JsonSerializer serializer);
}

internal abstract class JsonEnumerableConverter<TEnumerable, TElement> : JsonConverter<TEnumerable>
{
	private readonly Func<TEnumerable, IEnumerable<TElement>> getEnumerable;
	private readonly JsonConverter<TElement> elementConverter;

	internal JsonEnumerableConverter(Func<TEnumerable, IEnumerable<TElement>> getEnumerable, JsonConverter<TElement> elementConverter)
	{
		this.getEnumerable = getEnumerable;
		this.elementConverter = elementConverter;
	}

	protected JsonConverter<TElement> ElementConverter => this.elementConverter;

	public override void Write(ref JsonWriter writer, TEnumerable? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStartArray();
		bool first = true;
		foreach (TElement element in this.getEnumerable(value))
		{
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			this.elementConverter.Write(ref writer, element, serializer);
		}

		writer.WriteEndArray();
	}
}

internal sealed class JsonReadOnlyEnumerableConverter<TEnumerable, TElement> : JsonEnumerableConverter<TEnumerable, TElement>
{
	internal JsonReadOnlyEnumerableConverter(Func<TEnumerable, IEnumerable<TElement>> getEnumerable, JsonConverter<TElement> elementConverter)
		: base(getEnumerable, elementConverter)
	{
	}

	public override TEnumerable? Read(ref JsonReader reader, JsonSerializer serializer)
		=> throw new NotSupportedException($"JSON deserialization does not support read-only enumerable type {typeof(TEnumerable).FullName}.");
}

internal sealed class JsonMutableEnumerableConverter<TEnumerable, TElement> : JsonEnumerableConverter<TEnumerable, TElement>, IJsonDeserializeInto<TEnumerable>
{
	private readonly EnumerableAppender<TEnumerable, TElement> addElement;
	private readonly MutableCollectionConstructor<TElement, TEnumerable> constructor;

	internal JsonMutableEnumerableConverter(Func<TEnumerable, IEnumerable<TElement>> getEnumerable, JsonConverter<TElement> elementConverter, EnumerableAppender<TEnumerable, TElement> addElement, MutableCollectionConstructor<TElement, TEnumerable> constructor)
		: base(getEnumerable, elementConverter)
	{
		this.addElement = addElement;
		this.constructor = constructor;
	}

	public void DeserializeInto(ref JsonReader reader, ref TEnumerable collection, JsonSerializer serializer)
	{
		reader.ReadStartArray();
		if (reader.TryReadEndArray())
		{
			return;
		}

		while (true)
		{
			this.addElement(ref collection, this.ElementConverter.Read(ref reader, serializer)!);
			if (reader.TryReadEndArray())
			{
				break;
			}

			reader.ReadValueSeparator();
		}
	}

	public override TEnumerable? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		TEnumerable result = this.constructor(default);
		this.DeserializeInto(ref reader, ref result, serializer);
		return result;
	}
}

internal sealed class JsonParameterizedEnumerableConverter<TEnumerable, TElement> : JsonEnumerableConverter<TEnumerable, TElement>
{
	private readonly ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> constructor;

	internal JsonParameterizedEnumerableConverter(Func<TEnumerable, IEnumerable<TElement>> getEnumerable, JsonConverter<TElement> elementConverter, ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> constructor)
		: base(getEnumerable, elementConverter)
	{
		this.constructor = constructor;
	}

	public override TEnumerable? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		List<TElement> elements = [];
		reader.ReadStartArray();
		if (!reader.TryReadEndArray())
		{
			while (true)
			{
				elements.Add(this.ElementConverter.Read(ref reader, serializer)!);
				if (reader.TryReadEndArray())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		return this.constructor(elements.ToArray(), default);
	}
}

internal abstract class JsonDictionaryConverter<TDictionary, TKey, TValue> : JsonConverter<TDictionary>
	where TKey : notnull
{
	private readonly Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getReadable;
	private readonly JsonConverter<TValue> valueConverter;
	private readonly ConverterCache owner;

	internal JsonDictionaryConverter(Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getReadable, JsonConverter<TValue> valueConverter, ConverterCache owner)
	{
		this.getReadable = getReadable;
		this.valueConverter = valueConverter;
		this.owner = owner;
	}

	protected JsonConverter<TValue> ValueConverter => this.valueConverter;

	public override void Write(ref JsonWriter writer, TDictionary? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStartObject();
		bool first = true;
		foreach (KeyValuePair<TKey, TValue> entry in this.getReadable(value))
		{
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			writer.WritePropertyName(this.owner.GetSerializedDictionaryKey(JsonDictionaryKeyConverter.FormatKey(entry.Key)));
			this.valueConverter.Write(ref writer, entry.Value, serializer);
		}

		writer.WriteEndObject();
	}
}

internal sealed class JsonReadOnlyDictionaryConverter<TDictionary, TKey, TValue> : JsonDictionaryConverter<TDictionary, TKey, TValue>
	where TKey : notnull
{
	internal JsonReadOnlyDictionaryConverter(Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getReadable, JsonConverter<TValue> valueConverter, ConverterCache owner)
		: base(getReadable, valueConverter, owner)
	{
	}

	public override TDictionary? Read(ref JsonReader reader, JsonSerializer serializer)
		=> throw new NotSupportedException($"JSON deserialization does not support read-only dictionary type {typeof(TDictionary).FullName}.");
}

internal sealed class JsonMutableDictionaryConverter<TDictionary, TKey, TValue> : JsonDictionaryConverter<TDictionary, TKey, TValue>, IJsonDeserializeInto<TDictionary>
	where TKey : notnull
{
	private readonly DictionaryInserter<TDictionary, TKey, TValue> addEntry;
	private readonly MutableCollectionConstructor<TKey, TDictionary> constructor;

	internal JsonMutableDictionaryConverter(Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getReadable, JsonConverter<TValue> valueConverter, ConverterCache owner, DictionaryInserter<TDictionary, TKey, TValue> addEntry, MutableCollectionConstructor<TKey, TDictionary> constructor)
		: base(getReadable, valueConverter, owner)
	{
		this.addEntry = addEntry;
		this.constructor = constructor;
	}

	public void DeserializeInto(ref JsonReader reader, ref TDictionary collection, JsonSerializer serializer)
	{
		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			return;
		}

		while (true)
		{
			string key = reader.ReadRequiredString();
			reader.ReadNameSeparator();
			this.addEntry(ref collection, JsonDictionaryKeyConverter.ParseKey<TKey>(key), this.ValueConverter.Read(ref reader, serializer)!);
			if (reader.TryReadEndObject())
			{
				break;
			}

			reader.ReadValueSeparator();
		}
	}

	public override TDictionary? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		TDictionary result = this.constructor(default);
		this.DeserializeInto(ref reader, ref result, serializer);
		return result;
	}
}

internal sealed class JsonParameterizedDictionaryConverter<TDictionary, TKey, TValue> : JsonDictionaryConverter<TDictionary, TKey, TValue>
	where TKey : notnull
{
	private readonly ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> constructor;

	internal JsonParameterizedDictionaryConverter(Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getReadable, JsonConverter<TValue> valueConverter, ConverterCache owner, ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> constructor)
		: base(getReadable, valueConverter, owner)
	{
		this.constructor = constructor;
	}

	public override TDictionary? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		List<KeyValuePair<TKey, TValue>> entries = [];
		reader.ReadStartObject();
		if (!reader.TryReadEndObject())
		{
			while (true)
			{
				string key = reader.ReadRequiredString();
				reader.ReadNameSeparator();
				entries.Add(new(JsonDictionaryKeyConverter.ParseKey<TKey>(key), this.ValueConverter.Read(ref reader, serializer)!));
				if (reader.TryReadEndObject())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		return this.constructor(entries.ToArray(), default);
	}
}

internal sealed class JsonListConverter<TCollection, TElement>(JsonConverter<TElement> elementConverter, Func<TCollection> createCollection) : JsonConverter<TCollection>
	where TCollection : IEnumerable<TElement>
{
	public override void Write(ref JsonWriter writer, TCollection? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

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

	public override TCollection? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		if (createCollection() is not ICollection<TElement> collection)
		{
			throw new NotSupportedException($"Collection type {typeof(TCollection).FullName} must implement ICollection<{typeof(TElement).Name}>.");
		}

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

internal sealed class JsonDictionaryCollectionConverter<TDictionary, TKey, TValue>(ConverterCache owner, JsonConverter<TValue> valueConverter, Func<TDictionary> createDictionary) : JsonConverter<TDictionary>
	where TDictionary : IEnumerable<KeyValuePair<TKey, TValue>>
	where TKey : notnull
{
	public override void Write(ref JsonWriter writer, TDictionary? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStartObject();
		bool first = true;
		foreach (KeyValuePair<TKey, TValue> entry in value)
		{
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			writer.WritePropertyName(owner.GetSerializedDictionaryKey(JsonDictionaryKeyConverter.FormatKey(entry.Key)));
			valueConverter.Write(ref writer, entry.Value, serializer);
		}

		writer.WriteEndObject();
	}

	public override TDictionary? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		if (createDictionary() is not IDictionary<TKey, TValue> dictionary)
		{
			throw new NotSupportedException($"Dictionary type {typeof(TDictionary).FullName} must implement IDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}>.");
		}

		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			return (TDictionary)dictionary;
		}

		while (true)
		{
			string key = reader.ReadRequiredString();
			reader.ReadNameSeparator();
			dictionary.Add(JsonDictionaryKeyConverter.ParseKey<TKey>(key), valueConverter.Read(ref reader, serializer)!);
			if (reader.TryReadEndObject())
			{
				break;
			}

			reader.ReadValueSeparator();
		}

		return (TDictionary)dictionary;
	}
}
