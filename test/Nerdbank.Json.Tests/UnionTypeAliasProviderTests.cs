// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using PolyType.SourceGenerator;
using Scalar = CSharpUnionTests.Scalar;

public class UnionTypeAliasProviderTests : TestBase
{
	[Test]
	public void Mapping_CustomAndDefaultAliases()
	{
		UnionCaseMapping mapping = new(typeof(Scalar)) { { "number", typeof(int) } };
		this.Serializer = this.Serializer with { UnionTypeAliasProvider = new UnionCaseMappingCollection([mapping]) };
		Assert.Equal(42, this.Roundtrip(new Scalar(42))!.Value);
		Assert.Equal("""["number",42]""", this.LastSerializedJson);
		Assert.Equal("hi", this.Roundtrip(new Scalar("hi"))!.Value);
		Assert.Equal("""["String","hi"]""", this.LastSerializedJson);
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<Scalar>("""["Int32",42]"""));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Delegate_IntegerAlias(bool objects)
	{
		this.Serializer = this.Serializer with
		{
			UseDiscriminatorObjects = objects,
			UnionTypeAliasProvider = new DelegateUnionTypeAliasProvider((union, payload) =>
			{
				Assert.Equal(typeof(Scalar), union);
				return payload == typeof(int) ? new DerivedTypeIdentifier(7) : (DerivedTypeIdentifier?)null;
			}),
		};
		Assert.Equal(42, this.Roundtrip(new Scalar(42))!.Value);
		Assert.Equal(objects ? """{"7":42}""" : """[7,42]""", this.LastSerializedJson);
		string schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Scalar>()!).ToJsonString();
		Assert.Contains(objects ? "\"7\"" : "\"const\":7", schema);
		Assert.DoesNotContain("\"Int32\"", schema);
	}

	[Test]
	public void ConfigurationClone_RebuildsConverters()
	{
		Assert.Equal("""["Int32",42]""", this.Serializer.Serialize(new Scalar(42)));
		JsonSerializer customized = this.Serializer with
		{
			UnionTypeAliasProvider = new DelegateUnionTypeAliasProvider((_, payload) => payload == typeof(int) ? new DerivedTypeIdentifier("number") : (DerivedTypeIdentifier?)null),
		};
		Assert.Equal("""["number",42]""", customized.Serialize(new Scalar(42)));
		Assert.Equal("""["Int32",42]""", this.Serializer.Serialize(new Scalar(42)));
		Assert.Equal("""["Int32",42]""", (customized with { UnionTypeAliasProvider = null }).Serialize(new Scalar(42)));
	}

	[Test]
	public void Provider_ResolvedOnlyWhileBuildingConverters()
	{
		int calls = 0;
		this.Serializer = this.Serializer with
		{
			UnionTypeAliasProvider = new DelegateUnionTypeAliasProvider((_, _) =>
			{
				calls++;
				return null;
			}),
		};
		this.Serializer.Serialize(new Scalar(42));
		int initialCalls = calls;
		Assert.Equal(2, initialCalls);
		this.Serializer.Serialize(new Scalar("hi"));
		this.Serializer.Deserialize<Scalar>("""["Int32",42]""");
		Assert.Equal(initialCalls, calls);
	}

	[Test]
	public void Provider_DoesNotAffectTypeHierarchies()
	{
		this.Serializer = this.Serializer with
		{
			UnionTypeAliasProvider = new DelegateUnionTypeAliasProvider((_, _) => throw new InvalidOperationException("Not a C# union.")),
		};
		JsonObjectSerializerTests.Animal animal = new JsonObjectSerializerTests.Cat("Milo", 9);
		Assert.Equal("""["Cat",{"lives":9,"name":"Milo"}]""", this.Serializer.Serialize(animal));
		Assert.Contains("\"const\":\"Cat\"", this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<JsonObjectSerializerTests.Animal>()!).ToJsonString());
	}

	[Test]
	public void InvalidProviderAliases_AreRejected()
	{
		JsonSerializer duplicate = this.Serializer with { UnionTypeAliasProvider = new DelegateUnionTypeAliasProvider((_, _) => new DerivedTypeIdentifier("same")) };
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => duplicate.Serialize(new Scalar(42)));
		Assert.Contains("Duplicate alias", error.ToString());
		JsonSerializer uninitialized = this.Serializer with { UnionTypeAliasProvider = new DelegateUnionTypeAliasProvider((_, _) => default(DerivedTypeIdentifier)) };
		error = Assert.Throws<JsonSerializationException>(() => uninitialized.Serialize(new Scalar(42)));
		Assert.Contains("must be initialized", error.ToString());
	}

	[Test]
	public void AliasCollisionWithDefault_IsRejected()
	{
		UnionCaseMapping mapping = new(typeof(Scalar)) { { "String", typeof(int) } };
		this.Serializer = this.Serializer with { UnionTypeAliasProvider = new UnionCaseMappingCollection([mapping]) };
		Assert.Contains("Duplicate alias", Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(new Scalar(42))).ToString());
	}

	[Test]
	public void Mapping_NonMemberType_IsRejected()
	{
		UnionCaseMapping mapping = new(typeof(Scalar)) { { "other", typeof(DateTime) } };
		this.Serializer = this.Serializer with { UnionTypeAliasProvider = new UnionCaseMappingCollection([mapping]) };
		Assert.Contains("is not a case", Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(new Scalar(42))).ToString());
	}

	[Test]
	public void Mapping_ValidationAndFreezing()
	{
		Assert.Throws<ArgumentNullException>(() => new UnionCaseMapping(null!));
		Assert.Throws<ArgumentNullException>(() => new DelegateUnionTypeAliasProvider(null!));
		UnionCaseMapping mapping = new(typeof(Scalar));
		Assert.Throws<ArgumentNullException>(() => mapping.Add("x", null!));
		Assert.Throws<ArgumentException>(() => mapping.Add(default, typeof(int)));
		mapping.Add("number", typeof(int));
		Assert.Throws<ArgumentException>(() => mapping.Add("other", typeof(int)));
		Assert.Throws<ArgumentException>(() => mapping.Add("number", typeof(string)));
		UnionCaseMappingCollection collection = [mapping];
		Assert.Throws<InvalidOperationException>(() => mapping.Add("text", typeof(string)));
		Assert.Equal(1, collection.Count);
		Assert.Equal(1, mapping.Count);
		Assert.Same(mapping, Assert.Single(collection));
		Assert.Same(mapping, Assert.Single(((IEnumerable)collection).Cast<UnionCaseMapping>()));
		Assert.Equal(typeof(int), Assert.Single(((IEnumerable)mapping).Cast<KeyValuePair<Type, DerivedTypeIdentifier>>()).Key);
		Assert.Throws<ArgumentException>(() => new UnionCaseMappingCollection([mapping, new(typeof(Scalar))]));
		Assert.Throws<ArgumentNullException>(() => new UnionCaseMappingCollection([null!]));
	}

	[Test]
	public void Mapping_UnrelatedUnionRetainsDefaults()
	{
		this.Serializer = this.Serializer with { UnionTypeAliasProvider = new UnionCaseMappingCollection([new(typeof(CSharpUnionTests.Pet))]) };
		Assert.Equal(42, this.Roundtrip(new Scalar(42))!.Value);
		Assert.Equal("""["Int32",42]""", this.LastSerializedJson);
	}
}
