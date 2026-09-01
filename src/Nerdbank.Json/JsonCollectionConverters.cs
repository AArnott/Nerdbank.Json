// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1649 // File name should match first type name

namespace Nerdbank.Json;

internal interface IJsonDeserializeInto<TCollection>
{
	void DeserializeInto(ref JsonReader reader, ref TCollection collection, SerializationContext context);

	ValueTask DeserializeIntoAsync(JsonAsyncReader reader, TCollection collection, SerializationContext context);
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

	public override bool PreferAsyncSerialization => true;

	protected JsonConverter<TElement> ElementConverter => this.elementConverter;

	public override void Write(ref JsonWriter writer, TEnumerable? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();

		writer.WriteStartArray();
		bool first = true;
		foreach (TElement element in this.getEnumerable(value))
		{
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			this.elementConverter.Write(ref writer, element, context);
		}

		writer.WriteEndArray();
	}

	public override async ValueTask WriteAsync(JsonAsyncWriter writer, TEnumerable? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is null)
		{
			await writer.WriteNullAsync(context).ConfigureAwait(false);
			return;
		}

		context.DepthStep();

		JsonWriter syncWriter = writer.CreateWriter();
		syncWriter.WriteStartArray();
		writer.ReturnWriter(ref syncWriter);

		bool first = true;
		foreach (TElement element in this.getEnumerable(value))
		{
			if (!first)
			{
				syncWriter = writer.CreateWriter();
				syncWriter.WriteValueSeparator();
				writer.ReturnWriter(ref syncWriter);
			}

			first = false;
			await writer.WriteValueAsync(this.elementConverter, element, context).ConfigureAwait(false);
		}

		syncWriter = writer.CreateWriter();
		syncWriter.WriteEndArray();
		writer.ReturnWriter(ref syncWriter);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	private protected async ValueTask ReadArrayElementsAsync(JsonAsyncReader reader, SerializationContext context, Action<TElement> add)
	{
		await reader.ReadStartArrayAsync(context).ConfigureAwait(false);
		if (await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
		{
			return;
		}

		while (true)
		{
			TElement element = (await reader.ReadValueAsync(this.elementConverter, context).ConfigureAwait(false))!;
			add(element);
			if (await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
			{
				break;
			}

			await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
		}
	}
}

internal sealed class JsonReadOnlyEnumerableConverter<TEnumerable, TElement> : JsonEnumerableConverter<TEnumerable, TElement>
{
	internal JsonReadOnlyEnumerableConverter(Func<TEnumerable, IEnumerable<TElement>> getEnumerable, JsonConverter<TElement> elementConverter)
		: base(getEnumerable, elementConverter)
	{
	}

	public override TEnumerable? Read(ref JsonReader reader, SerializationContext context)
		=> throw new NotSupportedException($"JSON deserialization does not support read-only enumerable type {typeof(TEnumerable).FullName}.");
}

internal sealed class JsonMutableEnumerableConverter<TEnumerable, TElement> : JsonEnumerableConverter<TEnumerable, TElement>, IJsonDeserializeInto<TEnumerable>, IJsonReferencePreservingConverter<TEnumerable>
{
	private readonly EnumerableAppender<TEnumerable, TElement> addElement;
	private readonly MutableCollectionConstructor<TElement, TEnumerable> constructor;
	private readonly CollectionConstructionOptions<TElement> constructionOptions;

	internal JsonMutableEnumerableConverter(Func<TEnumerable, IEnumerable<TElement>> getEnumerable, JsonConverter<TElement> elementConverter, EnumerableAppender<TEnumerable, TElement> addElement, MutableCollectionConstructor<TElement, TEnumerable> constructor, CollectionConstructionOptions<TElement> constructionOptions)
		: base(getEnumerable, elementConverter)
	{
		this.addElement = addElement;
		this.constructor = constructor;
		this.constructionOptions = constructionOptions;
	}

	public void DeserializeInto(ref JsonReader reader, ref TEnumerable collection, SerializationContext context)
	{
		reader.ReadStartArray();
		if (reader.TryReadEndArray())
		{
			return;
		}

		while (true)
		{
			this.addElement(ref collection, this.ElementConverter.Read(ref reader, context)!);
			if (reader.TryReadEndArray())
			{
				break;
			}

			reader.ReadValueSeparator();
		}
	}

	public TEnumerable CreateReferenceInstance() => this.constructor(this.constructionOptions);

	public void PopulateReference(ref JsonReader reader, ref TEnumerable instance, SerializationContext context)
	{
		context.DepthStep();
		this.DeserializeInto(ref reader, ref instance, context);
	}

	public async ValueTask DeserializeIntoAsync(JsonAsyncReader reader, TEnumerable collection, SerializationContext context)
		=> await this.ReadArrayElementsAsync(reader, context, element => this.addElement(ref collection, element!)).ConfigureAwait(false);

	public async ValueTask<TEnumerable> PopulateReferenceAsync(JsonAsyncReader reader, TEnumerable instance, SerializationContext context)
	{
		context.DepthStep();
		await this.DeserializeIntoAsync(reader, instance, context).ConfigureAwait(false);
		return instance;
	}

	public override TEnumerable? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		TEnumerable result = this.CreateReferenceInstance();
		this.PopulateReference(ref reader, ref result, context);
		return result;
	}

	public override async ValueTask<TEnumerable?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		if (await reader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			return default;
		}

		context.DepthStep();
		TEnumerable collection = this.CreateReferenceInstance();
		await this.ReadArrayElementsAsync(reader, context, element => this.addElement(ref collection, element!)).ConfigureAwait(false);
		return collection;
	}
}

internal sealed class JsonParameterizedEnumerableConverter<TEnumerable, TElement> : JsonEnumerableConverter<TEnumerable, TElement>
{
	private readonly ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> constructor;
	private readonly CollectionConstructionOptions<TElement> constructionOptions;

	internal JsonParameterizedEnumerableConverter(Func<TEnumerable, IEnumerable<TElement>> getEnumerable, JsonConverter<TElement> elementConverter, ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> constructor, CollectionConstructionOptions<TElement> constructionOptions)
		: base(getEnumerable, elementConverter)
	{
		this.constructor = constructor;
		this.constructionOptions = constructionOptions;
	}

	public override TEnumerable? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		context.DepthStep();

		List<TElement> elements = [];
		reader.ReadStartArray();
		if (!reader.TryReadEndArray())
		{
			while (true)
			{
				elements.Add(this.ElementConverter.Read(ref reader, context)!);
				if (reader.TryReadEndArray())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		return this.constructor(elements.ToArray(), this.constructionOptions);
	}

	public override async ValueTask<TEnumerable?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		if (await reader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			return default;
		}

		context.DepthStep();
		List<TElement> elements = [];
		await this.ReadArrayElementsAsync(reader, context, element => elements.Add(element!)).ConfigureAwait(false);
		return this.constructor(elements.ToArray(), this.constructionOptions);
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

	public override bool PreferAsyncSerialization => true;

	protected JsonConverter<TValue> ValueConverter => this.valueConverter;

	public override void Write(ref JsonWriter writer, TDictionary? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();

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
			this.valueConverter.Write(ref writer, entry.Value, context);
		}

		writer.WriteEndObject();
	}

	public override async ValueTask WriteAsync(JsonAsyncWriter writer, TDictionary? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is null)
		{
			await writer.WriteNullAsync(context).ConfigureAwait(false);
			return;
		}

		context.DepthStep();

		JsonWriter syncWriter = writer.CreateWriter();
		syncWriter.WriteStartObject();
		writer.ReturnWriter(ref syncWriter);

		bool first = true;
		foreach (KeyValuePair<TKey, TValue> entry in this.getReadable(value))
		{
			syncWriter = writer.CreateWriter();
			if (!first)
			{
				syncWriter.WriteValueSeparator();
			}

			syncWriter.WritePropertyName(this.owner.GetSerializedDictionaryKey(JsonDictionaryKeyConverter.FormatKey(entry.Key)));
			writer.ReturnWriter(ref syncWriter);
			first = false;
			await writer.WriteValueAsync(this.valueConverter, entry.Value, context).ConfigureAwait(false);
		}

		syncWriter = writer.CreateWriter();
		syncWriter.WriteEndObject();
		writer.ReturnWriter(ref syncWriter);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	private protected async ValueTask ReadObjectEntriesAsync(JsonAsyncReader reader, SerializationContext context, Action<string, TValue> add)
	{
		await reader.ReadStartObjectAsync(context).ConfigureAwait(false);
		if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
		{
			return;
		}

		while (true)
		{
			string key = await reader.ReadPropertyNameAsync(context).ConfigureAwait(false);
			TValue entryValue = (await reader.ReadValueAsync(this.valueConverter, context).ConfigureAwait(false))!;
			add(key, entryValue);
			if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
			{
				break;
			}

			await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
		}
	}
}

internal sealed class JsonReadOnlyDictionaryConverter<TDictionary, TKey, TValue> : JsonDictionaryConverter<TDictionary, TKey, TValue>
	where TKey : notnull
{
	internal JsonReadOnlyDictionaryConverter(Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getReadable, JsonConverter<TValue> valueConverter, ConverterCache owner)
		: base(getReadable, valueConverter, owner)
	{
	}

	public override TDictionary? Read(ref JsonReader reader, SerializationContext context)
		=> throw new NotSupportedException($"JSON deserialization does not support read-only dictionary type {typeof(TDictionary).FullName}.");
}

internal sealed class JsonMutableDictionaryConverter<TDictionary, TKey, TValue> : JsonDictionaryConverter<TDictionary, TKey, TValue>, IJsonDeserializeInto<TDictionary>, IJsonReferencePreservingConverter<TDictionary>
	where TKey : notnull
{
	private readonly DictionaryInserter<TDictionary, TKey, TValue> addEntry;
	private readonly MutableCollectionConstructor<TKey, TDictionary> constructor;
	private readonly CollectionConstructionOptions<TKey> constructionOptions;

	internal JsonMutableDictionaryConverter(Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getReadable, JsonConverter<TValue> valueConverter, ConverterCache owner, DictionaryInserter<TDictionary, TKey, TValue> addEntry, MutableCollectionConstructor<TKey, TDictionary> constructor, CollectionConstructionOptions<TKey> constructionOptions)
		: base(getReadable, valueConverter, owner)
	{
		this.addEntry = addEntry;
		this.constructor = constructor;
		this.constructionOptions = constructionOptions;
	}

	public void DeserializeInto(ref JsonReader reader, ref TDictionary collection, SerializationContext context)
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
			this.addEntry(ref collection, JsonDictionaryKeyConverter.ParseKey<TKey>(key), this.ValueConverter.Read(ref reader, context)!);
			if (reader.TryReadEndObject())
			{
				break;
			}

			reader.ReadValueSeparator();
		}
	}

	public TDictionary CreateReferenceInstance() => this.constructor(this.constructionOptions);

	public void PopulateReference(ref JsonReader reader, ref TDictionary instance, SerializationContext context)
	{
		context.DepthStep();
		this.DeserializeInto(ref reader, ref instance, context);
	}

	public async ValueTask DeserializeIntoAsync(JsonAsyncReader reader, TDictionary collection, SerializationContext context)
		=> await this.ReadObjectEntriesAsync(reader, context, (key, entryValue) => this.addEntry(ref collection, JsonDictionaryKeyConverter.ParseKey<TKey>(key), entryValue!)).ConfigureAwait(false);

	public async ValueTask<TDictionary> PopulateReferenceAsync(JsonAsyncReader reader, TDictionary instance, SerializationContext context)
	{
		context.DepthStep();
		await this.DeserializeIntoAsync(reader, instance, context).ConfigureAwait(false);
		return instance;
	}

	public override TDictionary? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		TDictionary result = this.CreateReferenceInstance();
		this.PopulateReference(ref reader, ref result, context);
		return result;
	}

	public override async ValueTask<TDictionary?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		if (await reader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			return default;
		}

		context.DepthStep();
		TDictionary collection = this.CreateReferenceInstance();
		await this.ReadObjectEntriesAsync(reader, context, (key, entryValue) => this.addEntry(ref collection, JsonDictionaryKeyConverter.ParseKey<TKey>(key), entryValue!)).ConfigureAwait(false);
		return collection;
	}
}

internal sealed class JsonParameterizedDictionaryConverter<TDictionary, TKey, TValue> : JsonDictionaryConverter<TDictionary, TKey, TValue>
	where TKey : notnull
{
	private readonly ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> constructor;
	private readonly CollectionConstructionOptions<TKey> constructionOptions;

	internal JsonParameterizedDictionaryConverter(Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getReadable, JsonConverter<TValue> valueConverter, ConverterCache owner, ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> constructor, CollectionConstructionOptions<TKey> constructionOptions)
		: base(getReadable, valueConverter, owner)
	{
		this.constructor = constructor;
		this.constructionOptions = constructionOptions;
	}

	public override TDictionary? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		context.DepthStep();

		List<KeyValuePair<TKey, TValue>> entries = [];
		reader.ReadStartObject();
		if (!reader.TryReadEndObject())
		{
			while (true)
			{
				string key = reader.ReadRequiredString();
				reader.ReadNameSeparator();
				entries.Add(new(JsonDictionaryKeyConverter.ParseKey<TKey>(key), this.ValueConverter.Read(ref reader, context)!));
				if (reader.TryReadEndObject())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		return this.constructor(entries.ToArray(), this.constructionOptions);
	}

	public override async ValueTask<TDictionary?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		if (await reader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			return default;
		}

		context.DepthStep();
		List<KeyValuePair<TKey, TValue>> entries = [];
		await this.ReadObjectEntriesAsync(reader, context, (key, entryValue) => entries.Add(new(JsonDictionaryKeyConverter.ParseKey<TKey>(key), entryValue!))).ConfigureAwait(false);
		return this.constructor(entries.ToArray(), this.constructionOptions);
	}
}

internal sealed class JsonListConverter<TCollection, TElement>(JsonConverter<TElement> elementConverter, Func<TCollection> createCollection) : JsonConverter<TCollection>
	where TCollection : IEnumerable<TElement>
{
	public override bool PreferAsyncSerialization => true;

	public override void Write(ref JsonWriter writer, TCollection? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();

		writer.WriteStartArray();
		bool first = true;
		foreach (TElement element in value)
		{
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			elementConverter.Write(ref writer, element, context);
		}

		writer.WriteEndArray();
	}

	public override TCollection? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		context.DepthStep();

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
			collection.Add(elementConverter.Read(ref reader, context)!);
			if (reader.TryReadEndArray())
			{
				break;
			}

			reader.ReadValueSeparator();
		}

		return (TCollection)collection;
	}

	public override async ValueTask WriteAsync(JsonAsyncWriter writer, TCollection? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is null)
		{
			await writer.WriteNullAsync(context).ConfigureAwait(false);
			return;
		}

		context.DepthStep();

		JsonWriter syncWriter = writer.CreateWriter();
		syncWriter.WriteStartArray();
		writer.ReturnWriter(ref syncWriter);

		bool first = true;
		foreach (TElement element in value)
		{
			if (!first)
			{
				syncWriter = writer.CreateWriter();
				syncWriter.WriteValueSeparator();
				writer.ReturnWriter(ref syncWriter);
			}

			first = false;
			await writer.WriteValueAsync(elementConverter, element, context).ConfigureAwait(false);
		}

		syncWriter = writer.CreateWriter();
		syncWriter.WriteEndArray();
		writer.ReturnWriter(ref syncWriter);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	public override async ValueTask<TCollection?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		if (await reader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			return default;
		}

		context.DepthStep();

		if (createCollection() is not ICollection<TElement> collection)
		{
			throw new NotSupportedException($"Collection type {typeof(TCollection).FullName} must implement ICollection<{typeof(TElement).Name}>.");
		}

		await reader.ReadStartArrayAsync(context).ConfigureAwait(false);
		if (!await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
		{
			while (true)
			{
				collection.Add((await reader.ReadValueAsync(elementConverter, context).ConfigureAwait(false))!);
				if (await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
				{
					break;
				}

				await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
			}
		}

		return (TCollection)collection;
	}
}

internal sealed class JsonDictionaryCollectionConverter<TDictionary, TKey, TValue>(ConverterCache owner, JsonConverter<TValue> valueConverter, Func<TDictionary> createDictionary) : JsonConverter<TDictionary>
	where TDictionary : IEnumerable<KeyValuePair<TKey, TValue>>
	where TKey : notnull
{
	public override bool PreferAsyncSerialization => true;

	public override void Write(ref JsonWriter writer, TDictionary? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();

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
			valueConverter.Write(ref writer, entry.Value, context);
		}

		writer.WriteEndObject();
	}

	public override TDictionary? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		context.DepthStep();

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
			dictionary.Add(JsonDictionaryKeyConverter.ParseKey<TKey>(key), valueConverter.Read(ref reader, context)!);
			if (reader.TryReadEndObject())
			{
				break;
			}

			reader.ReadValueSeparator();
		}

		return (TDictionary)dictionary;
	}

	public override async ValueTask WriteAsync(JsonAsyncWriter writer, TDictionary? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is null)
		{
			await writer.WriteNullAsync(context).ConfigureAwait(false);
			return;
		}

		context.DepthStep();

		JsonWriter syncWriter = writer.CreateWriter();
		syncWriter.WriteStartObject();
		writer.ReturnWriter(ref syncWriter);

		bool first = true;
		foreach (KeyValuePair<TKey, TValue> entry in value)
		{
			syncWriter = writer.CreateWriter();
			if (!first)
			{
				syncWriter.WriteValueSeparator();
			}

			syncWriter.WritePropertyName(owner.GetSerializedDictionaryKey(JsonDictionaryKeyConverter.FormatKey(entry.Key)));
			writer.ReturnWriter(ref syncWriter);
			first = false;
			await writer.WriteValueAsync(valueConverter, entry.Value, context).ConfigureAwait(false);
		}

		syncWriter = writer.CreateWriter();
		syncWriter.WriteEndObject();
		writer.ReturnWriter(ref syncWriter);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	public override async ValueTask<TDictionary?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		if (await reader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			return default;
		}

		context.DepthStep();

		if (createDictionary() is not IDictionary<TKey, TValue> dictionary)
		{
			throw new NotSupportedException($"Dictionary type {typeof(TDictionary).FullName} must implement IDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}>.");
		}

		await reader.ReadStartObjectAsync(context).ConfigureAwait(false);
		if (!await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
		{
			while (true)
			{
				string key = await reader.ReadPropertyNameAsync(context).ConfigureAwait(false);
				dictionary.Add(JsonDictionaryKeyConverter.ParseKey<TKey>(key), (await reader.ReadValueAsync(valueConverter, context).ConfigureAwait(false))!);
				if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
				{
					break;
				}

				await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
			}
		}

		return (TDictionary)dictionary;
	}
}
