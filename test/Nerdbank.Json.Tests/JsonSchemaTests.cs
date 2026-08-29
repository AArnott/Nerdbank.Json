// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using PolyType.Abstractions;

public partial class JsonSchemaTests : TestBase
{
	internal enum Fruit
	{
		Apple,
		Banana,
	}

	[Test]
	public void Scalar_Int_HasRange()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<int, Witness>());
		Assert.Equal("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"integer","minimum":-2147483648,"maximum":2147483647}""", schema);
	}

	[Test]
	public void Scalar_Formats()
	{
		Assert.Contains("\"type\":\"string\",\"format\":\"uuid\"", this.Serializer.GetJsonSchema(Shape<Guid, Witness>()), StringComparison.Ordinal);
		Assert.Contains("\"type\":\"string\",\"format\":\"date-time\"", this.Serializer.GetJsonSchema(Shape<DateTime, Witness>()), StringComparison.Ordinal);
		Assert.Contains("\"type\":\"string\",\"contentEncoding\":\"base64\"", this.Serializer.GetJsonSchema(Shape<byte[], Witness>()), StringComparison.Ordinal);
		Assert.Contains("\"type\":\"boolean\"", this.Serializer.GetJsonSchema(Shape<bool, Witness>()), StringComparison.Ordinal);
	}

	[Test]
	public void Object_NamingPolicy_Required_Nullable()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<Person, Person>());
		Assert.Equal("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer","minimum":-2147483648,"maximum":2147483647},"nickname":{"type":["string","null"]}},"required":["name","age"],"additionalProperties":false}""", schema);
	}

	[Test]
	public void Object_NamingPolicyNull_UsesClrNames()
	{
		JsonSerializer serializer = new() { PropertyNamingPolicy = null };
		string schema = serializer.GetJsonSchema(Shape<Person, Person>());
		Assert.Contains("\"properties\":{\"Name\":", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Enum_ByValue_IsInteger()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<Fruit, Witness>());
		Assert.Contains("\"type\":\"integer\"", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Enum_ByName_UsesStringEnum()
	{
		JsonSerializer serializer = new() { SerializeEnumValuesByName = true };
		string schema = serializer.GetJsonSchema(Shape<Fruit, Witness>());
		Assert.Contains("\"type\":\"string\",\"enum\":[\"apple\",\"banana\"]", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Array_HasItems()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<int[], Witness>());
		Assert.Contains("\"type\":\"array\",\"items\":{\"type\":\"integer\"", schema, StringComparison.Ordinal);
	}

	[Test]
	public void MultidimensionalArray_NestsItems()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<int[,], Witness>());
		Assert.Contains("\"type\":\"array\",\"items\":{\"type\":\"array\",\"items\":{\"type\":\"integer\"", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Dictionary_UsesAdditionalProperties()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<Dictionary<string, int>, Witness>());
		Assert.Contains("\"type\":\"object\",\"additionalProperties\":{\"type\":\"integer\"", schema, StringComparison.Ordinal);
	}

	[Test]
	public void ExtensionData_AllowsAdditionalProperties()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<WithExtensionData, WithExtensionData>());
		Assert.Contains("\"additionalProperties\":true", schema, StringComparison.Ordinal);
	}

	[Test]
	public void GeneratedUnion_ProducesOneOfEnvelope()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<Animal, Animal>());
		Assert.Contains("\"oneOf\":", schema, StringComparison.Ordinal);
		Assert.Contains("\"const\":\"Cat\"", schema, StringComparison.Ordinal);
		Assert.Contains("\"prefixItems\":", schema, StringComparison.Ordinal);
	}

	[Test]
	public void RuntimeUnion_ProducesOneOfEnvelope()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Animal>.Create().AddCase(7, Shape<Cat, Cat>())),
		};

		string schema = serializer.GetJsonSchema(Shape<Animal, Animal>());
		Assert.Contains("\"const\":7", schema, StringComparison.Ordinal);
	}

	[Test]
	public void DuckTypingUnion_ProducesOneOfOfObjects()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Animal>.Create().AddCase("cat", Shape<Cat, Cat>()).UseDuckTyping()),
		};

		string schema = serializer.GetJsonSchema(Shape<Animal, Animal>());
		Assert.Contains("\"oneOf\":[{\"type\":\"object\"", schema, StringComparison.Ordinal);
		Assert.DoesNotContain("prefixItems", schema, StringComparison.Ordinal);
	}

	[Test]
	public void ReferencePreservation_DescribesEnvelope()
	{
		JsonSerializer serializer = new() { PreserveReferences = ReferencePreservationMode.RejectCycles };
		string schema = serializer.GetJsonSchema(Shape<Person, Person>());
		Assert.Contains("\"$ref\":{\"type\":\"integer\"}", schema, StringComparison.Ordinal);
		Assert.Contains("\"$id\":{\"type\":\"integer\"}", schema, StringComparison.Ordinal);
		Assert.Contains("$value", schema, StringComparison.Ordinal);
	}

	[Test]
	public void RecursiveType_UsesDefsAndRef()
	{
		string schema = this.Serializer.GetJsonSchema(Shape<TreeNode, TreeNode>());
		Assert.Contains("\"$defs\":", schema, StringComparison.Ordinal);
		Assert.Contains("#/$defs/", schema, StringComparison.Ordinal);
	}

	[Test]
	public void CustomConverter_Override_IsUsed()
	{
		JsonSerializer serializer = new()
		{
			Converters = new ConverterCollection([new MoneyConverter()]),
		};

		string schema = serializer.GetJsonSchema(Shape<Money, Money>());
		Assert.Contains("\"type\":\"string\",\"pattern\"", schema, StringComparison.Ordinal);
	}

	[Test]
	public void CustomConverter_NoOverride_ProducesConspicuousAnnotation()
	{
		JsonSerializer serializer = new()
		{
			Converters = new ConverterCollection([new OpaqueConverter()]),
		};

		string schema = serializer.GetJsonSchema(Shape<Opaque, Opaque>());
		Assert.Contains("$comment", schema, StringComparison.Ordinal);
		Assert.Contains("No JSON schema is available", schema, StringComparison.Ordinal);
	}

	[Test]
	public void Output_IsDeterministic()
	{
		string first = this.Serializer.GetJsonSchema(Shape<Person, Person>());
		string second = this.Serializer.GetJsonSchema(Shape<Person, Person>());
		Assert.Equal(first, second);
	}

	private static ITypeShape<T> Shape<T, TProvider>()
#if NET
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#endif

	[GenerateShape]
	internal partial record Person(string Name, int Age)
	{
		public string? Nickname { get; set; }
	}

	[GenerateShape]
	internal partial class WithExtensionData
	{
		public string? Name { get; set; }

		[JsonExtensionData]
		public Dictionary<string, string>? Extra { get; set; }
	}

	[GenerateShape]
	[DerivedTypeShape(typeof(Cat))]
	internal partial record Animal(string Name);

	[GenerateShape]
	internal partial record Cat(string Name, int Lives) : Animal(Name);

	[GenerateShape]
	internal partial class TreeNode
	{
		public int Value { get; set; }

		public TreeNode? Left { get; set; }

		public TreeNode? Right { get; set; }
	}

	[GenerateShape]
	internal partial record Money(decimal Amount);

	internal sealed class MoneyConverter : JsonConverter<Money>
	{
		public override void Write(ref JsonWriter writer, Money? value, SerializationContext context) => writer.WriteStringValue(value?.Amount.ToString());

		public override Money? Read(ref JsonReader reader, SerializationContext context) => new(decimal.Parse(reader.ReadString()!));

		public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape)
			=> new JsonSchema().Set("type", "string").Set("pattern", "^-?[0-9]+(\\.[0-9]+)?$");
	}

	[GenerateShape]
	internal partial record Opaque(int Value);

	internal sealed class OpaqueConverter : JsonConverter<Opaque>
	{
		public override void Write(ref JsonWriter writer, Opaque? value, SerializationContext context) => writer.WriteNumberValue(value?.Value ?? 0);

		public override Opaque? Read(ref JsonReader reader, SerializationContext context) => new(int.Parse(reader.ReadNumberToken()));
	}

	[GenerateShapeFor<int>]
	[GenerateShapeFor<bool>]
	[GenerateShapeFor<Guid>]
	[GenerateShapeFor<DateTime>]
	[GenerateShapeFor<byte[]>]
	[GenerateShapeFor<int[]>]
	[GenerateShapeFor<int[,]>]
	[GenerateShapeFor<Fruit>]
	[GenerateShapeFor<Dictionary<string, int>>]
	internal partial class Witness;
}
