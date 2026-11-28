// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using PolyType.SourceGenerator;

public partial class UnionDuckTypingTests : TestBase
{
	[Test]
	public void FirstDeclared_UsesRangeForNumbers()
	{
		Assert.Equal("5", this.Serializer.Serialize(new Sizes(5L)));
		Assert.Equal(5, this.Serializer.Deserialize<Sizes>("5").Value);
		Assert.Equal("5000000000", this.Serializer.Serialize(new Sizes(5_000_000_000)));
		Assert.Equal(5_000_000_000L, this.Serializer.Deserialize<Sizes>("5000000000").Value);
	}

	[Test]
	public void StringBeatsGuidUnlessAClassifierSaysOtherwise()
	{
		string guid = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
		Assert.Equal(guid, this.Serializer.Deserialize<PlainId>($"\"{guid}\"").Value);
		Assert.Equal(Guid.Parse(guid), this.Serializer.Deserialize<ClassifiedId>($"\"{guid}\"").Value);
		Assert.Equal("abc", this.Serializer.Deserialize<ClassifiedId>("\"abc\"").Value);
	}

	[Test]
	public void Tagged_DoesNotDuckTypeOrCallTheClassifier()
	{
		JsonSerializer tagged = this.Serializer with { DefaultUnionTagging = UnionTagging.Tagged };
		string guid = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
		JsonSerializationException ex = Assert.Throws<JsonSerializationException>(() => tagged.Deserialize<ClassifiedId>($"\"{guid}\""));
		Assert.Equal(JsonSerializationException.ErrorCode.UnionTagRequired, ex.Code);
		Assert.Equal("""{"$type":"Int32","$value":5}""", tagged.Serialize(new Sizes(5)));
	}

	[Test]
	public void Discriminator_SelectsTheCaseAndIsOptional()
	{
		Assert.Equal("""{"kind":"create","uri":"file:///a"}""", this.Serializer.Serialize(new Change(new Create("file:///a"))));
		Create created = Assert.IsType<Create>(this.Serializer.Deserialize<Change>("""{"kind":"create","uri":"file:///a"}""").Value);
		Assert.Equal("file:///a", created.Uri);
		Assert.Equal("""{"name":"n","parts":[]}""", this.Serializer.Serialize(new Change(new Edit("n", []))));
		Assert.IsType<Edit>(this.Serializer.Deserialize<Change>("""{"name":"n","parts":[]}""").Value);

		JsonSerializationException ambiguous = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<Change>("""{"uri":"file:///a"}"""));
		Assert.Equal(JsonSerializationException.ErrorCode.AmbiguousUnionCase, ambiguous.Code);
		JsonSerializationException missing = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<Change>("""{"kind":"nope","uri":"file:///a"}"""));
		Assert.Equal(JsonSerializationException.ErrorCode.NoMatchingUnionCase, missing.Code);
	}

	[Test]
	public void Discriminator_OutOfOrderRequiresTheSetting()
	{
		JsonSerializationException rejected = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<Change>("""{"uri":"file:///a","kind":"create"}"""));
		Assert.Equal(JsonSerializationException.ErrorCode.AmbiguousUnionCase, rejected.Code);

		JsonSerializer allowed = this.Serializer with { AllowOutOfOrderDiscriminator = true };
		Create created = Assert.IsType<Create>(allowed.Deserialize<Change>("""{"uri":"file:///a","kind":"create"}""").Value);
		Assert.Equal("file:///a", created.Uri);
	}

	[Test]
	public void RequiredProperties_MostSpecificWinsAndCatchAllIsLast()
	{
		Assert.Equal("""{"range":"r","newText":"x"}""", this.Serializer.Serialize(new TextChange(new PlainEdit("r", "x"))));
		Assert.IsType<PlainEdit>(this.Serializer.Deserialize<TextChange>("""{"range":"r","newText":"x"}""").Value);
		Assert.IsType<NotedEdit>(this.Serializer.Deserialize<TextChange>("""{"range":"r","newText":"x","note":"n"}""").Value);

		Assert.Equal("""{"radius":2}""", this.Serializer.Serialize(new Picture(new Dot(2))));
		Assert.Equal("{}", this.Serializer.Serialize(new Picture(new Marker())));
		Assert.IsType<Dot>(this.Serializer.Deserialize<Picture>("""{"radius":2}""").Value);
		Assert.IsType<Marker>(this.Serializer.Deserialize<Picture>("{}").Value);
	}

	[Test]
	public void ElementElimination_NarrowsCollectionCases()
	{
		Assert.IsType<List<int>>(this.Serializer.Deserialize<Lists>("[5,6]").Value);
		Assert.IsType<List<int>>(this.Serializer.Deserialize<Lists>("[]").Value);
		List<string> text = Assert.IsType<List<string>>(this.Serializer.Deserialize<Lists>("""[null,"a"]""").Value);
		Assert.Null(text[0]);
		Assert.Equal("a", text[1]);

		List<int?> numbers = Assert.IsType<List<int?>>(this.Serializer.Deserialize<NullableLists>("[null,5]").Value);
		Assert.Null(numbers[0]);
		Assert.Equal(5, numbers[1]);

		List<List<int>> emptyNested = Assert.IsType<List<List<int>>>(this.Serializer.Deserialize<NestedLists>("[[]]").Value);
		Assert.Empty(Assert.Single(emptyNested));
		List<List<string>> nestedText = Assert.IsType<List<List<string>>>(this.Serializer.Deserialize<NestedLists>("""[[],["a"]]""").Value);
		Assert.Empty(nestedText[0]);
		Assert.Equal("a", Assert.Single(nestedText[1]));
	}

	[Test]
	public void NestedUnion_FlattensNonRecursiveCases()
	{
		Assert.Equal("\"hi\"", this.Serializer.Serialize(new Wrapper(new Inner("hi"))));
		Assert.Equal("5", this.Serializer.Serialize(new Wrapper(5)));
		Assert.Equal("""{"x":1,"y":2}""", this.Serializer.Serialize(new Wrapper(new Inner(new PlotPoint(1, 2)))));

		Assert.Equal("hi", Assert.IsType<Inner>(this.Serializer.Deserialize<Wrapper>("\"hi\"").Value).Value);
		Assert.Equal(5, this.Serializer.Deserialize<Wrapper>("5").Value);
		Inner point = Assert.IsType<Inner>(this.Serializer.Deserialize<Wrapper>("""{"x":1,"y":2}""").Value);
		Assert.Equal(new PlotPoint(1, 2), point.Value);
	}

	[Test]
	public void Classifier_RejectsABadTypeAnExceptionAndAnUnknownCase()
	{
		JsonSerializationException badType = Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(new BadClassifier("x")));
		Assert.IsType<NotSupportedException>(badType.InnerException);

		JsonSerializationException thrown = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<ThrowingId>("\"abc\""));
		Assert.IsType<InvalidOperationException>(thrown.InnerException);

		JsonSerializationException unknown = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<UnknownCaseId>("\"abc\""));
		Assert.Contains("Int32", unknown.Message, StringComparison.Ordinal);
	}

	[Test]
	public async Task AsyncRead_ClassifiesABufferedValue()
	{
		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, new Sizes(5_000_000_000));
		Assert.Equal("5000000000", Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		Assert.Equal(5_000_000_000L, (await this.Serializer.DeserializeAsync<Sizes>(stream)).Value);

		using MemoryStream change = new();
		await this.Serializer.SerializeAsync(change, new Change(new Create("file:///a")));
		change.Position = 0;
		Assert.Equal("file:///a", Assert.IsType<Create>((await this.Serializer.DeserializeAsync<Change>(change)).Value).Uri);
	}

	[GenerateShape]
	public partial union Sizes(int, long);

	[GenerateShape]
	public partial union PlainId(string, Guid);

	[GenerateShape, UnionClassifier(typeof(GuidStringClassifier))]
	public partial union ClassifiedId(string, Guid);

	[GenerateShape, UnionClassifier(typeof(ThrowingClassifier))]
	public partial union ThrowingId(string, Guid);

	[GenerateShape, UnionClassifier(typeof(UnknownCaseClassifier))]
	public partial union UnknownCaseId(string, Guid);

	[GenerateShape, UnionClassifier(typeof(string))]
	public partial union BadClassifier(string, int);

	[GenerateShape]
	public partial union Change(Edit, Create, Delete);

	[GenerateShape]
	public partial union TextChange(PlainEdit, NotedEdit);

	[GenerateShape]
	public partial union Picture(Dot, Marker);

	[GenerateShape]
	public partial union Lists(List<int>, List<string>);

	[GenerateShape]
	public partial union NullableLists(List<int?>, List<string>);

	[GenerateShape]
	public partial union NestedLists(List<List<int>>, List<List<string>>);

	[GenerateShape]
	public partial union Wrapper(int, Inner);

	[GenerateShape]
	public partial union Inner(string, PlotPoint);

	public sealed class GuidStringClassifier : JsonUnionClassifier
	{
		public override Type? Classify(ref JsonReader reader, UnionClassifierContext context)
		{
			if (reader.NextJsonType == JsonValueKind.String && Guid.TryParse(reader.ReadString(), out _))
			{
				return typeof(Guid);
			}

			return null;
		}
	}

	public sealed class ThrowingClassifier : JsonUnionClassifier
	{
		public override Type? Classify(ref JsonReader reader, UnionClassifierContext context)
			=> throw new InvalidOperationException("boom");
	}

	public sealed class UnknownCaseClassifier : JsonUnionClassifier
	{
		public override Type? Classify(ref JsonReader reader, UnionClassifierContext context)
			=> typeof(int);
	}

	public partial record Create(string Uri)
	{
		[UnionDiscriminator("create")]
		public string Kind => "create";
	}

	public partial record Delete(string Uri)
	{
		[UnionDiscriminator("delete")]
		public string Kind => "delete";
	}

	public partial record Edit(string Name, string[] Parts);

	public partial record PlainEdit(string Range, string NewText);

	public partial record NotedEdit(string Range, string NewText, string Note);

	public partial record Dot(int Radius);

	public partial record Marker();

	public partial record PlotPoint(int X, int Y);
}
