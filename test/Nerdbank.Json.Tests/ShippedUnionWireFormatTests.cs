// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PolyType.Abstractions;

// Locks the shipped derived-type hierarchy wire format.
// F# union bytes are locked in samples/fs/Program.fs.
public class ShippedUnionWireFormatTests
{
	private readonly JsonSerializer serializer = new();

	[Test]
	public void Hierarchy_BaseCase_UsesBaseAlias()
		=> Assert.Equal("""{"$type":"$base","name":"Milo"}""", this.serializer.Serialize(new UnionAnimal { Name = "Milo" }));

	[Test]
	public void Hierarchy_DerivedCase_UsesTypeName()
		=> Assert.Equal("""{"$type":"UnionCow","weight":1400,"name":"Bessie"}""", this.serializer.Serialize<UnionAnimal>(new UnionCow { Name = "Bessie", Weight = 1400 }));

	[Test]
	public void Hierarchy_StringAlias_UsesConfiguredName()
		=> Assert.Equal("""{"$type":"cat","lives":9,"name":"Ada"}""", this.serializer.Serialize<StringAliasAnimal>(new StringAliasCat { Name = "Ada", Lives = 9 }));

	[Test]
	public void Hierarchy_IntegerAlias_UsesDecimalString()
		=> Assert.Equal("""{"$type":"1","lives":7,"name":"Otis"}""", this.serializer.Serialize<IntAliasAnimal>(new IntAliasCat { Name = "Otis", Lives = 7 }));

	[Test]
	public void Hierarchy_LossyDefault_OmitsDerivedProperties()
		=> Assert.Equal("""{"animals":[{"name":"Bessie"},{"name":"Lightning"},{"name":"Rover"}]}""", this.serializer.Serialize(new Farm()));

	[Test]
	public void Hierarchy_RuntimeMapping_MatchesGeneratedEnvelope()
	{
		JsonSerializer runtime = new()
		{
			DerivedTypeUnions = [new DerivedShapeMapping<Drawing> { { "circle", ShapeOf<Circle>() }, { "square", ShapeOf<Square>() } }],
		};
		Assert.Equal("""{"$type":"circle","radius":2.5}""", runtime.Serialize<Drawing>(new Circle(2.5)));
	}

	private static ITypeShape<T> ShapeOf<T>()
#if NET
		where T : IShapeable<T> => T.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T>();
#endif
}
