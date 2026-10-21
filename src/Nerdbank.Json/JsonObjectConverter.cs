// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1649 // File name should match first type name

using System.Text;

namespace Nerdbank.Json;

internal sealed class JsonObjectConverter<T> : JsonConverter<T>, IJsonReferencePreservingConverter<T>
{
	private readonly Func<T> factory;
	private readonly JsonExtensionData<T>? extensionData;
	private readonly JsonProperty<T>[] properties;
	private readonly Utf8PropertyLookup<T>? utf8PropertiesByName;
	private readonly Dictionary<string, JsonProperty<T>> propertiesByName;
	private readonly StringComparer propertyNameComparer;

	internal JsonObjectConverter(Func<T> factory, JsonProperty<T>[] properties, StringComparer propertyNameComparer, JsonExtensionData<T>? extensionData = null)
	{
		this.factory = factory;
		this.extensionData = extensionData;
		this.properties = properties;
		this.propertyNameComparer = propertyNameComparer;
		this.propertiesByName = new Dictionary<string, JsonProperty<T>>(properties.Length, propertyNameComparer);
		for (int i = 0; i < properties.Length; i++)
		{
			properties[i].Index = i;
			this.propertiesByName[properties[i].Name] = properties[i];
		}

		if (propertyNameComparer == StringComparer.Ordinal && properties.Length > 0)
		{
			this.utf8PropertiesByName = new(properties);
		}
	}

	public override bool PreferAsyncSerialization => true;

	public override void Write(ref JsonWriter writer, in T? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNull();
			return;
		}

		context.DepthStep();
		(value as IJsonSerializationCallbacks)?.OnBeforeSerialize();

		writer.WriteStartObject();
		bool first = true;
		for (int i = 0; i < this.properties.Length; i++)
		{
			JsonProperty<T> property = this.properties[i];
			if (!property.CanSerialize)
			{
				continue;
			}

			if (property.Write(ref writer, value, context, first))
			{
				first = false;
			}
		}

		this.extensionData?.Write(ref writer, value, ref first);

		writer.WriteEndObject();
	}

	public override async ValueTask WriteAsync(JsonAsyncWriter writer, T? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is null)
		{
			await writer.WriteNullAsync(context).ConfigureAwait(false);
			return;
		}

		context.DepthStep();
		(value as IJsonSerializationCallbacks)?.OnBeforeSerialize();

		JsonWriter syncWriter = writer.CreateWriter();
		syncWriter.WriteStartObject();
		writer.ReturnWriter(ref syncWriter);

		bool first = true;
		for (int i = 0; i < this.properties.Length; i++)
		{
			JsonProperty<T> property = this.properties[i];
			if (!property.CanSerialize)
			{
				continue;
			}

			if (await property.WriteAsync(writer, value, context, first).ConfigureAwait(false))
			{
				first = false;
			}
		}

		syncWriter = writer.CreateWriter();
		this.extensionData?.Write(ref syncWriter, value, ref first);
		syncWriter.WriteEndObject();
		writer.ReturnWriter(ref syncWriter);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	public override T? Read(ref JsonReader reader, SerializationContext context)
	{
		if (!typeof(T).IsValueType && reader.TryReadNull())
		{
			return default;
		}

		T result = this.factory();
		this.PopulateReference(ref reader, ref result, context);
		return result;
	}

	public T CreateReferenceInstance() => this.factory();

	public void PopulateReference(ref JsonReader reader, ref T result, SerializationContext context)
	{
		context.DepthStep();

		PropertyCollisionDetection collisionDetection = new(this.propertyNameComparer, this.properties.Length);
		UnusedDataPacket.Map? unused = null;
		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			(result as IJsonSerializationCallbacks)?.OnAfterDeserialize();
			return;
		}

		while (true)
		{
			string? propertyName = null;
			JsonProperty<T>? property = null;
			if (this.utf8PropertiesByName is not null && reader.TryReadUnescapedUtf8StringToken(out ReadOnlySpan<byte> utf8PropertyName, out long start, out long length))
			{
				if (utf8PropertyName.IsEmpty)
				{
					property = this.FindSplitProperty(ref reader, start, length, out propertyName);
				}
				else if (!this.utf8PropertiesByName.TryGetValue(utf8PropertyName, out property))
				{
					propertyName = Encoding.UTF8.GetString(utf8PropertyName[1..^1]);
				}

				if (property is not null)
				{
					collisionDetection.MarkAsRead(property.Index, property.Name);
				}
				else
				{
					collisionDetection.MarkAsRead(propertyName!);
				}
			}
			else
			{
				propertyName = reader.ReadRequiredString();
				if (this.propertiesByName.TryGetValue(propertyName, out property))
				{
					collisionDetection.MarkAsRead(property.Index, property.Name);
				}
				else
				{
					collisionDetection.MarkAsRead(propertyName);
				}
			}

			reader.ReadNameSeparator();

			if (property is not null && property.CanDeserialize)
			{
				property.Read(ref reader, ref result, context);
			}
			else if (this.extensionData?.CanRead is true)
			{
				propertyName ??= property?.Name ?? throw new InvalidOperationException("The property name should be available for extension data.");
				(unused ??= new()).Add(propertyName, reader.ReadRawValue(context));
			}
			else
			{
				reader.Skip(context);
			}

			if (reader.TryReadEndObject())
			{
				break;
			}

			reader.ReadValueSeparator();
		}

		this.extensionData?.Apply(ref result, unused);
		(result as IJsonSerializationCallbacks)?.OnAfterDeserialize();
	}

	public override async ValueTask<T?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		if (!typeof(T).IsValueType && await reader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			return default;
		}

		T result = this.factory();
		return await this.PopulateAsyncCore(reader, result, context).ConfigureAwait(false);
	}

	public async ValueTask<T> PopulateReferenceAsync(JsonAsyncReader reader, T instance, SerializationContext context)
		=> await this.PopulateAsyncCore(reader, instance, context).ConfigureAwait(false);

	private JsonProperty<T>? FindSplitProperty(ref JsonReader reader, long start, long length, out string? propertyName)
	{
		using JsonInput.Token token = reader.CopyToken(start, length, stackalloc byte[256]);
		reader.ConsumeStringToken(start, length, token.Span);
		if (this.utf8PropertiesByName!.TryGetValue(token.Span, out JsonProperty<T>? property))
		{
			propertyName = null;
			return property;
		}

		propertyName = Encoding.UTF8.GetString(token.Span[1..^1]);
		return null;
	}

	private async ValueTask<T> PopulateAsyncCore(JsonAsyncReader reader, T result, SerializationContext context)
	{
		context.DepthStep();

		PropertyCollisionDetection collisionDetection = new(this.propertyNameComparer, this.properties.Length);
		UnusedDataPacket.Map? unused = null;
		await reader.ReadStartObjectAsync(context).ConfigureAwait(false);
		if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
		{
			(result as IJsonSerializationCallbacks)?.OnAfterDeserialize();
			return result;
		}

		while (true)
		{
			string propertyName = await reader.ReadPropertyNameAsync(context).ConfigureAwait(false);
			JsonProperty<T>? property = this.propertiesByName.TryGetValue(propertyName, out JsonProperty<T>? matched) ? matched : null;
			if (property is not null)
			{
				collisionDetection.MarkAsRead(property.Index, property.Name);
			}
			else
			{
				collisionDetection.MarkAsRead(propertyName);
			}

			if (property is not null && property.CanDeserialize)
			{
				result = await property.ReadValueIntoAsync(reader, result, context).ConfigureAwait(false);
			}
			else if (this.extensionData?.CanRead is true)
			{
				(unused ??= new()).Add(propertyName, await reader.ReadRawValueAsync(context).ConfigureAwait(false));
			}
			else
			{
				await reader.SkipValueAsync(context).ConfigureAwait(false);
			}

			if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
			{
				break;
			}

			await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
		}

		this.extensionData?.Apply(ref result, unused);
		(result as IJsonSerializationCallbacks)?.OnAfterDeserialize();
		return result;
	}
}

internal sealed class JsonExtensionData<TDeclaring>
{
	private readonly Getter<TDeclaring, UnusedDataPacket>? getter;
	private readonly Setter<TDeclaring, UnusedDataPacket>? setter;

	internal JsonExtensionData(Getter<TDeclaring, UnusedDataPacket>? getter, Setter<TDeclaring, UnusedDataPacket>? setter)
	{
		this.getter = getter;
		this.setter = setter;
	}

	internal bool CanRead => this.setter is not null;

	internal void Write(ref JsonWriter writer, TDeclaring value, ref bool first)
	{
		if (this.getter?.Invoke(ref value) is UnusedDataPacket.Map packet)
		{
			packet.WriteTo(ref writer, ref first);
		}
	}

	internal void Apply(ref TDeclaring value, UnusedDataPacket.Map? packet)
	{
		if (packet is not null && value is not null)
		{
			this.setter?.Invoke(ref value, packet);
		}
	}
}

internal abstract class JsonProperty<TDeclaring>
{
	private readonly byte[] encodedName;

	internal JsonProperty(string name)
	{
		this.Name = name;
		this.encodedName = JsonWriter.EncodePropertyName(name);
	}

	internal string Name { get; }

	internal int Index { get; set; }

	internal ReadOnlySpan<byte> EncodedName => this.encodedName;

	internal abstract bool CanSerialize { get; }

	internal abstract bool CanDeserialize { get; }

	internal abstract bool Write(ref JsonWriter writer, TDeclaring container, in SerializationContext context, bool first);

	internal abstract ValueTask<bool> WriteAsync(JsonAsyncWriter writer, TDeclaring container, SerializationContext context, bool first);

	internal abstract void Read(ref JsonReader reader, ref TDeclaring container, in SerializationContext context);

	internal abstract ValueTask<TDeclaring> ReadValueIntoAsync(JsonAsyncReader reader, TDeclaring container, SerializationContext context);
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
	private readonly TProperty? defaultValue;

	internal JsonProperty(string name, string memberName, Getter<TDeclaring, TProperty>? getter, Setter<TDeclaring, TProperty>? setter, JsonConverter<TProperty> converter, bool deserializeIntoExistingInstance = false, bool isRequired = false, bool isNonNullableReferenceType = false, TProperty? defaultValue = default)
		: base(name)
	{
		this.memberName = memberName;
		this.getter = getter;
		this.setter = setter;
		this.converter = converter;
		this.deserializeIntoExistingInstance = deserializeIntoExistingInstance;
		this.isRequired = isRequired;
		this.isNonNullableReferenceType = isNonNullableReferenceType;
		this.defaultValue = defaultValue;
	}

	internal override bool CanSerialize => this.getter is not null;

	internal override bool CanDeserialize => this.setter is not null || this.deserializeIntoExistingInstance;

	internal override bool Write(ref JsonWriter writer, TDeclaring container, in SerializationContext context, bool first)
	{
		if (this.getter is null)
		{
			throw new InvalidOperationException("Property has no getter.");
		}

		TProperty? value = this.getter(ref container);
		if (!this.ShouldSerializeValue(value, context.SerializeDefaultValues))
		{
			return false;
		}

		if (!first)
		{
			writer.WriteValueSeparator();
		}

		writer.WritePropertyName(this.EncodedName);
		this.converter.Write(ref writer, value, context);
		return true;
	}

	internal override async ValueTask<bool> WriteAsync(JsonAsyncWriter writer, TDeclaring container, SerializationContext context, bool first)
	{
		if (this.getter is null)
		{
			throw new InvalidOperationException("Property has no getter.");
		}

		TProperty? value = this.getter(ref container);
		if (!this.ShouldSerializeValue(value, context.SerializeDefaultValues))
		{
			return false;
		}

		JsonWriter syncWriter = writer.CreateWriter();
		if (!first)
		{
			syncWriter.WriteValueSeparator();
		}

		syncWriter.WritePropertyName(this.EncodedName);
		writer.ReturnWriter(ref syncWriter);
		await writer.WriteValueAsync(this.converter, value, context).ConfigureAwait(false);
		return true;
	}

	internal override void Read(ref JsonReader reader, ref TDeclaring container, in SerializationContext context)
	{
		if (this.setter is not null)
		{
			TProperty? value = this.converter.Read(ref reader, context);
			if (this.isNonNullableReferenceType
				&& value is null
				&& !typeof(TProperty).IsValueType
				&& (context.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties) != DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties)
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
			deserializeInto.DeserializeInto(ref reader, ref collection, context);
			return;
		}

		if (this.setter is null)
		{
			reader.Skip(context);
			return;
		}
	}

	internal override async ValueTask<TDeclaring> ReadValueIntoAsync(JsonAsyncReader reader, TDeclaring container, SerializationContext context)
	{
		if (this.setter is not null)
		{
			TProperty? value = await reader.ReadValueAsync(this.converter, context).ConfigureAwait(false);
			if (this.isNonNullableReferenceType
				&& value is null
				&& !typeof(TProperty).IsValueType
				&& (context.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties) != DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties)
			{
				throw new FormatException($"Property '{this.memberName}' does not allow null values.");
			}

			this.setter(ref container, value!);
			return container;
		}

		if (this.deserializeIntoExistingInstance && this.getter is not null && this.converter is IJsonDeserializeInto<TProperty> deserializeInto)
		{
			if (!typeof(TProperty).IsValueType && await reader.TryReadNullAsync(context).ConfigureAwait(false))
			{
				return container;
			}

			TProperty collection = this.getter(ref container);
			await deserializeInto.DeserializeIntoAsync(reader, collection, context).ConfigureAwait(false);
			return container;
		}

		await reader.SkipValueAsync(context).ConfigureAwait(false);
		return container;
	}

	private bool ShouldSerializeValue(TProperty? value, SerializeDefaultValuesPolicy policy)
	{
		if (policy == SerializeDefaultValuesPolicy.Always)
		{
			return true;
		}

		if (!EqualityComparer<TProperty>.Default.Equals(value!, this.defaultValue!))
		{
			return true;
		}

		if (this.isRequired && (policy & SerializeDefaultValuesPolicy.Required) == SerializeDefaultValuesPolicy.Required)
		{
			return true;
		}

		if (typeof(TProperty).IsValueType)
		{
			return (policy & SerializeDefaultValuesPolicy.ValueTypes) == SerializeDefaultValuesPolicy.ValueTypes;
		}

		return (policy & SerializeDefaultValuesPolicy.ReferenceTypes) == SerializeDefaultValuesPolicy.ReferenceTypes;
	}
}

internal sealed class Utf8PropertyLookup<TDeclaring>
{
	private readonly int[] buckets;
	private readonly Entry[] entries;

	internal Utf8PropertyLookup(JsonProperty<TDeclaring>[] properties)
	{
		this.buckets = new int[properties.Length];
		this.entries = new Entry[properties.Length];
		for (int i = 0; i < properties.Length; i++)
		{
			JsonProperty<TDeclaring> property = properties[i];
			ReadOnlySpan<byte> name = property.EncodedName;
			int hashCode = GetHashCode(name);
			int bucketIndex = (hashCode & 0x7fffffff) % this.buckets.Length;
			this.entries[i] = new Entry(hashCode, this.buckets[bucketIndex] - 1, property);
			this.buckets[bucketIndex] = i + 1;
		}
	}

	internal bool TryGetValue(ReadOnlySpan<byte> name, out JsonProperty<TDeclaring>? property)
	{
		int hashCode = GetHashCode(name);
		for (int i = this.buckets[(hashCode & 0x7fffffff) % this.buckets.Length] - 1; i >= 0; i = this.entries[i].Next)
		{
			Entry entry = this.entries[i];
			if (entry.HashCode == hashCode && name.SequenceEqual(entry.Property.EncodedName))
			{
				property = entry.Property;
				return true;
			}
		}

		property = null;
		return false;
	}

	private static int GetHashCode(ReadOnlySpan<byte> name)
	{
		unchecked
		{
			int hash = 17;
			for (int i = 0; i < name.Length; i++)
			{
				hash = (hash * 31) + name[i];
			}

			return hash;
		}
	}

	private readonly struct Entry
	{
		internal Entry(int hashCode, int next, JsonProperty<TDeclaring> property)
		{
			this.HashCode = hashCode;
			this.Next = next;
			this.Property = property;
		}

		internal int HashCode { get; }

		internal int Next { get; }

		internal JsonProperty<TDeclaring> Property { get; }
	}
}
