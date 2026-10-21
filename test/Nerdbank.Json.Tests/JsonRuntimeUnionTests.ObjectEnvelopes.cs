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
			UseDiscriminatorObjects = true,
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { "circle", Shape<Circle, Circle>() } }],
		};
		Shape2 value = new Circle(2.5);
		string json = serializer.Serialize(value, Shape<Shape2, Shape2>());
		Assert.Equal("""{"circle":{"radius":2.5}}""", json);
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
			UseDiscriminatorObjects = true,
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { -12, Shape<Circle, Circle>() } }],
		};
		CultureInfo previous = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
			string json = serializer.Serialize<Shape2>(new Circle(3), Shape<Shape2, Shape2>());
			Assert.Equal("""{"-12":{"radius":3}}""", json);
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
		JsonSerializer serializer = new() { UseDiscriminatorObjects = true };
		Animal2 derived = new Cat2("Milo", 9);
		string json = serializer.Serialize(derived, Shape<Animal2, Animal2>());
		Assert.Equal("""{"Cat2":{"lives":9,"name":"Milo"}}""", json);
		Assert.Equal(derived, serializer.Deserialize(json, Shape<Animal2, Animal2>()));
		Animal2 plain = new("Ada");
		Assert.Equal("""{"$base":{"name":"Ada"}}""", serializer.Serialize(plain, Shape<Animal2, Animal2>()));
		Assert.Equal(plain, serializer.Deserialize("""{"$base":{"name":"Ada"}}""", Shape<Animal2, Animal2>()));
	}

	[Test]
	public void ObjectEnvelope_GeneratedIntegerTag_RoundTrips()
	{
		JsonSerializer serializer = new() { UseDiscriminatorObjects = true };
		TaggedBase value = new TaggedCase(8);
		string json = serializer.Serialize(value, Shape<TaggedBase, TaggedBase>());
		Assert.Equal("""{"7":{"number":8}}""", json);
		Assert.Equal(value, serializer.Deserialize(json, Shape<TaggedBase, TaggedBase>()));
	}

	[Test]
	[Arguments("{}")]
	[Arguments("""{"Cat2":{"lives":9,"name":"Milo"},"$base":{"name":"other"}}""")]
	[Arguments("""{"unknown":{}}""")]
	public void ObjectEnvelope_GeneratedInvalidInput_IsRejected(string json)
	{
		JsonSerializer serializer = new() { UseDiscriminatorObjects = true };
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json, Shape<Animal2, Animal2>()));
	}

	[Test]
	public void ObjectEnvelope_RuntimeBaseFallback_RoundTrips()
	{
		JsonSerializer serializer = new()
		{
			UseDiscriminatorObjects = true,
			DerivedTypeUnions = [new DerivedShapeMapping<Animal2> { { "cat", Shape<Cat2, Cat2>() } }],
		};
		Animal2 plain = new("Ada");
		string json = serializer.Serialize(plain, Shape<Animal2, Animal2>());
		Assert.Equal("""{"$base":{"name":"Ada"}}""", json);
		Assert.Equal(plain, serializer.Deserialize(json, Shape<Animal2, Animal2>()));
	}

	[Test]
	[Arguments("{}")]
	[Arguments("""{"circle":{"radius":1},"circle":{"radius":2}}""")]
	[Arguments("""{"missing":{"radius":1}}""")]
	[Arguments("""{"$base":{}}""")]
	[Arguments("""["circle",{"radius":1}]""")]
	public void ObjectEnvelope_InvalidInput_IsRejected(string json)
	{
		JsonSerializer serializer = new()
		{
			UseDiscriminatorObjects = true,
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { "circle", Shape<Circle, Circle>() } }],
		};
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json, Shape<Shape2, Shape2>()));
	}

	[Test]
	public void ObjectEnvelope_CollidingIntegerAndStringAliases_AreRejected()
	{
		JsonSerializer serializer = new()
		{
			UseDiscriminatorObjects = true,
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { 1, Shape<Circle, Circle>() }, { "1", Shape<Square, Square>() } }],
		};
		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => serializer.Serialize<Shape2>(new Circle(2), Shape<Shape2, Shape2>()));
		Assert.IsType<NotSupportedException>(exception.InnerException);
		Assert.Throws<NotSupportedException>(() => serializer.GetJsonSchema(Shape<Shape2, Shape2>()));
		JsonSerializer arraySerializer = serializer with { UseDiscriminatorObjects = false };
		Assert.Equal("""[1,{"radius":2}]""", arraySerializer.Serialize<Shape2>(new Circle(2), Shape<Shape2, Shape2>()));
	}

	[Test]
	public void ObjectEnvelope_ReferenceMetadata_RemainsJsonObjects()
	{
		JsonSerializer serializer = new()
		{
			UseDiscriminatorObjects = true,
			PreserveReferences = ReferencePreservationMode.RejectCycles,
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { "circle", Shape<Circle, Circle>() } }],
		};
		Shape2 value = new Circle(4);
		string json = serializer.Serialize(value, Shape<Shape2, Shape2>());
		Assert.StartsWith("""{"$id":""", json);
		Assert.Contains("\"$value\":{\"circle\":", json, StringComparison.Ordinal);
		Assert.Equal(value, serializer.Deserialize(json, Shape<Shape2, Shape2>()));
	}

	[Test]
	[Arguments(JsonSchemaDialect.Draft4)]
	[Arguments(JsonSchemaDialect.Draft7)]
	[Arguments(JsonSchemaDialect.Draft2020_12)]
	public void ObjectEnvelope_SchemaUsesRequiredSingleProperty(JsonSchemaDialect dialect)
	{
		JsonSerializer serializer = new()
		{
			UseDiscriminatorObjects = true,
			DerivedTypeUnions = [new DerivedShapeMapping<Animal2> { { -12, Shape<Cat2, Cat2>() } }],
		};
		JsonSchemaObject schema = serializer.GetJsonSchema(Shape<Animal2, Animal2>(), new JsonSchemaOptions { Dialect = dialect });
		Assert.Equal("object", schema["oneOf"]![0]!["type"]!.GetValue<string>());
		Assert.Equal("-12", schema["oneOf"]![0]!["required"]![0]!.GetValue<string>());
		Assert.False(schema["oneOf"]![0]!["additionalProperties"]!.GetValue<bool>());
		Assert.Equal("$base", schema["oneOf"]![1]!["required"]![0]!.GetValue<string>());
		Assert.Null(schema["oneOf"]![0]!["prefixItems"]);
	}

	[GenerateShape]
	[DerivedTypeShape(typeof(TaggedCase), Tag = 7)]
	internal abstract partial record TaggedBase;

	[GenerateShape]
	internal partial record TaggedCase(int Number) : TaggedBase;
}
