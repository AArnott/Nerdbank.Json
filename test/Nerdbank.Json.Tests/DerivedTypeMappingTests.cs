// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Text;

public partial class DerivedTypeMappingTests
{
	[Test]
	public void Mapping_RejectsDuplicateAliasesAndTypes()
	{
		DerivedShapeMapping<Base> mapping = new();
		mapping.Add(1, Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<Derived>());
		Assert.Throws<ArgumentException>(() => mapping.Add(1, Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<Other>()));
		Assert.Throws<ArgumentException>(() => mapping.Add("different", Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<Derived>()));
		Assert.Equal(1, mapping.Count);
		Assert.Throws<ArgumentException>(() => mapping.Add(default(DerivedTypeIdentifier), Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<Other>()));
	}

	[Test]
	public void Collection_FreezesMappingsAndRejectsDuplicateBases()
	{
		DerivedShapeMapping<Base> mapping = new() { { "derived", Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<Derived>() } };
		DerivedTypeUnionCollection collection = [mapping];
		Assert.Throws<InvalidOperationException>(() => mapping.Add("other", Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<Other>()));
		Assert.Throws<ArgumentException>(() => DerivedTypeUnionCollection.Create([mapping, DerivedTypeUnion.CreateDisabled(typeof(Base))]));
		Assert.Same(mapping, Assert.Single(collection));
		Assert.Single((IEnumerable)collection);
	}

	[Test]
	public void TypeMapping_ProviderAndIndexerRoundTrip()
	{
		DerivedTypeMapping<Base> mapping = new(Witness.GeneratedTypeShapeProvider)
		{
			[1] = typeof(Derived),
			["other"] = typeof(Other),
		};
		mapping[1] = typeof(Derived);
		Assert.Equal(typeof(Derived), mapping[1]);
		Assert.Throws<ArgumentException>(() => mapping.Add(3, typeof(string)));
		Assert.Throws<ArgumentException>(() => mapping["duplicate"] = typeof(Derived));
		JsonSerializer serializer = new() { DerivedTypeUnions = [mapping] };
		Assert.Equal(new Derived("hello"), serializer.Deserialize("""[1,{"name":"hello"}]""", Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<Base>()));
		Assert.Throws<InvalidOperationException>(() => mapping[1] = typeof(Other));
	}

	[Test]
	public void TypeMapping_IndexerReplacementReleasesOldType()
	{
		DerivedTypeMapping<Base> mapping = new(Witness.GeneratedTypeShapeProvider)
		{
			[1] = typeof(Derived),
		};
		mapping[1] = typeof(Other);
		mapping.Add("derived", typeof(Derived));
		Assert.Equal(typeof(Other), mapping[1]);
		Assert.Equal(typeof(Derived), mapping["derived"]);
	}

	[Test]
	public void ShapeMapping_ProviderAndWitnessOverloads()
	{
		DerivedShapeMapping<Base> mapping = new();
		mapping.Add<Derived>("derived", Witness.GeneratedTypeShapeProvider);
#if NET
		mapping.Add<Other, Witness>("other");
#else
		mapping.AddSourceGenerated<Other, Witness>("other");
#endif
		Assert.Equal(2, mapping.Count);
		Assert.Equal(typeof(Base), mapping.BaseType);
		Assert.False(mapping.Disabled);
		Assert.Equal(2, new List<KeyValuePair<DerivedTypeIdentifier, ITypeShape>>(mapping).Count);
	}

	[Test]
	public void DisabledUnion_DescribesBaseType()
	{
		DerivedTypeUnion disabled = DerivedTypeUnion.CreateDisabled(typeof(Base));
		Assert.True(disabled.Disabled);
		Assert.Equal(typeof(Base), disabled.BaseType);
	}

	[Test]
	public void Identifiers_PreserveKindAndUtf8()
	{
		DerivedTypeIdentifier number = 7;
		DerivedTypeIdentifier text = "7";
		Assert.NotEqual(number, text);
		Assert.Equal(7, number.IntAlias);
		Assert.Equal("7", text.StringAlias);
		Assert.Equal(Encoding.UTF8.GetBytes("7"), text.Utf8Alias.ToArray());
		Assert.Equal(new DerivedTypeIdentifier("7").GetHashCode(), text.GetHashCode());
		Assert.Throws<InvalidOperationException>(() => number.StringAlias);
		Assert.Throws<InvalidOperationException>(() => text.IntAlias);
		Assert.Throws<ArgumentNullException>(() => new DerivedTypeIdentifier(null!));
	}

	[GenerateShape]
	internal abstract partial record Base;

	[GenerateShape]
	internal partial record Derived(string Name) : Base;

	[GenerateShape]
	internal partial record Other(int Number) : Base;

	[GenerateShapeFor<Base>]
	[GenerateShapeFor<Derived>]
	[GenerateShapeFor<Other>]
	internal partial class Witness;
}
