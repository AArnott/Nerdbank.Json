// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public partial class JsonSchemaTests
{
	[Test]
	[Arguments(JsonSchemaDialect.Draft4, "http://json-schema.org/draft-04/schema#")]
	[Arguments(JsonSchemaDialect.Draft7, "http://json-schema.org/draft-07/schema#")]
	public void LegacyDraft_UsesDefinitionsAndReferences(JsonSchemaDialect dialect, string schemaUri)
	{
		JsonSchemaOptions options = new() { Dialect = dialect };
		string schema = this.Serializer.GetJsonSchema(Shape<TreeNode, TreeNode>(), options).ToJsonString();

		Assert.Contains($"\"$schema\":\"{schemaUri}\"", schema, StringComparison.Ordinal);
		Assert.Contains("\"definitions\":", schema, StringComparison.Ordinal);
		Assert.Contains("#/definitions/TreeNode", schema, StringComparison.Ordinal);
		Assert.DoesNotContain("\"$defs\":", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Draft4_UsesEnumDiscriminatorsAndTupleItems()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<Animal, Animal>(), new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft4 }).ToJsonString();

		Assert.Contains("\"enum\":[\"Cat\"]", schema, StringComparison.Ordinal);
		Assert.Contains("\"items\":[", schema, StringComparison.Ordinal);
		Assert.DoesNotContain("\"const\":", schema, StringComparison.Ordinal);
		Assert.DoesNotContain("\"prefixItems\":", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Draft4_RuntimeUnionSupportsIntegerDiscriminators()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Animal>.Create().AddCase(7, Shape<Cat, Cat>())),
		};

		string schema = serializer.GetJsonSchema(Shape<Animal, Animal>(), new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft4 }).ToJsonString();

		Assert.Contains("\"enum\":[7]", schema, StringComparison.Ordinal);
		Assert.Contains("\"items\":[", schema, StringComparison.Ordinal);
	}

	[Test]
	[Arguments(JsonSchemaDialect.Draft4)]
	[Arguments(JsonSchemaDialect.Draft7)]
	public void LegacyDraft_PointUsesAdditionalItems(JsonSchemaDialect dialect)
	{
		string schema = this.Serializer.GetJsonSchema(Shape<System.Drawing.Point, Witness>(), new JsonSchemaOptions { Dialect = dialect }).ToJsonString();

		Assert.Contains("\"items\":[{\"type\":\"integer\"},{\"type\":\"integer\"}]", schema, StringComparison.Ordinal);
		Assert.Contains("\"additionalItems\":{\"type\":\"integer\"}", schema, StringComparison.Ordinal);
		Assert.DoesNotContain("\"prefixItems\":", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Draft7_UsesConstDiscriminatorsAndTupleItems()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<Animal, Animal>(), new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft7 }).ToJsonString();

		Assert.Contains("\"items\":[{\"const\":\"Cat\"},", schema, StringComparison.Ordinal);
		Assert.Contains("\"minItems\":2,\"maxItems\":2", schema, StringComparison.Ordinal);
		Assert.DoesNotContain("\"enum\":", schema, StringComparison.Ordinal);
		Assert.DoesNotContain("\"prefixItems\":", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Draft7_RuntimeUnionSupportsIntegerDiscriminators()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Animal>.Create().AddCase(7, Shape<Cat, Cat>())),
		};

		string schema = serializer.GetJsonSchema(Shape<Animal, Animal>(), new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft7 }).ToJsonString();

		Assert.Contains("\"items\":[{\"const\":7},", schema, StringComparison.Ordinal);
		Assert.DoesNotContain("\"enum\":", schema, StringComparison.Ordinal);
		Assert.DoesNotContain("\"prefixItems\":", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Draft7_ObjectPreservesRequiredAndNullableMembers()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<Person, Person>(), new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft7 }).ToJsonString();

		Assert.Equal("""{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer","minimum":-2147483648,"maximum":2147483647},"nickname":{"type":["string","null"]}},"required":["name","age"],"additionalProperties":false}""", schema);
	}

	[Test]
	public void Draft7_UntypedShapeIsSupported()
	{
		ITypeShape shape = Shape<int, Witness>();
		string schema = this.Serializer.GetJsonSchema(shape, new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft7 }).ToJsonString();

		Assert.Equal("""{"$schema":"http://json-schema.org/draft-07/schema#","type":"integer","minimum":-2147483648,"maximum":2147483647}""", schema);
	}

	[Test]
	public void Draft2020_12_PointUsesPrefixItems()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<System.Drawing.Point, Witness>()).ToJsonString();

		Assert.Contains("\"prefixItems\":[{\"type\":\"integer\"},{\"type\":\"integer\"}]", schema, StringComparison.Ordinal);
		Assert.Contains("\"items\":{\"type\":\"integer\"}", schema, StringComparison.Ordinal);
	}

	[Test]
	public void UnsupportedDialect_IsRejected()
	{
		JsonSchemaOptions options = new() { Dialect = (JsonSchemaDialect)100 };

		Assert.Throws<ArgumentOutOfRangeException>(() => this.Serializer.GetJsonSchema(Shape<Person, Person>(), options));
	}
}
