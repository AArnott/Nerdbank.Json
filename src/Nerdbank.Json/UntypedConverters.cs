// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using JsonSchemaArray = System.Text.Json.Nodes.JsonArray;
using JsonSchemaNode = System.Text.Json.Nodes.JsonNode;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Internal converter members are intentionally undocumented.
#pragma warning disable SA1649 // File name should match first type name

using System.Collections.ObjectModel;
using System.Dynamic;
using System.Globalization;

namespace Nerdbank.Json;

/// <summary>Converts untyped values to primitives, dictionaries and arrays using contextual runtime shapes when writing.</summary>
/// <param name="dynamicObjects">Whether deserialized dictionaries support dynamic member access.</param>
internal sealed class ObjectConverter(bool dynamicObjects = false) : JsonConverter<object>
{
	/// <summary>The ordinary object converter.</summary>
	internal static readonly ObjectConverter Instance = new();

	/// <summary>The object converter with read-only dynamic dictionaries.</summary>
	internal static readonly ObjectConverter DynamicInstance = new(dynamicObjects: true);

	/// <inheritdoc/>
	public override bool PreferAsyncSerialization => true;

	/// <inheritdoc/>
	public override object? Read(ref JsonReader reader, SerializationContext context)
	{
		context.CancellationToken.ThrowIfCancellationRequested();
		switch (reader.PeekValueToken())
		{
			case 'n':
				if (!reader.TryReadNull())
				{
					throw new FormatException("Expected a JSON null literal.");
				}

				return null;
			case '"':
				return reader.ReadRequiredString(context.StringInterningCache);
			case 't':
			case 'f':
				return reader.ReadBoolean();
			case '[':
				context.DepthStep();
				List<object?> items = [];
				reader.ReadStartArray();
				if (!reader.TryReadEndArray())
				{
					do
					{
						items.Add(this.ReadNestedValue(ref reader, context));
						if (reader.TryReadEndArray())
						{
							break;
						}

						reader.ReadValueSeparator();
					}
					while (true);
				}

				return items.ToArray();
			case '{':
				context.DepthStep();
				Dictionary<object, object?> members = [];
				object result = dynamicObjects ? new DynamicJsonDictionary(members) : new ReadOnlyDictionary<object, object?>(members);
				context.ReportObjectConstructed(result);
				reader.ReadStartObject();
				if (!reader.TryReadEndObject())
				{
					do
					{
						string name = reader.ReadRequiredString(context.StringInterningCache);
						reader.ReadNameSeparator();
						if (members.Count >= context.Security.MaxObjectMemberCount)
						{
							throw new FormatException($"The JSON object exceeds the configured maximum of {context.Security.MaxObjectMemberCount} members.");
						}

						if (members.ContainsKey(name))
						{
							throw new FormatException($"The JSON object contains duplicate property '{name}'.");
						}

						members.Add(name, this.ReadNestedValue(ref reader, context));
						if (reader.TryReadEndObject())
						{
							break;
						}

						reader.ReadValueSeparator();
					}
					while (true);
				}

				return result;
			default:
				string token = reader.ReadNumberToken();
				if (token.IndexOfAny(['.', 'e', 'E']) < 0)
				{
					if (token[0] == '-')
					{
						long signed = long.Parse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
						return signed < 0 ? (object)signed : (ulong)signed;
					}

					return ulong.Parse(token, NumberStyles.None, CultureInfo.InvariantCulture);
				}

				double number = double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
				if (double.IsInfinity(number) || double.IsNaN(number))
				{
					throw new FormatException("The JSON number is outside the range of a finite Double. Use JsonValue for exact number fidelity.");
				}

				return number;
		}
	}

	/// <inheritdoc/>
	public override void Write(ref JsonWriter writer, in object? value, SerializationContext context)
	{
		context.CancellationToken.ThrowIfCancellationRequested();
		switch (value)
		{
			case null:
				writer.WriteNullValue();
				break;
			case JsonValue json:
				JsonValueConverter.Instance.Write(ref writer, json, context);
				break;
			case IReadOnlyDictionary<object, object?> map:
				context.DepthStep();
				writer.WriteStartObject();
				bool first = true;
				foreach (KeyValuePair<object, object?> entry in map)
				{
					if (entry.Key is not string key)
					{
						throw new NotSupportedException("Untyped JSON dictionaries require string keys.");
					}

					if (!first)
					{
						writer.WriteValueSeparator();
					}

					first = false;
					writer.WritePropertyName(key);
					this.WriteNestedValue(ref writer, entry.Value, context);
				}

				writer.WriteEndObject();
				break;
			case object?[] array:
				context.DepthStep();
				writer.WriteStartArray();
				for (int i = 0; i < array.Length; i++)
				{
					if (i > 0)
					{
						writer.WriteValueSeparator();
					}

					this.WriteNestedValue(ref writer, array[i], context);
				}

				writer.WriteEndArray();
				break;
			default:
				if (!TryWriteBoxedPrimitive(ref writer, value))
				{
					if (value.GetType() == typeof(object))
					{
						throw new NotSupportedException("A plain System.Object has no JSON representation.");
					}

					GetRuntimeConverter(value, context).WriteObject(ref writer, value, context);
				}

				break;
		}
	}

	/// <inheritdoc/>
	public override ValueTask WriteAsync(JsonAsyncWriter writer, object? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is not null and not JsonValue and not IReadOnlyDictionary<object, object?> and not object[] &&
			value.GetType() != typeof(object) && !BuiltInJsonConverters.IsSupported(value.GetType()))
		{
			return GetRuntimeConverter(value, context).WriteObjectAsync(writer, value, context);
		}

		return base.WriteAsync(writer, value, context);
	}

	/// <inheritdoc/>
	public override JsonSchemaObject? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new();

	/// <summary>Reads a nested untyped value, including a reference envelope when preservation is enabled.</summary>
	/// <param name="reader">The JSON reader.</param>
	/// <param name="context">The operation context.</param>
	/// <returns>The nested value.</returns>
	internal object? ReadNestedValue(ref JsonReader reader, SerializationContext context)
	{
		if (context.ReferenceTracker is not null && reader.PeekValueToken() == '{')
		{
			JsonReader peek = reader;
			peek.ReadStartObject();
			if (!peek.TryReadEndObject() && peek.ReadRequiredString() is "$id" or "$ref")
			{
				return context.ReferenceTracker.ReadObject(ref reader, this, context);
			}
		}

		return this.Read(ref reader, context with { ReferenceId = 0 });
	}

	/// <summary>Writes a nested untyped reference value using the operation's reference tracker.</summary>
	/// <param name="writer">The JSON writer.</param>
	/// <param name="value">The nested value.</param>
	/// <param name="context">The operation context.</param>
	internal void WriteNestedValue(ref JsonWriter writer, object? value, SerializationContext context)
	{
		if (value is not null and not string && !value.GetType().IsValueType && context.ReferenceTracker is not null)
		{
			context.ReferenceTracker.WriteObject(ref writer, value, this, context);
		}
		else
		{
			this.Write(ref writer, value, context with { ReferenceId = 0 });
		}
	}

	private static JsonConverter GetRuntimeConverter(object value, SerializationContext context)
	{
		JsonConverter converter = context.GetConverter(value.GetType());
		return context.ReferenceTracker?.IsWritingObject(value, context.ReferenceId) is true ? converter.UnwrapReferencePreservation() : converter;
	}

	private static bool TryWriteBoxedPrimitive(ref JsonWriter writer, object value)
	{
		switch (value)
		{
			case bool b:
				writer.WriteBooleanValue(b);
				break;
			case string s:
				writer.WriteStringValue(s);
				break;
			case char c:
				writer.WriteStringValue(c.ToString());
				break;
			case byte n:
				writer.WriteNumberValue(n);
				break;
			case sbyte n:
				writer.WriteNumberValue(n);
				break;
			case short n:
				writer.WriteNumberValue(n);
				break;
			case ushort n:
				writer.WriteNumberValue(n);
				break;
			case int n:
				writer.WriteNumberValue(n);
				break;
			case uint n:
				writer.WriteNumberValue(n);
				break;
			case long n:
				writer.WriteNumberValue(n);
				break;
			case ulong n:
				writer.WriteNumberValue(n);
				break;
			case float n:
				writer.WriteNumberValue(n);
				break;
			case double n:
				writer.WriteNumberValue(n);
				break;
			case decimal n:
				writer.WriteNumberValue(n);
				break;
			case DateTime dateTime:
				return BuiltInJsonConverters.TrySerialize(ref writer, dateTime);
			case DateTimeOffset dateTimeOffset:
				return BuiltInJsonConverters.TrySerialize(ref writer, dateTimeOffset);
			case TimeSpan timeSpan:
				return BuiltInJsonConverters.TrySerialize(ref writer, timeSpan);
			case Guid guid:
				return BuiltInJsonConverters.TrySerialize(ref writer, guid);
			case System.Numerics.BigInteger bigInteger:
				return BuiltInJsonConverters.TrySerialize(ref writer, bigInteger);
			case byte[] bytes:
				return BuiltInJsonConverters.TrySerialize(ref writer, bytes);
			default:
				return false;
		}

		return true;
	}
}

/// <summary>A read-only dictionary whose string keys are also available through dynamic member access.</summary>
/// <param name="content">The dictionary contents.</param>
internal sealed class DynamicJsonDictionary(IDictionary<object, object?> content) : DynamicObject, IReadOnlyDictionary<object, object?>
{
	/// <inheritdoc/>
	public int Count => content.Count;

	/// <inheritdoc/>
	public IEnumerable<object> Keys => content.Keys;

	/// <inheritdoc/>
	public IEnumerable<object?> Values => content.Values;

	/// <inheritdoc/>
	public object? this[object key] => content[key];

	/// <inheritdoc/>
	public override bool TryGetMember(GetMemberBinder binder, out object? result) => content.TryGetValue(binder.Name, out result);

	/// <inheritdoc/>
	public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
	{
		if (indexes.Length == 1)
		{
			return content.TryGetValue(indexes[0], out result);
		}

		result = null;
		return false;
	}

	/// <inheritdoc/>
	public override IEnumerable<string> GetDynamicMemberNames() => content.Keys.Cast<string>();

	/// <inheritdoc/>
	public bool ContainsKey(object key) => content.ContainsKey(key);

	/// <inheritdoc/>
	public bool TryGetValue(object key, out object? value) => content.TryGetValue(key, out value);

	/// <inheritdoc/>
	public IEnumerator<KeyValuePair<object, object?>> GetEnumerator() => content.GetEnumerator();

	/// <inheritdoc/>
	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
}

/// <summary>
/// Reads and writes <see cref="ExpandoObject"/> instances with dynamically accessible nested objects.
/// </summary>
/// <remarks>
/// Reconstructing an <see cref="ExpandoObject"/> requires O(n²) member insertion work.
/// The configured <see cref="SecuritySettings.ExpandoObjectMaxPropertyCount"/> bounds the worst case.
/// Prefer <see cref="JsonObject"/> for large payloads.
/// </remarks>
internal sealed class ExpandoObjectConverter : JsonConverter<ExpandoObject>
{
	/// <summary>The reusable expando converter.</summary>
	internal static readonly ExpandoObjectConverter Instance = new();

	/// <inheritdoc/>
	public override ExpandoObject? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return null;
		}

		context.DepthStep();
		int maxMembers = Math.Min(context.Security.ExpandoObjectMaxPropertyCount, context.Security.MaxObjectMemberCount);
		PropertyCollisionDetection collision = new(StringComparer.Ordinal);
		ExpandoObject result = new();
		context.ReportObjectConstructed(result);
		IDictionary<string, object?> members = result;
		reader.ReadStartObject();
		if (!reader.TryReadEndObject())
		{
			do
			{
				string name = reader.ReadRequiredString(context.StringInterningCache);
				collision.MarkAsRead(name);
				reader.ReadNameSeparator();
				if (members.Count >= maxMembers)
				{
					throw new FormatException($"The JSON object exceeds the configured maximum of {maxMembers} members.");
				}

				members.Add(name, ObjectConverter.DynamicInstance.ReadNestedValue(ref reader, context));
				if (reader.TryReadEndObject())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
			while (true);
		}

		return result;
	}

	/// <inheritdoc/>
	public override void Write(ref JsonWriter writer, in ExpandoObject? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();
		IDictionary<string, object?> members = value;
		int maxMembers = Math.Min(context.Security.ExpandoObjectMaxPropertyCount, context.Security.MaxObjectMemberCount);
		if (members.Count > maxMembers)
		{
			throw new FormatException($"The ExpandoObject exceeds the configured maximum of {maxMembers} properties.");
		}

		writer.WriteStartObject();
		bool first = true;
		foreach (KeyValuePair<string, object?> member in members)
		{
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			writer.WritePropertyName(member.Key);
			ObjectConverter.Instance.WriteNestedValue(ref writer, member.Value, context);
		}

		writer.WriteEndObject();
	}

	/// <inheritdoc/>
	public override JsonSchemaObject? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new();
}

/// <summary>Adapts the native DOM converter to a specific concrete node type.</summary>
/// <typeparam name="T">The node type.</typeparam>
internal sealed class JsonDomConverter<T> : JsonConverter<T>
	where T : JsonValue
{
	/// <inheritdoc/>
	public override bool PreferAsyncSerialization => true;

	/// <inheritdoc/>
	public override void Write(ref JsonWriter writer, in T? value, SerializationContext context) => JsonValueConverter.Instance.Write(ref writer, value, context);

	/// <inheritdoc/>
	public override T? Read(ref JsonReader reader, SerializationContext context) => Cast(JsonValueConverter.Instance.Read(ref reader, context));

	/// <inheritdoc/>
	public override ValueTask WriteAsync(JsonAsyncWriter writer, T? value, SerializationContext context) => JsonValueConverter.Instance.WriteAsync(writer, value, context);

	/// <inheritdoc/>
	public override async ValueTask<T?> ReadAsync(JsonAsyncReader reader, SerializationContext context) => Cast(await JsonValueConverter.Instance.ReadAsync(reader, context).ConfigureAwait(false));

	/// <inheritdoc/>
	public override JsonSchemaObject? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new();

	private static T? Cast(JsonValue? value) => value is T result ? result : value is null or JsonNull ? null : throw new FormatException($"The JSON value is not a {typeof(T).Name}.");
}
