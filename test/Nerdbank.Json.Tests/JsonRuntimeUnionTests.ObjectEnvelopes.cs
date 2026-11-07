// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO;
using System.Text;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;

public partial class JsonRuntimeUnionTests
{
	[Test]
	public async Task ObjectEnvelope_RuntimeStringAlias_RoundTrips()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { "circle", Shape<Circle, Circle>() } }],
		};
		Shape2 value = new Circle(2.5);
		string json = serializer.Serialize(value, Shape<Shape2, Shape2>());
		Assert.Equal("""{"$type":"circle","radius":2.5}""", json);
		Assert.Equal(value, serializer.Deserialize(json, Shape<Shape2, Shape2>()));

		using MemoryStream stream = new();
		await serializer.SerializeAsync(stream, value, Shape<Shape2, Shape2>());
		Assert.Equal(json, Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		Assert.Equal(value, await serializer.DeserializeAsync(stream, Shape<Shape2, Shape2>()));
		Assert.Equal("null", serializer.Serialize<Shape2>(null, Shape<Shape2, Shape2>()));
		Assert.Null(serializer.Deserialize("null", Shape<Shape2, Shape2>()));
	}

	[Test]
	public void ObjectEnvelope_IntegerAliases_AreInvariantPropertyText()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { -12, Shape<Circle, Circle>() } }],
		};
		CultureInfo previous = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
			string json = serializer.Serialize<Shape2>(new Circle(3), Shape<Shape2, Shape2>());
			Assert.Equal("""{"$type":"-12","radius":3}""", json);
			Assert.Equal(new Circle(3), serializer.Deserialize(json, Shape<Shape2, Shape2>()));
		}
		finally
		{
			CultureInfo.CurrentCulture = previous;
		}
	}

	[Test]
	public void ObjectEnvelope_GeneratedUnionAndBaseFallback_RoundTrip()
	{
		JsonSerializer serializer = new();
		Animal2 derived = new Cat2("Milo", 9);
		string json = serializer.Serialize(derived, Shape<Animal2, Animal2>());
		Assert.Equal("""{"$type":"Cat2","lives":9,"name":"Milo"}""", json);
		Assert.Equal(derived, serializer.Deserialize(json, Shape<Animal2, Animal2>()));
		Animal2 plain = new("Ada");
		Assert.Equal("""{"$type":"$base","name":"Ada"}""", serializer.Serialize(plain, Shape<Animal2, Animal2>()));
		Assert.Equal(plain, serializer.Deserialize("""{"$type":"$base","name":"Ada"}""", Shape<Animal2, Animal2>()));
	}

	[Test]
	public void ObjectEnvelope_RuntimeSelfMapping_AndNestedUnionSchema()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions =
			[
				new DerivedShapeMapping<Animal2>
				{
					{ "animal", Shape<Animal2, Animal2>() },
					{ "cat", Shape<Cat2, Cat2>() },
				},
			],
		};

		Animal2 plain = new("Ada");
		Assert.Equal("""{"$type":"animal","name":"Ada"}""", serializer.Serialize(plain, Shape<Animal2, Animal2>()));

		JsonSchemaObject runtimeSchema = serializer.GetJsonSchema(Shape<Animal2, Animal2>());
		JsonSchemaObject runtimeDefinition = runtimeSchema["$defs"]?["Animal2"]?.AsObject() ?? runtimeSchema["anyOf"]![0]!.AsObject();
		JsonSchemaObject selfCase = Assert.Single(runtimeDefinition["oneOf"]!.AsArray(), candidate => candidate!["properties"]?["$type"]?["const"]?.GetValue<string>() == "animal")!.AsObject();
		Assert.NotNull(selfCase["properties"]?["name"]);
		Assert.Null(selfCase["properties"]?["$value"]);

		AnimalPayloadOrNumber outer = new(plain);
		PolyType.ITypeShape<AnimalPayloadOrNumber> outerShape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<AnimalPayloadOrNumber>()!;
		string json = serializer.Serialize(outer, outerShape);
		Assert.Equal("""{"$type":"Animal2","$value":{"$type":"animal","name":"Ada"}}""", json);
		JsonSchemaObject outerSchema = serializer.GetJsonSchema(outerShape);
		string outerSchemaJson = outerSchema.ToJsonString();
		Assert.Contains("\"const\":\"Animal2\"", outerSchemaJson, StringComparison.Ordinal);
		Assert.Contains("\"const\":\"animal\"", outerSchemaJson, StringComparison.Ordinal);
		Assert.Contains("\"$value\"", outerSchemaJson, StringComparison.Ordinal);
	}

	[Test]
	public void ObjectEnvelope_RuntimeSelfMappingWithExtensionData_UsesBaseSchema()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions =
			[
				new DerivedShapeMapping<SelfMappedBase>
				{
					{ "self", Shape<SelfMappedBase, SelfMappedBase>() },
					{ "other", Shape<SelfMappedOther, SelfMappedOther>() },
				},
			],
		};

		SelfMappedBase value = new() { Name = "Ada" };
		string json = serializer.Serialize(value, Shape<SelfMappedBase, SelfMappedBase>());
		Assert.Equal("""{"$type":"self","$value":{"name":"Ada"}}""", json);

		JsonSchemaObject schema = serializer.GetJsonSchema(Shape<SelfMappedBase, SelfMappedBase>());
		JsonSchemaObject unionSchema = schema["anyOf"]?[0] as JsonSchemaObject ?? schema;
		JsonSchemaObject selfCase = Assert.Single(unionSchema["oneOf"]!.AsArray(), candidate => candidate!["properties"]?["$type"]?["const"]?.GetValue<string>() == "self")!.AsObject();
		Assert.Equal("object", selfCase["properties"]?["$value"]?["type"]?.GetValue<string>());
		Assert.NotNull(selfCase["properties"]?["$value"]?["properties"]?["name"]);
		Assert.Null(selfCase["properties"]?["$value"]?["oneOf"]);
	}

	[Test]
	public void ObjectEnvelope_GeneratedIntegerTag_RoundTrips()
	{
		JsonSerializer serializer = new();
		TaggedBase value = new TaggedCase(8);
		string json = serializer.Serialize(value, Shape<TaggedBase, TaggedBase>());
		Assert.Equal("""{"$type":"7","number":8}""", json);
		Assert.Equal(value, serializer.Deserialize(json, Shape<TaggedBase, TaggedBase>()));
	}

	[Test]
	[Arguments("{}")]
	[Arguments("""{"$type":"Cat2","lives":9,"name":"Milo","$type":"$base"}""")]
	[Arguments("""{"$type":"unknown"}""")]
	public void ObjectEnvelope_GeneratedInvalidInput_IsRejected(string json)
	{
		JsonSerializer serializer = new();
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json, Shape<Animal2, Animal2>()));
	}

	[Test]
	public void ObjectEnvelope_RuntimeBaseFallback_RoundTrips()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedShapeMapping<Animal2> { { "cat", Shape<Cat2, Cat2>() } }],
		};
		Animal2 plain = new("Ada");
		string json = serializer.Serialize(plain, Shape<Animal2, Animal2>());
		Assert.Equal("""{"$type":"$base","name":"Ada"}""", json);
		Assert.Equal(plain, serializer.Deserialize(json, Shape<Animal2, Animal2>()));
	}

	[Test]
	[Arguments("{}")]
	[Arguments("""{"$type":"circle","$type":"circle","radius":1}""")]
	[Arguments("""{"$type":"missing","radius":1}""")]
	[Arguments("""{"$type":"$base"}""")]
	[Arguments("""["circle",{"radius":1}]""")]
	public void ObjectEnvelope_InvalidInput_IsRejected(string json)
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { "circle", Shape<Circle, Circle>() } }],
		};
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json, Shape<Shape2, Shape2>()));
	}

	[Test]
	public async Task ObjectEnvelope_OutOfOrderMetadataProperties_AreOptional()
	{
		JsonSerializer serializer = new()
		{
			AllowOutOfOrderMetadataProperties = true,
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { "circle", Shape<Circle, Circle>() } }],
		};
		Shape2 expected = new Circle(1.5);
		string json = """{"radius":1.5,"$type":"circle"}""";
		Assert.Equal(expected, serializer.Deserialize(json, Shape<Shape2, Shape2>()));

		using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
		Assert.Equal(expected, await serializer.DeserializeAsync(stream, Shape<Shape2, Shape2>()));

		JsonSerializer orderedSerializer = serializer with { AllowOutOfOrderMetadataProperties = false };
		Assert.Throws<JsonSerializationException>(() => orderedSerializer.Deserialize(json, Shape<Shape2, Shape2>()));
	}

	[Test]
	public void ObjectEnvelope_CollidingIntegerAndStringAliases_AreRejected()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { 1, Shape<Circle, Circle>() }, { "1", Shape<Square, Square>() } }],
		};
		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => serializer.Serialize<Shape2>(new Circle(2), Shape<Shape2, Shape2>()));
		Assert.IsType<NotSupportedException>(exception.InnerException);
		Assert.Throws<NotSupportedException>(() => serializer.GetJsonSchema(Shape<Shape2, Shape2>()));
	}

	[Test]
	public void ObjectEnvelope_ReferenceMetadata_RemainsJsonObjects()
	{
		JsonSerializer serializer = new()
		{
			PreserveReferences = ReferencePreservationMode.RejectCycles,
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { "circle", Shape<Circle, Circle>() } }],
		};
		Shape2 value = new Circle(4);
		string json = serializer.Serialize(value, Shape<Shape2, Shape2>());
		Assert.StartsWith("""{"$id":""", json);
		Assert.Contains("\"$value\":{\"$type\":\"circle\"", json, StringComparison.Ordinal);
		Assert.Equal(value, serializer.Deserialize(json, Shape<Shape2, Shape2>()));
	}

	[Test]
	[Arguments(JsonSchemaDialect.Draft4)]
	[Arguments(JsonSchemaDialect.Draft7)]
	[Arguments(JsonSchemaDialect.Draft2020_12)]
	public void ObjectEnvelope_SchemaUsesTypeMetadata(JsonSchemaDialect dialect)
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedShapeMapping<Animal2> { { -12, Shape<Cat2, Cat2>() } }],
		};
		JsonSchemaObject schema = serializer.GetJsonSchema(Shape<Animal2, Animal2>(), new JsonSchemaOptions { Dialect = dialect });
		schema = schema["anyOf"]![0]!.AsObject();
		Assert.Equal("object", schema["oneOf"]![0]!["type"]!.GetValue<string>());
		Assert.Contains(schema["oneOf"]![0]!["required"]!.AsArray(), static property => property!.GetValue<string>() == "$type");
		JsonSchemaObject discriminator = schema["oneOf"]![0]!["properties"]!["$type"]!.AsObject();
		string expectedTag = dialect == JsonSchemaDialect.Draft4
			? discriminator["enum"]![0]!.GetValue<string>()
			: discriminator["const"]!.GetValue<string>();
		Assert.Equal("-12", expectedTag);
		Assert.False(schema["oneOf"]![0]!["additionalProperties"]!.GetValue<bool>());
		Assert.Contains(schema["oneOf"]![1]!["required"]!.AsArray(), static property => property!.GetValue<string>() == "$type");
		JsonSchemaObject baseDiscriminator = schema["oneOf"]![1]!["properties"]!["$type"]!.AsObject();
		string expectedBaseTag = dialect == JsonSchemaDialect.Draft4
			? baseDiscriminator["enum"]![0]!.GetValue<string>()
			: baseDiscriminator["const"]!.GetValue<string>();
		Assert.Equal("$base", expectedBaseTag);
		Assert.Null(schema["oneOf"]![0]!["prefixItems"]);
	}

	[GenerateShape]
	[DerivedTypeShape(typeof(TaggedCase), Tag = 7)]
	internal abstract partial record TaggedBase;

	[GenerateShape]
	internal partial record TaggedCase(int Number) : TaggedBase;
}
