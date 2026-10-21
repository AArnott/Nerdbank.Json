// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Internal converter members are intentionally undocumented.
#pragma warning disable SA1649 // File name should match first type name

using System.Collections.ObjectModel;
using System.Dynamic;
using System.Globalization;

namespace Nerdbank.Json;

internal sealed class ObjectConverter(bool dynamicObjects = false) : JsonConverter<object>
{
	internal static readonly ObjectConverter Instance = new();
	internal static readonly ObjectConverter DynamicInstance = new(dynamicObjects: true);

	public override bool PreferAsyncSerialization => true;

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
						items.Add(this.Read(ref reader, context));
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

						members.Add(name, this.Read(ref reader, context));
						if (reader.TryReadEndObject())
						{
							break;
						}

						reader.ReadValueSeparator();
					}
					while (true);
				}

				return dynamicObjects ? new DynamicJsonDictionary(members) : new ReadOnlyDictionary<object, object?>(members);
			default:
				string token = reader.ReadNumberToken();
				if (token.IndexOfAny(['.', 'e', 'E']) < 0)
				{
					return token[0] == '-'
						? (object)long.Parse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
						: ulong.Parse(token, NumberStyles.None, CultureInfo.InvariantCulture);
				}

				double number = double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
				if (double.IsInfinity(number) || double.IsNaN(number))
				{
					throw new FormatException("The JSON number is outside the range of a finite Double. Use JsonValue for exact number fidelity.");
				}

				return number;
		}
	}

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
					this.Write(ref writer, entry.Value, context);
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

					this.Write(ref writer, array[i], context);
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

					context.GetConverter(value.GetType()).WriteObject(ref writer, value, context);
				}

				break;
		}
	}

	public override ValueTask WriteAsync(JsonAsyncWriter writer, object? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is not null and not JsonValue and not IReadOnlyDictionary<object, object?> and not object[] &&
			value.GetType() != typeof(object) && !BuiltInJsonConverters.IsSupported(value.GetType()))
		{
			return context.GetConverter(value.GetType()).WriteObjectAsync(writer, value, context);
		}

		return base.WriteAsync(writer, value, context);
	}

	public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new();

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

internal sealed class DynamicJsonDictionary(IDictionary<object, object?> content) : DynamicObject, IReadOnlyDictionary<object, object?>
{
	public int Count => content.Count;

	public IEnumerable<object> Keys => content.Keys;

	public IEnumerable<object?> Values => content.Values;

	public object? this[object key] => content[key];

	public override bool TryGetMember(GetMemberBinder binder, out object? result) => content.TryGetValue(binder.Name, out result);

	public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
	{
		if (indexes.Length == 1)
		{
			return content.TryGetValue(indexes[0], out result);
		}

		result = null;
		return false;
	}

	public override IEnumerable<string> GetDynamicMemberNames() => content.Keys.Cast<string>();

	public bool ContainsKey(object key) => content.ContainsKey(key);

	public bool TryGetValue(object key, out object? value) => content.TryGetValue(key, out value);

	public IEnumerator<KeyValuePair<object, object?>> GetEnumerator() => content.GetEnumerator();

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
	internal static readonly ExpandoObjectConverter Instance = new();

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

				members.Add(name, ObjectConverter.DynamicInstance.Read(ref reader, context));
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
			ObjectConverter.Instance.Write(ref writer, member.Value, context);
		}

		writer.WriteEndObject();
	}

	public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new();
}

internal sealed class JsonDomConverter<T> : JsonConverter<T>
	where T : JsonValue
{
	public override bool PreferAsyncSerialization => true;

	public override void Write(ref JsonWriter writer, in T? value, SerializationContext context) => JsonValueConverter.Instance.Write(ref writer, value, context);

	public override T? Read(ref JsonReader reader, SerializationContext context) => Cast(JsonValueConverter.Instance.Read(ref reader, context));

	public override ValueTask WriteAsync(JsonAsyncWriter writer, T? value, SerializationContext context) => JsonValueConverter.Instance.WriteAsync(writer, value, context);

	public override async ValueTask<T?> ReadAsync(JsonAsyncReader reader, SerializationContext context) => Cast(await JsonValueConverter.Instance.ReadAsync(reader, context).ConfigureAwait(false));

	public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new();

	private static T? Cast(JsonValue? value) => value is T result ? result : value is null or JsonNull ? null : throw new FormatException($"The JSON value is not a {typeof(T).Name}.");
}
