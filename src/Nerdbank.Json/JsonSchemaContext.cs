// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1204 // Static ordering relaxed for this schema orchestration type.
#pragma warning disable SA1202 // Public converter APIs precede internal schema orchestration details.
#pragma warning disable SA1600 // Internal schema orchestration members are intentionally undocumented in this file.

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nerdbank.Json;

/// <summary>
/// Drives generation of a JSON Schema document, resolving nested schemas, shared definitions, and
/// recursive types. Provided to <see cref="JsonConverter.GetJsonSchema(JsonSchemaContext, ITypeShape)"/> so custom
/// converters can describe nested types consistently.
/// </summary>
public sealed class JsonSchemaContext : ITypeShapeFunc
{
	private readonly ConverterCache owner;
	private readonly JsonSchemaVisitor visitor;
	private readonly Dictionary<Type, string> references = new();
	private readonly Dictionary<string, JsonSchema> definitions = new(StringComparer.Ordinal);
	private readonly Dictionary<Type, string> definitionNames = new();
	private readonly HashSet<string> usedNames = new(StringComparer.Ordinal);
	private readonly HashSet<Type> recursionGuard = new();

	internal JsonSchemaContext(ConverterCache owner, JsonSchemaDialect dialect)
	{
		this.owner = owner;
		this.Dialect = dialect;
		this.visitor = new JsonSchemaVisitor(owner, this);
	}

	/// <summary>
	/// Gets the dialect that converter-generated schema fragments must conform to.
	/// </summary>
	public JsonSchemaDialect Dialect { get; }

	internal ConverterCache Owner => this.owner;

	internal string DefinitionsKeyword => this.Dialect switch
	{
		JsonSchemaDialect.Draft4 => "definitions",
		JsonSchemaDialect.Draft2020_12 => "$defs",
		_ => throw new NotSupportedException($"Unsupported JSON Schema dialect: {this.Dialect}."),
	};

	internal string SchemaUri => this.Dialect switch
	{
		JsonSchemaDialect.Draft4 => "http://json-schema.org/draft-04/schema#",
		JsonSchemaDialect.Draft2020_12 => "https://json-schema.org/draft/2020-12/schema",
		_ => throw new NotSupportedException($"Unsupported JSON Schema dialect: {this.Dialect}."),
	};

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

		if (JsonSchemaScalars.TryGetScalarSchema(type, this, out JsonSchema scalar))
		{
			return scalar;
		}

		string defName = this.GetDefinitionName(type);
		string refPath = "#/" + this.DefinitionsKeyword + "/" + defName;
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

	/// <summary>
	/// Creates an array schema with positional constraints using the selected dialect.
	/// </summary>
	/// <param name="items">The schema for each successive array position.</param>
	/// <param name="additionalItemsSchema">The schema for positions after those in <paramref name="items"/>, if permitted.</param>
	/// <returns>An array schema with the specified positional constraints.</returns>
	/// <exception cref="ArgumentException"><paramref name="items"/> is empty.</exception>
	public JsonSchema CreateTupleSchema(IReadOnlyList<JsonSchema> items, JsonSchema? additionalItemsSchema = null)
	{
		JsonSchema schema = new JsonSchema().Set("type", "array");
		this.ApplyTupleSchema(schema, items, additionalItemsSchema);
		return schema;
	}

	/// <summary>
	/// Adds positional array constraints to an existing schema using the selected dialect.
	/// </summary>
	/// <param name="schema">The schema to modify.</param>
	/// <param name="items">The schema for each successive array position.</param>
	/// <param name="additionalItemsSchema">The schema for positions after those in <paramref name="items"/>, if permitted.</param>
	/// <exception cref="ArgumentException"><paramref name="items"/> is empty.</exception>
	public void ApplyTupleSchema(JsonSchema schema, IReadOnlyList<JsonSchema> items, JsonSchema? additionalItemsSchema = null)
	{
		Requires.NotNull(schema);
		Requires.NotNull(items);
		Requires.Argument(items.Count > 0, nameof(items), "At least one positional schema is required.");

		if (this.Dialect == JsonSchemaDialect.Draft4)
		{
			schema.SetSchemas("items", items);
			if (additionalItemsSchema is not null)
			{
				schema.Set("additionalItems", additionalItemsSchema);
			}
		}
		else if (this.Dialect == JsonSchemaDialect.Draft2020_12)
		{
			schema.SetSchemas("prefixItems", items);
			if (additionalItemsSchema is not null)
			{
				schema.Set("items", additionalItemsSchema);
			}
		}
		else
		{
			throw new NotSupportedException($"Unsupported JSON Schema dialect: {this.Dialect}.");
		}
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
		JsonSchema document = new JsonSchema().Set("$schema", this.SchemaUri);
		document.Append(body);

		if (this.definitions.Count > 0)
		{
			List<KeyValuePair<string, JsonSchema>> sorted = this.definitions
				.OrderBy(pair => pair.Key, StringComparer.Ordinal)
				.ToList();
			document.SetMap(this.DefinitionsKeyword, sorted);
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
				if (this.Dialect == JsonSchemaDialect.Draft4)
				{
					discriminator.SetIntegers("enum", [(long)tag]);
				}
				else
				{
					discriminator.Set("const", tag);
				}
			}
			else
			{
				string name = (string)alias!;
				if (this.Dialect == JsonSchemaDialect.Draft4)
				{
					discriminator.SetStrings("enum", [name]);
				}
				else
				{
					discriminator.Set("const", name);
				}
			}

			options.Add(this.CreateTupleSchema([discriminator, caseSchema]).Set("minItems", 2L).Set("maxItems", 2L));
		}

		if (baseSchema is not null)
		{
			options.Add(this.CreateTupleSchema([new JsonSchema().Set("type", "null"), baseSchema]).Set("minItems", 2L).Set("maxItems", 2L));
		}

		return new JsonSchema().SetSchemas("oneOf", options);
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
