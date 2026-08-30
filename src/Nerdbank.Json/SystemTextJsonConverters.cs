// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Internal converter members are intentionally undocumented in this file.
#pragma warning disable SA1649 // File name should match first type name

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nerdbank.Json;

/// <summary>
/// Shared options for the System.Text.Json interop converters.
/// </summary>
internal static class SystemTextJsonInterop
{
	internal static readonly JsonDocumentOptions DocumentOptions = new()
	{
		CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};
}

/// <summary>
/// Bridges <see cref="JsonElement"/> to the wire by round-tripping through raw JSON text.
/// </summary>
internal sealed class SystemTextJsonElementConverter : JsonConverter<JsonElement>
{
	internal static readonly SystemTextJsonElementConverter Instance = new();

	public override JsonElement Read(ref JsonReader reader, SerializationContext context)
	{
		using JsonDocument document = JsonDocument.Parse(reader.ReadRawValue(), SystemTextJsonInterop.DocumentOptions);
		return document.RootElement.Clone();
	}

	public override void Write(ref JsonWriter writer, JsonElement value, SerializationContext context)
	{
		if (value.ValueKind == System.Text.Json.JsonValueKind.Undefined)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteRawValue(value.GetRawText());
	}

	public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new();
}

/// <summary>
/// Bridges the mutable <see cref="JsonNode"/> object model to the wire by round-tripping through raw JSON text.
/// </summary>
internal sealed class SystemTextJsonNodeConverter : JsonConverter<JsonNode>
{
	internal static readonly SystemTextJsonNodeConverter Instance = new();

	public override JsonNode? Read(ref JsonReader reader, SerializationContext context)
		=> JsonNode.Parse(reader.ReadRawValue(), nodeOptions: null, SystemTextJsonInterop.DocumentOptions);

	public override void Write(ref JsonWriter writer, JsonNode? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteRawValue(value.ToJsonString());
	}

	public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new();
}

/// <summary>
/// Bridges <see cref="JsonDocument"/> to the wire by round-tripping through raw JSON text.
/// </summary>
internal sealed class SystemTextJsonDocumentConverter : JsonConverter<JsonDocument>
{
	internal static readonly SystemTextJsonDocumentConverter Instance = new();

	public override JsonDocument? Read(ref JsonReader reader, SerializationContext context)
		=> JsonDocument.Parse(reader.ReadRawValue(), SystemTextJsonInterop.DocumentOptions);

	public override void Write(ref JsonWriter writer, JsonDocument? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteRawValue(value.RootElement.GetRawText());
	}

	public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new();
}
