// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1649 // File name should match first type name

using System.Text;

namespace Nerdbank.Json;

internal sealed class JsonObjectConverter<T> : JsonConverter<T>
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

		if (propertyNameComparer == StringComparer.Ordinal)
		{
			this.utf8PropertiesByName = new(properties);
		}
	}

	public override void Write(ref JsonWriter writer, T? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();

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

	public override T? Read(ref JsonReader reader, SerializationContext context)
	{
		if (!typeof(T).IsValueType && reader.TryReadNull())
		{
			return default;
		}

		context.DepthStep();

		T result = this.factory();
		PropertyCollisionDetection collisionDetection = new(this.propertyNameComparer, this.properties.Length);
		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			return result;
		}

		while (true)
		{
			string? propertyName = null;
			JsonProperty<T>? property = null;
			if (this.utf8PropertiesByName is not null && reader.TryReadUnescapedUtf8StringToken(out ReadOnlySpan<byte> utf8PropertyName))
			{
				if (this.utf8PropertiesByName.TryGetValue(utf8PropertyName, out property))
				{
					collisionDetection.MarkAsRead(property!.Index, property.Name);
				}
				else
				{
					propertyName = Encoding.UTF8.GetString(utf8PropertyName[1..^1]);
					collisionDetection.MarkAsRead(propertyName);
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
			else if (this.extensionData is not null)
			{
				propertyName ??= property?.Name ?? throw new InvalidOperationException("The property name should be available for extension data.");
				this.extensionData.Read(ref reader, ref result, propertyName);
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

internal abstract class JsonExtensionData<TDeclaring>
{
	internal abstract void Write(ref JsonWriter writer, TDeclaring value, ref bool first);

	internal abstract void Read(ref JsonReader reader, ref TDeclaring value, string propertyName);

	internal abstract Dictionary<string, string>? Read(ref JsonReader reader, string propertyName);

	internal abstract void Apply(TDeclaring value, Dictionary<string, string> extensionData);
}

internal sealed class JsonExtensionData<TDeclaring, TProperty> : JsonExtensionData<TDeclaring>
{
	private readonly Getter<TDeclaring, TProperty>? getter;
	private readonly Setter<TDeclaring, TProperty>? setter;

	internal JsonExtensionData(Getter<TDeclaring, TProperty>? getter, Setter<TDeclaring, TProperty>? setter)
	{
		this.getter = getter;
		this.setter = setter;
	}

	internal override void Write(ref JsonWriter writer, TDeclaring value, ref bool first)
	{
		if (this.getter is null)
		{
			return;
		}

		if (this.getter(ref value) is not IEnumerable<KeyValuePair<string, string>> entries)
		{
			return;
		}

		foreach (KeyValuePair<string, string> entry in entries)
		{
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			writer.WritePropertyName(entry.Key);
			writer.WriteRawValue(entry.Value);
			first = false;
		}
	}

	internal override void Read(ref JsonReader reader, ref TDeclaring value, string propertyName)
	{
		IDictionary<string, string> dictionary = this.GetOrCreateWritableDictionary(ref value);
		dictionary[propertyName] = reader.ReadRawValue();
	}

	internal override Dictionary<string, string>? Read(ref JsonReader reader, string propertyName)
		=> new(StringComparer.Ordinal) { [propertyName] = reader.ReadRawValue() };

	internal override void Apply(TDeclaring value, Dictionary<string, string> extensionData)
	{
		if (extensionData.Count == 0)
		{
			return;
		}

		if (this.TryGetWritableDictionary(value, out IDictionary<string, string>? writable))
		{
			foreach (KeyValuePair<string, string> entry in extensionData)
			{
				writable![entry.Key] = entry.Value;
			}

			return;
		}

		if (this.setter is null)
		{
			throw new NotSupportedException($"Extension data property on '{typeof(TDeclaring).FullName}' cannot be assigned.");
		}

		var assigned = (TProperty)(object)extensionData;
		this.setter!(ref value, assigned);
	}

	private IDictionary<string, string> GetOrCreateWritableDictionary(ref TDeclaring value)
	{
		if (this.TryGetWritableDictionary(value, out IDictionary<string, string>? existing))
		{
			return existing!;
		}

		if (this.setter is null)
		{
			throw new NotSupportedException($"Extension data property on '{typeof(TDeclaring).FullName}' must have a setter or return a writable dictionary instance.");
		}

		Dictionary<string, string> created = new(StringComparer.Ordinal);
		this.setter!(ref value, (TProperty)(object)created);
		return created;
	}

	private bool TryGetWritableDictionary(TDeclaring value, out IDictionary<string, string>? dictionary)
	{
		dictionary = null;
		if (this.getter is null)
		{
			return false;
		}

		if (this.getter!(ref value) is IDictionary<string, string> writable)
		{
			dictionary = writable;
			return true;
		}

		return false;
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

	internal abstract bool Write(ref JsonWriter writer, TDeclaring container, SerializationContext context, bool first);

	internal abstract void Read(ref JsonReader reader, ref TDeclaring container, SerializationContext context);
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

	internal override bool Write(ref JsonWriter writer, TDeclaring container, SerializationContext context, bool first)
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

	internal override void Read(ref JsonReader reader, ref TDeclaring container, SerializationContext context)
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
