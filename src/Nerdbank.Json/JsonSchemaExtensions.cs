// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal schema construction helpers.

using JsonSchemaArray = System.Text.Json.Nodes.JsonArray;
using JsonSchemaNode = System.Text.Json.Nodes.JsonNode;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;

namespace Nerdbank.Json;

/// <summary>
/// Internal helpers for constructing schema fragments.
/// </summary>
internal static class JsonSchemaExtensions
{
	internal static JsonSchemaObject Set(this JsonSchemaObject schema, string keyword, string value) => SetNode(schema, keyword, System.Text.Json.Nodes.JsonValue.Create(value));

	internal static JsonSchemaObject Set(this JsonSchemaObject schema, string keyword, bool value) => SetNode(schema, keyword, System.Text.Json.Nodes.JsonValue.Create(value));

	internal static JsonSchemaObject Set(this JsonSchemaObject schema, string keyword, long value) => SetNode(schema, keyword, System.Text.Json.Nodes.JsonValue.Create(value));

	internal static JsonSchemaObject Set(this JsonSchemaObject schema, string keyword, double value) => SetNode(schema, keyword, System.Text.Json.Nodes.JsonValue.Create(value));

	internal static JsonSchemaObject Set(this JsonSchemaObject schema, string keyword, JsonSchemaObject value) => SetNode(schema, keyword, value);

	internal static JsonSchemaObject SetSchemas(this JsonSchemaObject schema, string keyword, IReadOnlyList<JsonSchemaObject> values)
	{
		JsonSchemaArray array = new();
		foreach (JsonSchemaObject value in values)
		{
			array.Add(value.DeepClone());
		}

		return SetNode(schema, keyword, array);
	}

	internal static JsonSchemaObject SetStrings(this JsonSchemaObject schema, string keyword, IReadOnlyList<string> values)
	{
		JsonSchemaArray array = new();
		foreach (string value in values)
		{
			array.Add((JsonSchemaNode)value);
		}

		return SetNode(schema, keyword, array);
	}

	internal static JsonSchemaObject SetIntegers(this JsonSchemaObject schema, string keyword, IReadOnlyList<long> values)
	{
		JsonSchemaArray array = new();
		foreach (long value in values)
		{
			array.Add((JsonSchemaNode)value);
		}

		return SetNode(schema, keyword, array);
	}

	internal static JsonSchemaObject SetMap(this JsonSchemaObject schema, string keyword, IReadOnlyList<KeyValuePair<string, JsonSchemaObject>> values)
	{
		JsonSchemaObject map = new();
		foreach (KeyValuePair<string, JsonSchemaObject> value in values)
		{
			map[value.Key] = value.Value.DeepClone();
		}

		return SetNode(schema, keyword, map);
	}

	internal static bool TryGetString(this JsonSchemaObject schema, string keyword, out string? value)
	{
		if (schema[keyword] is System.Text.Json.Nodes.JsonValue node && node.TryGetValue(out string? text))
		{
			value = text;
			return true;
		}

		value = null;
		return false;
	}

	internal static JsonSchemaObject Append(this JsonSchemaObject schema, JsonSchemaObject other)
	{
		foreach (KeyValuePair<string, JsonSchemaNode?> property in other)
		{
			schema[property.Key] = property.Value?.DeepClone();
		}

		return schema;
	}

	internal static JsonSchemaObject MakeNullable(this JsonSchemaObject schema)
	{
		if (schema.Count == 0)
		{
			return schema;
		}

		if (schema["$ref"] is not null || schema["oneOf"] is not null || schema["anyOf"] is not null || schema["allOf"] is not null || schema["enum"] is not null || schema["const"] is not null)
		{
			return new JsonSchemaObject().SetSchemas("anyOf", [schema, new JsonSchemaObject().Set("type", "null")]);
		}

		JsonSchemaObject result = (JsonSchemaObject)schema.DeepClone();
		if (result["type"] is JsonSchemaArray types)
		{
			foreach (JsonSchemaNode? type in types)
			{
				if (type?.GetValue<string>() == "null")
				{
					return result;
				}
			}

			types.Add((JsonSchemaNode)"null");
		}
		else if (result.TryGetString("type", out string? type) && type != "null")
		{
			result["type"] = new JsonSchemaArray(type, "null");
		}

		return result;
	}

	private static JsonSchemaObject SetNode(JsonSchemaObject schema, string keyword, JsonSchemaNode? value)
	{
		schema[keyword] = value?.DeepClone();
		return schema;
	}
}
