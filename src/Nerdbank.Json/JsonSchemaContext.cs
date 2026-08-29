// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1204 // Static ordering relaxed for this schema orchestration type.
#pragma warning disable SA1600 // Internal schema orchestration members are intentionally undocumented in this file.

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nerdbank.Json;

/// <summary>
/// Drives generation of a JSON Schema document, resolving nested schemas, shared <c>$defs</c> definitions, and
/// recursive types. Provided to <see cref="JsonConverter.GetJsonSchema(JsonSchemaContext, ITypeShape)"/> so custom
/// converters can describe nested types consistently.
/// </summary>
public sealed class JsonSchemaContext : ITypeShapeFunc
{
	internal const string Dialect = "https://json-schema.org/draft/2020-12/schema";

	private readonly ConverterCache owner;
	private readonly JsonSchemaVisitor visitor;
	private readonly Dictionary<Type, string> references = new();
	private readonly Dictionary<string, JsonSchema> definitions = new(StringComparer.Ordinal);
	private readonly Dictionary<Type, string> definitionNames = new();
	private readonly HashSet<string> usedNames = new(StringComparer.Ordinal);
	private readonly HashSet<Type> recursionGuard = new();

	internal JsonSchemaContext(ConverterCache owner)
	{
		this.owner = owner;
		this.visitor = new JsonSchemaVisitor(owner, this);
	}

	internal ConverterCache Owner => this.owner;

	/// <summary>
	/// Gets the schema for a type shape, resolving shared definitions and recursion.
	/// </summary>
	/// <param name="typeShape">The shape of the type.</param>
	/// <returns>The schema, which may be a <c>$ref</c> to a shared definition.</returns>
	public JsonSchema GetSchema(ITypeShape typeShape)
	{
		Requires.NotNull(typeShape);
		Type type = typeShape.Type;

		if (this.references.TryGetValue(type, out string? existing))
		{
			return Reference(existing);
		}

		if (JsonSchemaScalars.TryGetScalarSchema(type, out JsonSchema scalar))
		{
			return scalar;
		}

		string defName = this.GetDefinitionName(type);
		string refPath = "#/$defs/" + defName;
		if (!this.recursionGuard.Add(type))
		{
			this.references[type] = refPath;
			return Reference(refPath);
		}

		JsonSchema schema = (JsonSchema)typeShape.Invoke(this)!;
		schema = this.ApplyReferencePreservation(type, schema);
		this.recursionGuard.Remove(type);

		if (this.references.ContainsKey(type))
		{
			this.definitions[defName] = schema;
			return Reference(refPath);
		}

		return schema;
	}

	object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
	{
		if (this.owner.TryGetCustomConverter(typeShape, out JsonConverter? custom) && custom is not null)
		{
			return custom.GetJsonSchema(this, typeShape) ?? CreateUndocumented(custom);
		}

		if (this.owner.UnionConfiguration.TryGetEntry(typeof(T), out object? entry) && entry is not null)
		{
			return this.BuildRuntimeUnionSchema(typeShape, entry);
		}

		return typeShape.Accept(this.visitor)!;
	}

	internal static JsonSchema Reference(string path) => new JsonSchema().Set("$ref", path);

	internal JsonSchema GenerateDocument(ITypeShape rootShape)
	{
		JsonSchema body = this.GetSchema(rootShape);
		JsonSchema document = new JsonSchema().Set("$schema", Dialect);
		document.Append(body);

		if (this.definitions.Count > 0)
		{
			List<KeyValuePair<string, JsonSchema>> sorted = this.definitions
				.OrderBy(pair => pair.Key, StringComparer.Ordinal)
				.ToList();
			document.SetMap("$defs", sorted);
		}

		return document;
	}

	internal JsonSchema CreateUnionEnvelope(IReadOnlyList<(object? Alias, JsonSchema CaseSchema)> cases, JsonSchema? baseSchema)
	{
		List<JsonSchema> options = new(cases.Count + (baseSchema is null ? 0 : 1));
		foreach ((object? alias, JsonSchema caseSchema) in cases)
		{
			JsonSchema discriminator = new();
			if (alias is int tag)
			{
				discriminator.Set("const", tag);
			}
			else
			{
				discriminator.Set("const", (string)alias!);
			}

			options.Add(Tuple(discriminator, caseSchema));
		}

		if (baseSchema is not null)
		{
			options.Add(Tuple(new JsonSchema().Set("type", "null"), baseSchema));
		}

		return new JsonSchema().SetSchemas("oneOf", options);

		static JsonSchema Tuple(JsonSchema first, JsonSchema second) => new JsonSchema()
			.Set("type", "array")
			.SetSchemas("prefixItems", [first, second])
			.Set("minItems", 2L)
			.Set("maxItems", 2L);
	}

	private static JsonSchema CreateUndocumented(JsonConverter converter) => new JsonSchema()
		.Set("$comment", $"No JSON schema is available for the custom converter '{converter.GetType().FullName}'; any JSON value is permitted here.");

	private JsonSchema BuildRuntimeUnionSchema<T>(ITypeShape<T> shape, object entry)
	{
		if (ReferenceEquals(entry, JsonUnionConfiguration.DisabledSentinel))
		{
			ITypeShape baseShape = shape is IUnionTypeShape disabledUnion ? disabledUnion.BaseType : shape;
			return (JsonSchema)baseShape.Accept(this.visitor)!;
		}

		var definition = (JsonUnion<T>)entry;
		List<(object? Alias, JsonSchema CaseSchema)> cases = [];

		if (definition.IncludeGeneratedCases && shape is IUnionTypeShape unionShape)
		{
			foreach (IUnionCaseShape unionCase in unionShape.UnionCases)
			{
				object alias = unionCase.IsTagSpecified ? unionCase.Tag : unionCase.Name;
				cases.Add((alias, this.GetSchema(unionCase.UnionCaseType)));
			}
		}

		foreach (RuntimeUnionCase<T> runtimeCase in definition.Cases)
		{
			object alias = runtimeCase.Tag is int tag ? tag : runtimeCase.Name!;
			cases.Add((alias, this.GetSchema(runtimeCase.GetCaseShape())));
		}

		if (definition.DuckTyping)
		{
			return new JsonSchema().SetSchemas("oneOf", [.. cases.Select(c => c.CaseSchema)]);
		}

		return this.CreateUnionEnvelope(cases, baseSchema: null);
	}

	private JsonSchema ApplyReferencePreservation(Type type, JsonSchema schema)
	{
		if (!this.owner.ShouldPreserveReferences(type))
		{
			return schema;
		}

		JsonSchema referenceForm = new JsonSchema()
			.Set("type", "object")
			.SetMap("properties", [new("$ref", new JsonSchema().Set("type", "integer"))])
			.SetStrings("required", ["$ref"])
			.Set("additionalProperties", false);

		JsonSchema identityForm = new JsonSchema()
			.Set("type", "object")
			.SetMap("properties", [new("$id", new JsonSchema().Set("type", "integer")), new("$value", schema)])
			.SetStrings("required", ["$id", "$value"])
			.Set("additionalProperties", false);

		return new JsonSchema().SetSchemas("oneOf", [referenceForm, identityForm]);
	}

	private string GetDefinitionName(Type type)
	{
		if (this.definitionNames.TryGetValue(type, out string? name))
		{
			return name;
		}

		string baseName = SanitizeName(type);
		string candidate = baseName;
		int suffix = 2;
		while (!this.usedNames.Add(candidate))
		{
			candidate = baseName + suffix++;
		}

		this.definitionNames[type] = candidate;
		return candidate;
	}

	private static string SanitizeName(Type type)
	{
		if (type.IsArray)
		{
			return SanitizeName(type.GetElementType()!) + "Array";
		}

		if (type.IsGenericType)
		{
			string name = type.Name;
			int tick = name.IndexOf('`');
			if (tick >= 0)
			{
				name = name.Substring(0, tick);
			}

			return name + "Of" + string.Join("And", type.GetGenericArguments().Select(SanitizeName));
		}

		StringBuilder builder = new(type.Name.Length);
		foreach (char c in type.Name)
		{
			builder.Append(char.IsLetterOrDigit(c) ? c : '_');
		}

		return builder.ToString();
	}
}
