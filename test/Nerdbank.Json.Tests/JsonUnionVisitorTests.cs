// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json;
using PolyType;
using PolyType.Abstractions;
using Xunit;

public partial class JsonObjectSerializerTests
{
	[Test]
	public void SerializeDeserialize_UnionBaseType_UsesNullAlias()
	{
		JsonSerializer serializer = new();
		Animal value = new("Milo");

		ITypeShape<Animal> shape = TypeShapeResolver.ResolveDynamicOrThrow<Animal>();
		string json = serializer.Serialize(value, shape);
		Animal roundTripped = serializer.Deserialize(json, shape);

		Assert.Equal("[null,{\"name\":\"Milo\"}]", json);
		Assert.Equal(value, roundTripped);
	}

	[Test]
	public void SerializeDeserialize_UnionDerivedType_UsesStringAlias()
	{
		JsonSerializer serializer = new();
		Animal value = new Cat("Milo", 9);

		ITypeShape<Animal> shape = TypeShapeResolver.ResolveDynamicOrThrow<Animal>();
		string json = serializer.Serialize(value, shape);
		Animal roundTripped = serializer.Deserialize(json, shape);

		Assert.Equal("[\"Cat\",{\"lives\":9,\"name\":\"Milo\"}]", json);
		Assert.Equal(value, roundTripped);
	}

	[Test]
	public void SerializeDeserialize_UnionDerivedType_UsesIntegerTagWhenSpecified()
	{
		JsonSerializer serializer = new();
		TaggedAnimal value = new TaggedCat("Otis", 7);

		ITypeShape<TaggedAnimal> shape = TypeShapeResolver.ResolveDynamicOrThrow<TaggedAnimal>();
		string json = serializer.Serialize(value, shape);
		TaggedAnimal roundTripped = serializer.Deserialize(json, shape);

		Assert.Equal("[3,{\"lives\":7,\"name\":\"Otis\"}]", json);
		Assert.Equal(value, roundTripped);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithUnionProperty()
	{
		JsonSerializer serializer = new();
		UnionContainer value = new() { Pet = new Cat("Milo", 9) };

		string json = serializer.Serialize(value);
		UnionContainer roundTripped = serializer.Deserialize<UnionContainer>(json);

		Assert.Equal("{\"pet\":[\"Cat\",{\"lives\":9,\"name\":\"Milo\"}]}", json);
		AssertRoundtrip(json, serializer, value);
		AssertStructuralEqual(value, roundTripped, json);
	}

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
