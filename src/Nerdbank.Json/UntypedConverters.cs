// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Internal converter members are intentionally undocumented in this file.
#pragma warning disable SA1649 // File name should match first type name

using System.Dynamic;

namespace Nerdbank.Json;

/// <summary>
/// Bridges the CLR <see cref="object"/> type to the native, dependency-free <see cref="JsonValue"/> document object
/// model without any reflection.
/// </summary>
/// <remarks>
/// <para>
/// Deserialization always produces a <see cref="JsonValue"/> (boxed as <see cref="object"/>), giving exact JSON fidelity
/// with no runtime type discovery.
/// </para>
/// <para>
/// Serialization accepts a <see cref="JsonValue"/> or a boxed JSON primitive (<see cref="bool"/>, <see cref="string"/>,
/// the built-in numeric types, or <see langword="null"/>). Any other CLR type throws <see cref="NotSupportedException"/>
/// rather than silently discovering arbitrary object graphs via reflection.
/// </para>
/// </remarks>
internal sealed class ObjectConverter : JsonConverter<object>
{
	internal static readonly ObjectConverter Instance = new();

	public override bool PreferAsyncSerialization => true;

	public override object? Read(ref JsonReader reader, SerializationContext context)
		=> JsonValueConverter.Instance.Read(ref reader, context);

	public override void Write(ref JsonWriter writer, object? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		if (value is JsonValue jsonValue)
		{
			JsonValueConverter.Instance.Write(ref writer, jsonValue, context);
			return;
		}

		WriteBoxedPrimitive(ref writer, value);
	}

	public override async ValueTask<object?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
		=> await JsonValueConverter.Instance.ReadAsync(reader, context).ConfigureAwait(false);

	public override async ValueTask WriteAsync(JsonAsyncWriter writer, object? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is JsonValue jsonValue)
		{
			await JsonValueConverter.Instance.WriteAsync(writer, jsonValue, context).ConfigureAwait(false);
			return;
		}

		JsonWriter syncWriter = writer.CreateWriter();
		try
		{
			this.Write(ref syncWriter, value, context);
		}
		finally
		{
			writer.ReturnWriter(ref syncWriter);
		}

		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new();

	internal static void WriteBoxedPrimitive(ref JsonWriter writer, object value)
	{
		switch (value)
		{
			case bool b:
				writer.WriteBooleanValue(b);
				break;
			case string s:
				writer.WriteStringValue(s);
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
			default:
				throw new NotSupportedException(
					$"Cannot serialize a value of runtime type '{value.GetType().FullName}' through the untyped 'object' converter. " +
					"Supply a JsonValue, a boxed JSON primitive (bool, string, or a numeric type), or serialize the value through its own generated type shape.");
		}
	}
}

/// <summary>
/// Reads and writes <see cref="ExpandoObject"/> instances as JSON objects whose member values are represented with the
/// native <see cref="JsonValue"/> model.
/// </summary>
/// <remarks>
/// Reconstructing an <see cref="ExpandoObject"/> from JSON is an O(n²) operation because <see cref="ExpandoObject"/>
/// stores members in a manner that requires a linear scan for each insertion. Prefer <see cref="JsonObject"/> for large
/// payloads. The configured <see cref="SecuritySettings.MaxObjectMemberCount"/> bounds the worst case.
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
		int maxMembers = context.Security.MaxObjectMemberCount;
		PropertyCollisionDetection collision = new(StringComparer.Ordinal);
		ExpandoObject result = new();
		IDictionary<string, object?> members = result;
		reader.ReadStartObject();
		if (!reader.TryReadEndObject())
		{
			int count = 0;
			while (true)
			{
				string name = reader.ReadRequiredString();
				collision.MarkAsRead(name);
				reader.ReadNameSeparator();
				if (count >= maxMembers)
				{
					throw new FormatException($"The JSON object exceeds the configured maximum of {maxMembers} members.");
				}

				members[name] = JsonValueConverter.Instance.Read(ref reader, context);
				count++;
				if (reader.TryReadEndObject())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		return result;
	}

	public override void Write(ref JsonWriter writer, ExpandoObject? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();
		writer.WriteStartObject();
		bool first = true;
		foreach (KeyValuePair<string, object?> member in (IDictionary<string, object?>)value)
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
