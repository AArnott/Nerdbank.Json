// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public partial class JsonObjectSerializerTests
{
	[Test]
	public void SerializeDeserialize_UnionBaseType_UsesNullAlias()
	{
		Animal value = new("Milo");

		this.AssertRoundtrip(value, """[null,{"name":"Milo"}]""");
	}

	[Test]
	public void SerializeDeserialize_UnionDerivedType_UsesStringAlias()
	{
		Animal value = new Cat("Milo", 9);

		this.AssertRoundtrip(value, """["Cat",{"lives":9,"name":"Milo"}]""");
	}

	[Test]
	public void SerializeDeserialize_UnionDerivedType_UsesIntegerTagWhenSpecified()
	{
		TaggedAnimal value = new TaggedCat("Otis", 7);

		this.AssertRoundtrip(value, """[3,{"lives":7,"name":"Otis"}]""");
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithUnionProperty()
	{
		UnionContainer value = new() { Pet = new Cat("Milo", 9) };
		this.AssertRoundtrip(value, """{"pet":["Cat",{"lives":9,"name":"Milo"}]}""");
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
