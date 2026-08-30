// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Generic;
using PolyType.Abstractions;

public partial class JsonTargetedDeserializationTests : TestBase
{
	private const string Document = """{"inner":{"label":"hi","count":3},"numbers":[10,20,30],"map":{"a":1,"b":2},"name":"root","pet":["Cat",{"lives":9,"name":"Milo"}]}""";

	[Test]
	public void PreParsed_Property_DeserializesSubtree()
	{
		Inner? inner = this.Serializer.DeserializeAt(Document, JsonPath.Root.Member("inner"), Shape<Inner, Inner>());
		Assert.Equal(new Inner("hi", 3), inner);
	}

	[Test]
	public void PreParsed_NestedProperty()
	{
		string? label = this.Serializer.DeserializeAt(Document, JsonPath.Root.Member("inner").Member("label"), Shape<string, Witness>());
		Assert.Equal("hi", label);
	}

	[Test]
	public void PreParsed_ArrayIndex()
	{
		int value = this.Serializer.DeserializeAt(Document, JsonPath.Root.Member("numbers").Index(1), Shape<int, Witness>());
		Assert.Equal(20, value);
	}

	[Test]
	public void PreParsed_DictionaryKey()
	{
		int value = this.Serializer.DeserializeAt(Document, JsonPath.Root.Member("map").Member("b"), Shape<int, Witness>());
		Assert.Equal(2, value);
	}

	[Test]
	public void Expression_Property_TranslatesNamingPolicy()
	{
		string? label = this.Serializer.DeserializeAt<Root, string>(Document, x => x.Inner.Label, Shape<Root, Root>());
		Assert.Equal("hi", label);
	}

	[Test]
	public void Expression_ArrayIndex()
	{
		int value = this.Serializer.DeserializeAt<Root, int>(Document, x => x.Numbers[2], Shape<Root, Root>());
		Assert.Equal(30, value);
	}

	[Test]
	public void Expression_DictionaryKey()
	{
		int value = this.Serializer.DeserializeAt<Root, int>(Document, x => x.Map["a"], Shape<Root, Root>());
		Assert.Equal(1, value);
	}

	[Test]
	public void Missing_Throws()
	{
		JsonPathNotFoundException ex = Assert.Throws<JsonPathNotFoundException>(
			() => this.Serializer.DeserializeAt(Document, JsonPath.Root.Member("inner").Member("missing"), Shape<int, Witness>()));
		Assert.Contains("missing", ex.Path, StringComparison.Ordinal);
	}

	[Test]
	public void Missing_ReturnsDefault()
	{
		int value = this.Serializer.DeserializeAt(Document, JsonPath.Root.Member("absent"), Shape<int, Witness>(), MissingPathBehavior.ReturnDefault);
		Assert.Equal(0, value);
	}

	[Test]
	public void Missing_ArrayOutOfRange_ReturnsDefault()
	{
		int? value = this.Serializer.DeserializeAt(Document, JsonPath.Root.Member("numbers").Index(9), Shape<int, Witness>(), MissingPathBehavior.ReturnDefault);
		Assert.Equal(0, value);
	}

	[Test]
	public void CaseInsensitive_MatchesMembers()
	{
		JsonSerializer serializer = new() { PropertyNameCaseInsensitive = true };
		string? label = serializer.DeserializeAt(Document, JsonPath.Root.Member("INNER").Member("LABEL"), Shape<string, Witness>());
		Assert.Equal("hi", label);
	}

	[Test]
	public void Comments_AreSkippedDuringNavigation()
	{
		JsonSerializer serializer = new() { ReadCommentHandling = JsonCommentHandling.Skip };
		string json = """{/* a */"inner":{"label":"hi"/* b */},"name":"x"}""";
		string? label = serializer.DeserializeAt(json, JsonPath.Root.Member("inner").Member("label"), Shape<string, Witness>());
		Assert.Equal("hi", label);
	}

	[Test]
	public void TrailingCommas_AreAllowedDuringNavigation()
	{
		JsonSerializer serializer = new() { AllowTrailingCommas = true };
		string json = """{"numbers":[10,20,],"name":"x",}""";
		int value = serializer.DeserializeAt(json, JsonPath.Root.Member("numbers").Index(1), Shape<int, Witness>());
		Assert.Equal(20, value);
	}

	[Test]
	public void MultiSegmentInput()
	{
		byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(Document);
		ReadOnlySequence<byte> sequence = CreateSequence(utf8.AsMemory(0, 12), utf8.AsMemory(12, 20), utf8.AsMemory(32));
		int value = this.Serializer.DeserializeAt(sequence, JsonPath.Root.Member("numbers").Index(0), Shape<int, Witness>());
		Assert.Equal(10, value);
	}

	[Test]
	public void UnionTarget_DeserializesEnvelope()
	{
		Animal? pet = this.Serializer.DeserializeAt(Document, JsonPath.Root.Member("pet"), Shape<Animal, Animal>());
		Assert.Equal(new Cat("Milo", 9), pet);
	}

	[Test]
	public void ReferencePreservation_IsNotSupported()
	{
		JsonSerializer serializer = new() { PreserveReferences = ReferencePreservationMode.RejectCycles };
		Assert.Throws<NotSupportedException>(
			() => serializer.DeserializeAt(Document, JsonPath.Root.Member("name"), Shape<string, Witness>()));
	}

	[Test]
	public void Expression_UnsupportedMethodCall_ThrowsEarly()
	{
		Assert.Throws<NotSupportedException>(
			() => this.Serializer.DeserializeAt<Root, string>(Document, x => x.Name.Trim(), Shape<Root, Root>()));
	}

	[Test]
	public void Expression_DynamicIndex_ThrowsEarly()
	{
		int i = 1;
		Assert.Throws<NotSupportedException>(
			() => this.Serializer.DeserializeAt<Root, int>(Document, x => x.Numbers[i], Shape<Root, Root>()));
	}

	[Test]
	public void PreParsedPath_IsReusable()
	{
		JsonPath path = JsonPath.Root.Member("inner").Member("count");
		int first = this.Serializer.DeserializeAt(Document, path, Shape<int, Witness>());
		int second = this.Serializer.DeserializeAt(Document, path, Shape<int, Witness>());
		Assert.Equal(3, first);
		Assert.Equal(3, second);
	}

	[Test]
	public void IntermediateNull_IsMissing()
	{
		string json = """{"inner":null}""";
		int value = this.Serializer.DeserializeAt(json, JsonPath.Root.Member("inner").Member("count"), Shape<int, Witness>(), MissingPathBehavior.ReturnDefault);
		Assert.Equal(0, value);
	}

	private static ITypeShape<T> Shape<T, TProvider>()
#if NET
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#endif

	private static ReadOnlySequence<byte> CreateSequence(params ReadOnlyMemory<byte>[] segments)
	{
		Segment? first = null;
		Segment? last = null;
		foreach (ReadOnlyMemory<byte> segment in segments)
		{
			Segment current = new(segment);
			if (first is null)
			{
				first = current;
			}
			else
			{
				last!.SetNext(current);
			}

			last = current;
		}

		return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
	}

	[GenerateShape]
	internal partial record Root(Inner Inner, int[] Numbers, Dictionary<string, int> Map, string Name, Animal Pet);

	[GenerateShape]
	internal partial record Inner(string Label, int Count);

	[GenerateShape]
	[DerivedTypeShape(typeof(Cat))]
	internal partial record Animal(string Name);

	[GenerateShape]
	internal partial record Cat(string Name, int Lives) : Animal(Name);

	[GenerateShapeFor<int>]
	[GenerateShapeFor<string>]
	internal partial class Witness;

	private sealed class Segment : ReadOnlySequenceSegment<byte>
	{
		internal Segment(ReadOnlyMemory<byte> memory) => this.Memory = memory;

		internal void SetNext(Segment next)
		{
			next.RunningIndex = this.RunningIndex + this.Memory.Length;
			this.Next = next;
		}
	}
}
