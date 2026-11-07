// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public partial class JsonObjectSerializerTests
{
	[GenerateShape]
	[DerivedTypeShape(typeof(CatWithParameterizedConstructor))]
	internal partial interface IAnimal
	{
		string Name { get; }
	}

	[Test]
	public void SerializeDeserialize_UnionBaseType_UsesNullAlias()
	{
		Animal value = new("Milo");

		this.AssertRoundtrip(value, """{"$type":"$base","name":"Milo"}""");
	}

	[Test]
	public void SerializeDeserialize_UnionDerivedType_UsesStringAlias()
	{
		Animal value = new Cat("Milo", 9);

		this.AssertRoundtrip(value, """{"$type":"Cat","lives":9,"name":"Milo"}""");
	}

	[Test]
	public void SerializeDeserialize_UnionDerivedType_UsesIntegerTagWhenSpecified()
	{
		TaggedAnimal value = new TaggedCat("Otis", 7);

		this.AssertRoundtrip(value, """{"$type":"3","lives":7,"name":"Otis"}""");
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithUnionProperty()
	{
		UnionContainer value = new() { Pet = new Cat("Milo", 9) };
		this.AssertRoundtrip(value, """{"pet":{"$type":"Cat","lives":9,"name":"Milo"}}""");
	}

	[Test]
	public void Serialize_Union_ExceedingMaxDepth_Throws()
	{
		this.Serializer = this.Serializer with
		{
			StartingContext = this.Serializer.StartingContext with { MaxDepth = 1 },
		};

		Animal value = new Cat("Milo", 9);

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(value));
		Assert.Contains("Exceeded maximum depth", exception.Message);
	}

	[Test]
	public void Deserialize_Union_ExceedingMaxDepth_Throws()
	{
		this.Serializer = this.Serializer with
		{
			StartingContext = this.Serializer.StartingContext with { MaxDepth = 1 },
		};

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<Animal>("""{"$type":"Cat","lives":9,"name":"Milo"}"""));
		Assert.Contains("Exceeded maximum depth", exception.Message);
	}

	[Test]
	public void SerializeDeserialize_UnionInterfaceWithParameterizedDerivedType()
	{
		IAnimal value = new CatWithParameterizedConstructor("Milo", 9);

		this.AssertRoundtrip(value, PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<IAnimal>()!, """{"$type":"CatWithParameterizedConstructor","name":"Milo","lives":9}""");
	}

	[Test]
	public void Deserialize_UnionInterfaceBaseValue_ThrowsJsonSerializationException()
	{
		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize("""{"$type":"$base","name":"Milo"}""", PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<IAnimal>()!));
		Assert.Contains("Unrecognized union discriminator", exception.Message);
	}

	[Test, MatrixDataSource]
	public async Task AsyncUnionRoundtrip(bool derived)
	{
		Animal value = derived ? new Cat("Milo", 9) : new Animal("Milo");
		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, value);
		Assert.Equal(this.Serializer.Serialize(value), System.Text.Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		Assert.Equal(value, await this.Serializer.DeserializeAsync<Animal>(stream));
	}

	[GenerateShape]
	internal partial record CatWithParameterizedConstructor(string Name, int Lives) : IAnimal;

	[GenerateShape]
	[DerivedTypeShape(typeof(Cat))]
	internal partial record Animal(string Name);

	[GenerateShape]
	internal partial record Cat(string Name, int Lives) : Animal(Name);

	[GenerateShape]
	[DerivedTypeShape(typeof(TaggedCat), Tag = 3)]
	internal partial record TaggedAnimal(string Name);

	[GenerateShape]
	internal partial record TaggedCat(string Name, int Lives) : TaggedAnimal(Name);

	[GenerateShape]
	internal partial class UnionContainer
	{
		public Animal? Pet { get; set; }
	}
}
