// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public partial class JsonSchemaTests
{
	[Test]
	public void Draft4_UsesDraft4DefinitionsAndReferences()
	{
		JsonSchemaOptions options = new() { Dialect = JsonSchemaDialect.Draft4 };
		string schema = this.Serializer.GetJsonSchema<TreeNode>(options);

		Assert.Contains("\"$schema\":\"http://json-schema.org/draft-04/schema#\"", schema, StringComparison.Ordinal);
		Assert.Contains("\"definitions\":", schema, StringComparison.Ordinal);
		Assert.Contains("#/definitions/TreeNode", schema, StringComparison.Ordinal);
		Assert.DoesNotContain("\"$defs\":", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Draft4_UsesEnumDiscriminatorsAndTupleItems()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<Animal, Animal>(), new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft4 });

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

		string schema = serializer.GetJsonSchema(Shape<Animal, Animal>(), new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft4 });

		Assert.Contains("\"enum\":[7]", schema, StringComparison.Ordinal);
		Assert.Contains("\"items\":[", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Draft4_PointUsesAdditionalItems()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<System.Drawing.Point, Witness>(), new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft4 });

		Assert.Contains("\"items\":[{\"type\":\"integer\"},{\"type\":\"integer\"}]", schema, StringComparison.Ordinal);
		Assert.Contains("\"additionalItems\":{\"type\":\"integer\"}", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Draft2020_12_PointUsesPrefixItems()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<System.Drawing.Point, Witness>());

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
