// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using JsonSchemaArray = System.Text.Json.Nodes.JsonArray;
using JsonSchemaNode = System.Text.Json.Nodes.JsonNode;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;

[assembly: TypeShapeExtension(typeof(int), AssociatedTypes = new[] { typeof(JsonSchemaTests.TextIntegerConverter) })]

public partial class JsonSchemaTests
{
	[Test]
	public void DefaultAndDescriptionAnnotations_UseConstructorPrecedence()
	{
		JsonSchemaObject schema = this.Serializer.GetJsonSchema(Shape<DefaultModel, DefaultModel>());
		Assert.Equal("Defaults contract.", schema["description"]!.GetValue<string>());
		JsonSchemaNode properties = schema["properties"]!;
		Assert.Equal(7, properties["count"]!["default"]!.GetValue<int>());
		Assert.Equal("default", properties["name"]!["default"]!.GetValue<string>());
		Assert.Equal(13, properties["limit"]!["default"]!.GetValue<int>());
		Assert.Equal("A configured limit.", properties["limit"]!["description"]!.GetValue<string>());
		Assert.Null(schema["required"]);
	}

	[Test]
	[Arguments(SerializeDefaultValuesPolicy.Always, true)]
	[Arguments(SerializeDefaultValuesPolicy.Required, true)]
	[Arguments(SerializeDefaultValuesPolicy.Never, false)]
	[Arguments(SerializeDefaultValuesPolicy.ValueTypes, false)]
	public void RequiredSchema_HonorsDefaultOmissionPolicy(SerializeDefaultValuesPolicy policy, bool required)
	{
		JsonSerializer serializer = new() { SerializeDefaultValues = policy };
		JsonSchemaObject schema = serializer.GetJsonSchema(Shape<Person, Person>());
		Assert.Equal(required, schema["required"] is not null);
	}

	[Test]
	public void CustomScalarConverter_TakesPrecedenceOverBuiltInSchema()
	{
		JsonSerializer serializer = new() { Converters = [new TextIntegerConverter()] };
		JsonSchemaObject schema = serializer.GetJsonSchema(Shape<int, Witness>());
		Assert.Equal("string", schema["type"]!.GetValue<string>());
		Assert.Null(schema["minimum"]);
		Assert.Equal("\"42\"", serializer.Serialize(42, Shape<int, Witness>()));
	}

	[Test]
	public void MemberConverterSchema_IsLocalAndOverridesTypeSchema()
	{
		JsonSchemaObject schema = this.Serializer.GetJsonSchema(Shape<MemberConverterModel, MemberConverterModel>());
		Assert.Equal("string", schema["properties"]!["text"]!["type"]!.GetValue<string>());
		Assert.Equal("integer", schema["properties"]!["number"]!["type"]!.GetValue<string>());
		Assert.Equal("""{"text":"3","number":4}""", this.Serializer.Serialize(new MemberConverterModel { Text = 3, Number = 4 }, Shape<MemberConverterModel, MemberConverterModel>()));
	}

	[Test]
	public void CustomScalarWithoutSchema_IsExplicitlyUndocumented()
	{
		JsonSerializer serializer = new() { Converters = [new UndocumentedIntegerConverter()] };
		JsonSchemaObject schema = serializer.GetJsonSchema(Shape<int, Witness>());
		Assert.NotNull(schema["$comment"]);
		Assert.Null(schema["type"]);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void RuntimeUnionSchema_TracksReplacementDisablingAndBaseFallback(bool useObjects)
	{
		JsonSerializer serializer = new()
		{
			UseDiscriminatorObjects = useObjects,
			DerivedTypeUnions = [new DerivedShapeMapping<Animal> { { "feline", Shape<Cat, Cat>() } }],
		};
		JsonSchemaObject schema = serializer.GetJsonSchema(Shape<Animal, Animal>());
		string text = schema.ToJsonString();
		Assert.Contains("feline", text, StringComparison.Ordinal);
		Assert.DoesNotContain("\"const\":\"Cat\"", text, StringComparison.Ordinal);
		Assert.Contains(useObjects ? "$base" : "\"type\":\"null\"", text, StringComparison.Ordinal);

		JsonSerializer disabled = serializer with { DerivedTypeUnions = [DerivedTypeUnion.CreateDisabled(typeof(Animal))] };
		JsonSchemaObject disabledSchema = disabled.GetJsonSchema(Shape<Animal, Animal>());
		Assert.NotNull(disabledSchema["properties"]!["name"]);
		Assert.DoesNotContain("feline", disabledSchema.ToJsonString(), StringComparison.Ordinal);
		Assert.Null(disabledSchema["oneOf"]);
		Assert.Null(disabledSchema["anyOf"]);
	}

	[Test]
	[Arguments(false, false)]
	[Arguments(false, true)]
	[Arguments(true, false)]
	[Arguments(true, true)]
	public void ReferencePreservedUnion_PayloadHasOneIdentityEnvelope(bool useObjects, bool runtimeUnion)
	{
		JsonSerializer serializer = new()
		{
			UseDiscriminatorObjects = useObjects,
			PreserveReferences = ReferencePreservationMode.RejectCycles,
			DerivedTypeUnions = runtimeUnion ? [new DerivedShapeMapping<Animal> { { "Cat", Shape<Cat, Cat>() } }] : [],
		};
		Animal cat = new Cat("Milo", 9);
		UnionPair value = new(cat, cat);
		string json = serializer.Serialize(value, Shape<UnionPair, UnionPair>());
		UnionPair? result = serializer.Deserialize(json, Shape<UnionPair, UnionPair>());
		Assert.NotNull(result);
		Assert.Same(result.First, result.Second);
		Assert.Contains("\"$ref\":", json, StringComparison.Ordinal);

		JsonSchemaObject schema = serializer.GetJsonSchema(Shape<Animal, Animal>());
		JsonSchemaNode unionPayload = schema["anyOf"]![0]!["oneOf"]![1]!["properties"]!["$value"]!;
		JsonSchemaNode casePayload = useObjects
			? unionPayload["oneOf"]![0]!["properties"]!["Cat"]!
			: unionPayload["oneOf"]![0]!["prefixItems"]![1]!;
		Assert.Equal("object", casePayload["type"]!.GetValue<string>());
		Assert.NotNull(casePayload["properties"]!["lives"]);
		Assert.Null(casePayload["oneOf"]);
		AssertReferencesResolve(schema, schema);
	}

	[Test]
	[Arguments(JsonSchemaDialect.Draft4)]
	[Arguments(JsonSchemaDialect.Draft7)]
	[Arguments(JsonSchemaDialect.Draft2020_12)]
	public void RecursiveUnionSchemas_ResolveReferenceAndPayloadDefinitions(JsonSchemaDialect dialect)
	{
		JsonSerializer serializer = new() { PreserveReferences = ReferencePreservationMode.RejectCycles };
		JsonSchemaObject schema = serializer.GetJsonSchema(Shape<RecursiveUnion, RecursiveUnion>(), new JsonSchemaOptions { Dialect = dialect });
		Assert.NotNull(schema[dialect == JsonSchemaDialect.Draft2020_12 ? "$defs" : "definitions"]);
		AssertReferencesResolve(schema, schema);
	}

	private static void AssertReferencesResolve(JsonSchemaNode node, JsonSchemaObject root)
	{
		if (node is JsonSchemaObject obj)
		{
			if (obj["$ref"] is System.Text.Json.Nodes.JsonValue reference && reference.TryGetValue(out string? path) && path!.StartsWith("#/", StringComparison.Ordinal))
			{
				JsonSchemaNode? target = root;
				foreach (string segment in path.Substring(2).Split('/'))
				{
					target = target?[segment.Replace("~1", "/").Replace("~0", "~")];
					Assert.NotNull(target);
				}
			}

			foreach (KeyValuePair<string, JsonSchemaNode?> property in obj)
			{
				if (property.Value is not null)
				{
					AssertReferencesResolve(property.Value, root);
				}
			}
		}
		else if (node is JsonSchemaArray array)
		{
			foreach (JsonSchemaNode? item in array)
			{
				if (item is not null)
				{
					AssertReferencesResolve(item, root);
				}
			}
		}
	}

	[GenerateShape]
	[Description("Defaults contract.")]
	internal partial record DefaultModel([property: DefaultValue(99)] int Count = 7, string? Name = "default")
	{
		[DefaultValue(13)]
		[Description("A configured limit.")]
		public int Limit { get; set; } = 13;
	}

	[GenerateShape]
	internal partial class MemberConverterModel
	{
		[JsonConverter(typeof(TextIntegerConverter))]
		public int Text { get; set; }

		public int Number { get; set; }
	}

	[GenerateShape]
	internal partial record UnionPair(Animal First, Animal Second);

	[GenerateShape]
	[DerivedTypeShape(typeof(RecursiveCase))]
	internal abstract partial record RecursiveUnion;

	[GenerateShape]
	internal partial record RecursiveCase(RecursiveUnion? Next) : RecursiveUnion;

	internal partial class TextIntegerConverter : JsonConverter<int>
	{
		public override void Write(ref JsonWriter writer, in int value, SerializationContext context) => writer.Write(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

		public override int Read(ref JsonReader reader, SerializationContext context) => int.Parse(reader.ReadString()!, System.Globalization.CultureInfo.InvariantCulture);

		public override JsonSchemaObject? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new() { ["type"] = "string" };
	}

	private sealed class UndocumentedIntegerConverter : JsonConverter<int>
	{
		public override void Write(ref JsonWriter writer, in int value, SerializationContext context) => writer.Write("integer");

		public override int Read(ref JsonReader reader, SerializationContext context) => 0;
	}
}
