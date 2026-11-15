// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PolyType.SourceGenerator;

public partial class UnionTaggingTests : TestBase
{
	[Test]
	public void Auto_WritesAUniqueObjectBare()
	{
		Assert.Equal("5", this.Serializer.Serialize(new Measure(5)));
		Assert.Equal("""{"x":1,"y":2}""", this.Serializer.Serialize(new Measure(new Plot(1, 2))));
		Assert.Equal(new Plot(1, 2), this.Serializer.Deserialize<Measure>("""{"x":1,"y":2}""").Value);
	}

	[Test]
	public void Tagged_WritesEveryObjectWithType()
	{
		JsonSerializer serializer = this.Serializer with { DefaultUnionTagging = UnionTagging.Tagged };
		Assert.Equal("5", serializer.Serialize(new Measure(5)));
		Assert.Equal("""{"$type":"Plot","x":1,"y":2}""", serializer.Serialize(new Measure(new Plot(1, 2))));
		Assert.Equal(new Plot(1, 2), serializer.Deserialize<Measure>("""{"$type":"Plot","x":1,"y":2}""").Value);
	}

	[Test]
	public void Untagged_RejectsObjectsThatCannotBeDistinguished()
	{
		JsonSerializer serializer = this.Serializer with { DefaultUnionTagging = UnionTagging.Untagged };
		JsonSerializationException ex = Assert.Throws<JsonSerializationException>(() => serializer.Serialize(new Twins(new Left(1))));
		Assert.IsType<NotSupportedException>(ex.InnerException);
		Assert.Equal("1", serializer.Serialize(new Widths(1)));
		Assert.Equal("""{"x":1,"y":0}""", serializer.Serialize(new Measure(new Plot(1, 0))));
	}

	[Test]
	public void Auto_WritesCollidingScalarsBare()
	{
		Assert.Equal("1", this.Serializer.Serialize(new Widths(1)));
		Assert.Equal(1, this.Serializer.Deserialize<Widths>("1").Value);
		Assert.Equal(5_000_000_000L, this.Serializer.Deserialize<Widths>("5000000000").Value);
		Assert.Equal("""{"$type":"Left","value":1}""", this.Serializer.Serialize(new Twins(new Left(1))));
	}

	[Test]
	public void Attribute_OverridesTheSerializerProperty()
	{
		JsonSerializer serializer = this.Serializer with { DefaultUnionTagging = UnionTagging.Auto };
		Assert.Equal("""{"$type":"Plot","x":1,"y":2}""", serializer.Serialize(new TaggedMeasure(new Plot(1, 2))));
	}

	[Test]
	public void Provider_OverridesTheAttribute()
	{
		JsonSerializer serializer = this.Serializer with
		{
			UnionTaggingProvider = new DelegateUnionTaggingProvider(union => union == typeof(TaggedMeasure) ? UnionTagging.Auto : null),
		};
		Assert.Equal("""{"x":1,"y":2}""", serializer.Serialize(new TaggedMeasure(new Plot(1, 2))));
		Assert.Equal("1", serializer.Serialize(new Widths(1)));
	}

	[Test]
	public void Alias_ForcesAWrapperInEveryMode()
	{
		JsonSerializer serializer = this.Serializer with
		{
			DefaultUnionTagging = UnionTagging.Untagged,
			UnionTypeAliasProvider = new UnionCaseMappingCollection([new(typeof(Measure)) { { "plot", typeof(Plot) } }]),
		};
		Assert.Equal("""{"$type":"plot","x":1,"y":2}""", serializer.Serialize(new Measure(new Plot(1, 2))));
		Assert.Equal(new Plot(1, 2), serializer.Deserialize<Measure>("""{"$type":"plot","x":1,"y":2}""").Value);
		Assert.Equal("5", serializer.Serialize(new Measure(5)));
	}

	[Test]
	public void DerivedTypeHierarchy_IgnoresTheCSharpUnionMode()
	{
		JsonSerializer serializer = this.Serializer with { DefaultUnionTagging = UnionTagging.Untagged };
		Assert.Equal("""{"$type":"Spaniel","barkVolume":10,"name":"Rover"}""", serializer.Serialize<KennelAnimal>(new Spaniel("Rover", 10)));
	}

	[GenerateShape]
	public partial union Measure(int, Plot);

	[GenerateShape, UnionTagging(UnionTagging.Tagged)]
	public partial union TaggedMeasure(int, Plot);

	[GenerateShape]
	public partial union Widths(int, long);

	[GenerateShape]
	public partial union Twins(Left, Right);

	[GenerateShape]
	public partial record Left(int Value);

	[GenerateShape]
	public partial record Right(int Value);

	[GenerateShape]
	public partial record Plot(int X, int Y);

	[GenerateShape, DerivedTypeShape(typeof(Spaniel))]
	public partial record KennelAnimal(string Name);

	[GenerateShape]
	public partial record Spaniel(string Name, int BarkVolume) : KennelAnimal(Name);
}
