// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Nerdbank.Json;

/// <summary>
/// A minimal, ordered JSON object model used to build JSON Schema documents without rooting
/// <c>System.Text.Json</c>.
/// </summary>
/// <remarks>
/// <para>
/// An empty schema (no keywords) matches any JSON value. Use the <see cref="Set(string, string)"/> family of
/// methods to add schema keywords, and <see cref="ToString"/> or <see cref="JsonSerializer"/> integration to
/// serialize. Keyword order is preserved for deterministic output.
/// </para>
/// </remarks>
public sealed class JsonSchema
{
	private readonly List<KeyValuePair<string, object?>> members = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonSchema"/> class that matches any value.
	/// </summary>
	public JsonSchema()
	{
	}

	/// <summary>Sets a string-valued keyword.</summary>
	/// <param name="keyword">The keyword name.</param>
	/// <param name="value">The value.</param>
	/// <returns>This instance.</returns>
	public JsonSchema Set(string keyword, string value) => this.Add(keyword, value);

	/// <summary>Sets a boolean-valued keyword.</summary>
	/// <param name="keyword">The keyword name.</param>
	/// <param name="value">The value.</param>
	/// <returns>This instance.</returns>
	public JsonSchema Set(string keyword, bool value) => this.Add(keyword, value);

	/// <summary>Sets an integer-valued keyword.</summary>
	/// <param name="keyword">The keyword name.</param>
	/// <param name="value">The value.</param>
	/// <returns>This instance.</returns>
	public JsonSchema Set(string keyword, long value) => this.Add(keyword, value);

	/// <summary>Sets a floating-point keyword.</summary>
	/// <param name="keyword">The keyword name.</param>
	/// <param name="value">The value.</param>
	/// <returns>This instance.</returns>
	public JsonSchema Set(string keyword, double value) => this.Add(keyword, value);

	/// <summary>Sets a nested-schema keyword (for example <c>items</c>).</summary>
	/// <param name="keyword">The keyword name.</param>
	/// <param name="value">The nested schema.</param>
	/// <returns>This instance.</returns>
	public JsonSchema Set(string keyword, JsonSchema value) => this.Add(keyword, value);

	/// <summary>Sets a keyword whose value is an array of schemas (for example <c>oneOf</c> or <c>prefixItems</c>).</summary>
	/// <param name="keyword">The keyword name.</param>
	/// <param name="value">The schema array.</param>
	/// <returns>This instance.</returns>
	public JsonSchema SetSchemas(string keyword, IReadOnlyList<JsonSchema> value) => this.Add(keyword, value);

	/// <summary>Sets a keyword whose value is an array of strings (for example <c>required</c> or <c>enum</c>).</summary>
	/// <param name="keyword">The keyword name.</param>
	/// <param name="value">The string array.</param>
	/// <returns>This instance.</returns>
	public JsonSchema SetStrings(string keyword, IReadOnlyList<string> value) => this.Add(keyword, value);

	/// <summary>Sets a keyword whose value is an ordered map of names to schemas (for example <c>properties</c>).</summary>
	/// <param name="keyword">The keyword name.</param>
	/// <param name="value">The ordered map.</param>
	/// <returns>This instance.</returns>
	public JsonSchema SetMap(string keyword, IReadOnlyList<KeyValuePair<string, JsonSchema>> value) => this.Add(keyword, value);

	/// <inheritdoc/>
	public override string ToString()
	{
		JsonWriter writer = new(SequencePool<byte>.Shared, new byte[4096]) { WriteIndented = true };
		this.Write(ref writer);
		return writer.FlushAndGetString();
	}

	/// <summary>Attempts to read a string-valued keyword.</summary>
	/// <param name="keyword">The keyword name.</param>
	/// <param name="value">Receives the value if present.</param>
	/// <returns><see langword="true"/> if the keyword exists with a string value.</returns>
	internal bool TryGetString(string keyword, out string? value)
	{
		foreach (KeyValuePair<string, object?> member in this.members)
		{
			if (member.Key == keyword && member.Value is string s)
			{
				value = s;
				return true;
			}
		}

		value = null;
		return false;
	}

	/// <summary>Appends the keywords from another schema onto this one.</summary>
	/// <param name="other">The schema whose keywords are appended.</param>
	/// <returns>This instance.</returns>
	internal JsonSchema Append(JsonSchema other)
	{
		this.members.AddRange(other.members);
		return this;
	}

	/// <summary>Returns a schema equivalent to this one that also permits <see langword="null"/>.</summary>
	/// <returns>A nullable schema.</returns>
	internal JsonSchema MakeNullable()
	{
		for (int i = 0; i < this.members.Count; i++)
		{
			string key = this.members[i].Key;
			if (key is "$ref" or "oneOf" or "anyOf" or "allOf")
			{
				return new JsonSchema().SetSchemas("anyOf", [this, new JsonSchema().Set("type", "null")]);
			}
		}

		for (int i = 0; i < this.members.Count; i++)
		{
			if (this.members[i].Key == "type" && this.members[i].Value is string existingType)
			{
				if (existingType == "null")
				{
					return this;
				}

				JsonSchema copy = new();
				for (int j = 0; j < this.members.Count; j++)
				{
					if (j == i)
					{
						copy.SetStrings("type", [existingType, "null"]);
					}
					else
					{
						copy.members.Add(this.members[j]);
					}
				}

				return copy;
			}
		}

		return new JsonSchema().SetSchemas("anyOf", [this, new JsonSchema().Set("type", "null")]);
	}

	/// <summary>Serializes this schema to the writer.</summary>
	/// <param name="writer">The writer.</param>
	internal void Write(ref JsonWriter writer)
	{
		writer.WriteStartObject();
		bool first = true;
		foreach (KeyValuePair<string, object?> member in this.members)
		{
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			writer.WritePropertyName(member.Key);
			WriteValue(ref writer, member.Value);
		}

		writer.WriteEndObject();
	}

	private static void WriteValue(ref JsonWriter writer, object? value)
	{
		switch (value)
		{
			case null:
				writer.WriteNullValue();
				break;
			case string s:
				writer.WriteStringValue(s);
				break;
			case bool b:
				writer.WriteBooleanValue(b);
				break;
			case long l:
				writer.WriteNumberValue(l);
				break;
			case double d:
				writer.WriteNumberValue(d);
				break;
			case JsonSchema schema:
				schema.Write(ref writer);
				break;
			case IReadOnlyList<JsonSchema> schemas:
				WriteSchemaArray(ref writer, schemas);
				break;
			case IReadOnlyList<string> strings:
				WriteStringArray(ref writer, strings);
				break;
			case IReadOnlyList<KeyValuePair<string, JsonSchema>> map:
				WriteMap(ref writer, map);
				break;
			default:
				throw new NotSupportedException($"Unsupported schema value of type {value.GetType().FullName}.");
		}
	}

	private static void WriteSchemaArray(ref JsonWriter writer, IReadOnlyList<JsonSchema> schemas)
	{
		writer.WriteStartArray();
		for (int i = 0; i < schemas.Count; i++)
		{
			if (i > 0)
			{
				writer.WriteValueSeparator();
			}

			schemas[i].Write(ref writer);
		}

		writer.WriteEndArray();
	}

	private static void WriteStringArray(ref JsonWriter writer, IReadOnlyList<string> strings)
	{
		writer.WriteStartArray();
		for (int i = 0; i < strings.Count; i++)
		{
			if (i > 0)
			{
				writer.WriteValueSeparator();
			}

			writer.WriteStringValue(strings[i]);
		}

		writer.WriteEndArray();
	}

	private static void WriteMap(ref JsonWriter writer, IReadOnlyList<KeyValuePair<string, JsonSchema>> map)
	{
		writer.WriteStartObject();
		for (int i = 0; i < map.Count; i++)
		{
			if (i > 0)
			{
				writer.WriteValueSeparator();
			}

			writer.WritePropertyName(map[i].Key);
			map[i].Value.Write(ref writer);
		}

		writer.WriteEndObject();
	}

	private JsonSchema Add(string keyword, object? value)
	{
		Requires.NotNull(keyword);
		this.members.Add(new(keyword, value));
		return this;
	}
}
