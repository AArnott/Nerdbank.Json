// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using JsonSchemaArray = System.Text.Json.Nodes.JsonArray;
using JsonSchemaNode = System.Text.Json.Nodes.JsonNode;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;

#pragma warning disable DuckTyping // Describes the experimental union strategy.
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
	private readonly Dictionary<string, JsonSchemaObject> definitions = new(StringComparer.Ordinal);
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

	/// <summary>
	/// Gets the converter cache that owns the schema generation operation.
	/// </summary>
	internal ConverterCache Owner => this.owner;

	/// <summary>
	/// Gets the keyword used for reusable schema definitions in the selected dialect.
	/// </summary>
	internal string DefinitionsKeyword => this.Dialect switch
	{
		JsonSchemaDialect.Draft4 or JsonSchemaDialect.Draft7 => "definitions",
		JsonSchemaDialect.Draft2020_12 => "$defs",
		_ => throw new NotSupportedException($"Unsupported JSON Schema dialect: {this.Dialect}."),
	};

	/// <summary>
	/// Gets the meta-schema URI for the selected dialect.
	/// </summary>
	internal string SchemaUri => this.Dialect switch
	{
		JsonSchemaDialect.Draft4 => "http://json-schema.org/draft-04/schema#",
		JsonSchemaDialect.Draft7 => "http://json-schema.org/draft-07/schema#",
		JsonSchemaDialect.Draft2020_12 => "https://json-schema.org/draft/2020-12/schema",
		_ => throw new NotSupportedException($"Unsupported JSON Schema dialect: {this.Dialect}."),
	};

	/// <summary>
	/// Gets the schema for a type shape, resolving shared definitions and recursion.
	/// </summary>
	/// <param name="typeShape">The shape of the type.</param>
	/// <returns>The schema, which may be a <c>$ref</c> to a shared definition.</returns>
	public JsonSchemaObject GetJsonSchema(ITypeShape typeShape)
	{
		Requires.NotNull(typeShape);
		Type type = typeShape.Type;

		if (this.references.TryGetValue(type, out string? existing))
		{
			return Reference(existing);
		}

		if (JsonSchemaScalars.TryGetScalarSchema(type, this, out JsonSchemaObject scalar))
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

		JsonSchemaObject schema = (JsonSchemaObject)typeShape.Invoke(this)!;
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
	internal JsonSchemaObject CreateTupleSchema(IReadOnlyList<JsonSchemaObject> items, JsonSchemaObject? additionalItemsSchema = null)
	{
		JsonSchemaObject schema = new JsonSchemaObject().Set("type", "array");
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
	internal void ApplyTupleSchema(JsonSchemaObject schema, IReadOnlyList<JsonSchemaObject> items, JsonSchemaObject? additionalItemsSchema = null)
	{
		Requires.NotNull(schema);
		Requires.NotNull(items);
		Requires.Argument(items.Count > 0, nameof(items), "At least one positional schema is required.");

		if (this.Dialect is JsonSchemaDialect.Draft4 or JsonSchemaDialect.Draft7)
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

	/// <summary>Creates a schema for a positional array in the selected dialect.</summary>
	/// <param name="items">The positional schemas. Ownership transfers to the result.</param>
	/// <returns>The array schema, without length constraints.</returns>
	public JsonSchemaObject CreateTupleSchema(JsonSchemaArray items)
	{
		JsonSchemaObject schema = new() { ["type"] = "array" };
		this.ApplyTupleSchema(schema, items);
		return schema;
	}

	/// <summary>Adds positional constraints to a schema in the selected dialect.</summary>
	/// <param name="schema">The schema to modify.</param>
	/// <param name="items">The positional schemas. Ownership transfers to the schema.</param>
	public void ApplyTupleSchema(JsonSchemaObject schema, JsonSchemaArray items)
	{
		Requires.NotNull(schema);
		Requires.NotNull(items);
		Requires.Argument(items.Count > 0, nameof(items), "At least one positional schema is required.");
		schema[this.Dialect is JsonSchemaDialect.Draft4 or JsonSchemaDialect.Draft7 ? "items" : "prefixItems"] = items;
	}

	object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
	{
		if (this.owner.TryGetCustomConverter(typeShape, out JsonConverter? custom) && custom is not null)
		{
			return custom.GetJsonSchema(this, typeShape) ?? CreateUndocumented(custom);
		}

		if (this.owner.DerivedTypeUnions.TryGetDerivedTypeUnion(typeof(T), out DerivedTypeUnion? entry) && entry is not null)
		{
			return this.BuildRuntimeUnionSchema(typeShape, entry);
		}

		return typeShape.Accept(this.visitor)!;
	}

	internal static JsonSchemaObject Reference(string path) => new JsonSchemaObject().Set("$ref", path);

	internal JsonSchemaObject GenerateDocument(ITypeShape rootShape)
	{
		JsonSchemaObject body = this.GetJsonSchema(rootShape);
		JsonSchemaObject document = new JsonSchemaObject().Set("$schema", this.SchemaUri);
		document.Append(body);

		if (this.definitions.Count > 0)
		{
			List<KeyValuePair<string, JsonSchemaObject>> sorted = this.definitions
				.OrderBy(pair => pair.Key, StringComparer.Ordinal)
				.ToList();
			document.SetMap(this.DefinitionsKeyword, sorted);
		}

		return document;
	}

	internal JsonSchemaObject CreateUnionEnvelope(IReadOnlyList<(object? Alias, JsonSchemaObject CaseSchema)> cases, JsonSchemaObject? baseSchema)
	{
		List<JsonSchemaObject> options = new(cases.Count + (baseSchema is null ? 0 : 1));
		HashSet<string> objectAliases = new(StringComparer.Ordinal);
		foreach ((object? alias, JsonSchemaObject caseSchema) in cases)
		{
			if (this.owner.UseDiscriminatorObjects)
			{
				string propertyName = JsonUnionObjectEnvelope.GetAlias(alias as int?, alias as string);
				if (propertyName == JsonUnionObjectEnvelope.BaseAlias || !objectAliases.Add(propertyName))
				{
					throw new NotSupportedException($"The union discriminator '{propertyName}' is reserved or ambiguous in object format.");
				}

				options.Add(CreateObjectCase(propertyName, caseSchema));
				continue;
			}

			JsonSchemaObject discriminator = new();
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
			options.Add(this.owner.UseDiscriminatorObjects
				? CreateObjectCase(JsonUnionObjectEnvelope.BaseAlias, baseSchema)
				: this.CreateTupleSchema([new JsonSchemaObject().Set("type", "null"), baseSchema]).Set("minItems", 2L).Set("maxItems", 2L));
		}

		return new JsonSchemaObject().SetSchemas("oneOf", options);

		static JsonSchemaObject CreateObjectCase(string alias, JsonSchemaObject schema) => new JsonSchemaObject()
			.Set("type", "object")
			.SetMap("properties", [new(alias, schema)])
			.SetStrings("required", [alias])
			.Set("additionalProperties", false);
	}

	private static JsonSchemaObject CreateUndocumented(JsonConverter converter) => JsonConverter<int>.CreateUndocumentedSchema(converter.GetType());

	private JsonSchemaObject BuildRuntimeUnionSchema<T>(ITypeShape<T> shape, DerivedTypeUnion entry)
	{
		if (entry.Disabled)
		{
			ITypeShape baseShape = shape is IUnionTypeShape disabledUnion ? disabledUnion.BaseType : shape;
			return (JsonSchemaObject)baseShape.Accept(this.visitor)!;
		}

		List<(object? Alias, JsonSchemaObject CaseSchema)> cases = [];
		foreach (KeyValuePair<DerivedTypeIdentifier, ITypeShape> pair in RuntimeUnionBuilder.GetCases(entry))
		{
			DerivedTypeIdentifier alias = pair.Key;
			ITypeShape caseShape = pair.Value;
			JsonSchemaObject caseSchema = caseShape.Type == typeof(T)
				? (JsonSchemaObject)(shape is IUnionTypeShape union ? union.BaseType : shape).Accept(this.visitor)!
				: this.GetJsonSchema(caseShape);
			cases.Add((alias.Type == DerivedTypeIdentifier.AliasType.Integer ? (object)alias.IntAlias : alias.StringAlias, caseSchema));
		}

		if (entry is DerivedTypeDuckTyping)
		{
			return new JsonSchemaObject().SetSchemas("oneOf", [.. cases.Select(c => c.CaseSchema)]);
		}

		ITypeShape baseShapeForFallback = shape is IUnionTypeShape fallbackUnion ? fallbackUnion.BaseType : shape;
		JsonSchemaObject? baseSchema = RuntimeUnionBuilder.IsConstructible(baseShapeForFallback as IObjectTypeShape)
			? (JsonSchemaObject)baseShapeForFallback.Accept(this.visitor)!
			: null;
		return this.CreateUnionEnvelope(cases, baseSchema);
	}

	private JsonSchemaObject ApplyReferencePreservation(Type type, JsonSchemaObject schema)
	{
		if (!this.owner.ShouldPreserveReferences(type))
		{
			return schema;
		}

		JsonSchemaObject referenceForm = new JsonSchemaObject()
			.Set("type", "object")
			.SetMap("properties", [new("$ref", new JsonSchemaObject().Set("type", "integer"))])
			.SetStrings("required", ["$ref"])
			.Set("additionalProperties", false);

		JsonSchemaObject identityForm = new JsonSchemaObject()
			.Set("type", "object")
			.SetMap("properties", [new("$id", new JsonSchemaObject().Set("type", "integer")), new("$value", schema)])
			.SetStrings("required", ["$id", "$value"])
			.Set("additionalProperties", false);

		return new JsonSchemaObject().SetSchemas("oneOf", [referenceForm, identityForm]);
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
