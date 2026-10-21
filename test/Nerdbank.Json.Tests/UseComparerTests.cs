// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using PolyType.Abstractions;

public partial class UseComparerTests : TestBase
{
	[Test]
	public async Task StaticAndInstanceProviders_ConfigureMutableAndParameterizedCollections()
	{
		const string Json = """{"headers":{"Accept":"json"},"tags":["Alpha"],"ordered":{"a":1,"b":2}}""";
		ProviderModel value = this.Serializer.Deserialize<ProviderModel>(Json)!;
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(Json));
		ProviderModel asyncValue = (await this.Serializer.DeserializeAsync(stream, TypeShapeResolver.ResolveDynamicOrThrow<ProviderModel>()))!;
		foreach (ProviderModel model in new[] { value, asyncValue })
		{
			Assert.Equal("json", model.Headers!["accept"]);
			Assert.Contains("alpha", model.Tags!);
			Assert.Equal(["b", "a"], model.Ordered!.Keys);
		}
	}

	[Test]
	public void NullProviderValue_Throws()
	{
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<NullProviderModel>("{}"));
	}

	[Test]
	public void Dictionary_MemberComparer_IsAppliedDuringDeserialization()
	{
		HeaderBag? bag = this.Serializer.Deserialize<HeaderBag>("""{"headers":{"Accept":"json"}}""");

		Assert.NotNull(bag?.Headers);
		Assert.Equal("json", bag.Headers["accept"]);
		Assert.Equal("json", bag.Headers["ACCEPT"]);
	}

	[Test]
	public void Dictionary_MemberComparer_TreatsCaseInsensitiveDuplicatesAsCollision()
	{
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<HeaderBag>("""{"headers":{"Accept":"a","ACCEPT":"b"}}"""));
	}

	[Test]
	public void Dictionary_MemberComparer_AppliesWhenGlobalComparerProviderIsNull()
	{
		JsonSerializer serializer = new() { ComparerProvider = null };

		HeaderBag? bag = serializer.Deserialize<HeaderBag>("""{"headers":{"Accept":"json"}}""");

		Assert.NotNull(bag?.Headers);
		Assert.Equal("json", bag.Headers["accept"]);
	}

	[Test]
	public void Dictionary_MemberComparer_DoesNotAffectSerialization()
	{
		HeaderBag bag = new() { Headers = new(StringComparer.Ordinal) { ["Accept"] = "json" } };

		string json = this.Serializer.Serialize(bag);

		Assert.Equal("""{"headers":{"Accept":"json"}}""", json);
	}

	[Test]
	public void Set_MemberComparer_IsAppliedDuringDeserialization()
	{
		TagSet? value = this.Serializer.Deserialize<TagSet>("""{"tags":["Alpha","BETA"]}""");

		Assert.NotNull(value?.Tags);
		Assert.Equal(2, value.Tags.Count);
		Assert.Contains("alpha", value.Tags);
		Assert.Contains("beta", value.Tags);
	}

	[Test]
	public void SortedDictionary_MemberComparer_UsesOrderComparer()
	{
		SortedModel? value = this.Serializer.Deserialize<SortedModel>("""{"ordered":{"a":1,"b":2,"c":3}}""");

		Assert.NotNull(value?.Ordered);
		Assert.Equal(["c", "b", "a"], value.Ordered.Keys);
	}

	[Test]
	public void ConstructorParameter_MemberComparer_IsApplied()
	{
		ConstructorModel? value = this.Serializer.Deserialize<ConstructorModel>("""{"headers":{"Accept":"json"}}""");

		Assert.NotNull(value?.Headers);
		Assert.Equal("json", value.Headers["ACCEPT"]);
	}

	[Test]
	public void TwoMembers_SameType_DifferentComparers()
	{
		TwoDictionaries? value = this.Serializer.Deserialize<TwoDictionaries>("""{"insensitive":{"Accept":"a"},"sensitive":{"Accept":"b"}}""");

		Assert.NotNull(value);
		Assert.NotNull(value.Insensitive);
		Assert.NotNull(value.Sensitive);
		Assert.Equal("a", value.Insensitive["accept"]);
		Assert.False(value.Sensitive.ContainsKey("accept"));
		Assert.Equal("b", value.Sensitive["Accept"]);
	}

	[Test]
	public void Attribute_OnNonCollectionMember_Throws()
	{
		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<InvalidScalarModel>("""{"name":"Ada"}"""));
		Assert.Contains(nameof(UseComparerAttribute), exception.Message, StringComparison.Ordinal);
	}

	[Test]
	public void Attribute_WithIncompatibleComparer_Throws()
	{
		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<IncompatibleComparerModel>("""{"headers":{"a":"b"}}"""));
		Assert.Contains("IEqualityComparer", exception.Message, StringComparison.Ordinal);
	}

	[GenerateShape]
	internal partial class HeaderBag
	{
		[UseComparer(typeof(CaseInsensitiveStringComparer))]
		public Dictionary<string, string>? Headers { get; set; }
	}

	[GenerateShape]
	internal partial class TagSet
	{
		[UseComparer(typeof(CaseInsensitiveStringComparer))]
		public HashSet<string>? Tags { get; set; }
	}

	[GenerateShape]
	internal partial class SortedModel
	{
		[UseComparer(typeof(DescendingStringComparer))]
		public SortedDictionary<string, int>? Ordered { get; set; }
	}

	[GenerateShape]
	internal partial class ConstructorModel([UseComparer(typeof(CaseInsensitiveStringComparer))] Dictionary<string, string> headers)
	{
		public Dictionary<string, string> Headers { get; } = headers;
	}

	[GenerateShape]
	internal partial class TwoDictionaries
	{
		[UseComparer(typeof(CaseInsensitiveStringComparer))]
		public Dictionary<string, string>? Insensitive { get; set; }

		public Dictionary<string, string>? Sensitive { get; set; }
	}

	[GenerateShape]
	internal partial class InvalidScalarModel
	{
		[UseComparer(typeof(CaseInsensitiveStringComparer))]
		public string? Name { get; set; }
	}

	[GenerateShape]
	internal partial class IncompatibleComparerModel
	{
		[UseComparer(typeof(DescendingStringComparer))]
		public Dictionary<string, string>? Headers { get; set; }
	}

	[GenerateShape]
	internal partial class ProviderModel
	{
		[UseComparer(typeof(StringComparer), nameof(StringComparer.OrdinalIgnoreCase))]
		public Dictionary<string, string>? Headers { get; set; }

		[UseComparer(typeof(ComparerProvider), nameof(ComparerProvider.EqualityComparer))]
		public ImmutableHashSet<string>? Tags { get; set; }

		[UseComparer(typeof(ComparerProvider), nameof(ComparerProvider.OrderComparer))]
		public ImmutableSortedDictionary<string, int>? Ordered { get; set; }
	}

	[GenerateShape]
	internal partial class NullProviderModel
	{
		[UseComparer(typeof(ComparerProvider), nameof(ComparerProvider.Null))]
		public Dictionary<string, string>? Headers { get; set; }
	}

	internal sealed class ComparerProvider
	{
		public static IEqualityComparer<string>? Null => null;

		public IEqualityComparer<string> EqualityComparer => StringComparer.OrdinalIgnoreCase;

		public IComparer<string> OrderComparer => new DescendingStringComparer();
	}

	internal sealed class CaseInsensitiveStringComparer : IEqualityComparer<string>
	{
		public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);

		public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
	}

	internal sealed class DescendingStringComparer : IComparer<string>
	{
		public int Compare(string? x, string? y) => StringComparer.Ordinal.Compare(y, x);
	}
}
