// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;
using JsonSchemaValue = System.Text.Json.Nodes.JsonValue;

namespace Nerdbank.Json;

/// <content>JSON Schema helpers for custom converters.</content>
public abstract partial class JsonConverter<T>
{
	/// <summary>Returns a schema that also accepts <see langword="null"/>.</summary>
	/// <param name="schema">The non-null schema fragment.</param>
	/// <returns>The adjusted schema fragment.</returns>
	protected internal static JsonSchemaObject ApplyJsonSchemaNullability(JsonSchemaObject schema)
	{
		Requires.NotNull(schema);
		return schema.MakeNullable();
	}

	/// <summary>Creates a permissive schema for a converter with an undocumented representation.</summary>
	/// <param name="undocumentingConverter">The converter type.</param>
	/// <returns>A schema allowing any JSON value, annotated with the responsible converter.</returns>
	protected internal static JsonSchemaObject CreateUndocumentedSchema(Type undocumentingConverter)
	{
		Requires.NotNull(undocumentingConverter);
		return new JsonSchemaObject().Set("$comment", $"No JSON schema is available for the custom converter '{undocumentingConverter.FullName}'; any JSON value is permitted here.");
	}

	/// <summary>Wraps a boxed primitive as a JSON value without reflection-based serialization.</summary>
	/// <param name="value">The boxed primitive, or <see langword="null"/>.</param>
	/// <returns>The JSON value, or <see langword="null"/> if the input is null.</returns>
	/// <exception cref="NotSupportedException">The value is not a supported primitive.</exception>
	[return: NotNullIfNotNull(nameof(value))]
	protected static JsonSchemaValue? CreateJsonValue(object? value) => value switch
	{
		null => null,
		string v => JsonSchemaValue.Create(v),
		short v => JsonSchemaValue.Create(v),
		int v => JsonSchemaValue.Create(v),
		long v => JsonSchemaValue.Create(v),
		float v => JsonSchemaValue.Create(v),
		double v => JsonSchemaValue.Create(v),
		decimal v => JsonSchemaValue.Create(v),
		bool v => JsonSchemaValue.Create(v),
		byte v => JsonSchemaValue.Create(v),
		sbyte v => JsonSchemaValue.Create(v),
		ushort v => JsonSchemaValue.Create(v),
		uint v => JsonSchemaValue.Create(v),
		ulong v => JsonSchemaValue.Create(v),
		char v => JsonSchemaValue.Create(v),
		_ => throw new NotSupportedException($"Unsupported object type: {value.GetType().FullName}"),
	};
}
