// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable DuckTyping // Tests or demonstrates the experimental union strategy.
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using PolyType.Abstractions;

public partial class JsonTargetedDeserializationTests : TestBase
{
	private const string Document = """{"inner":{"label":"hi","count":3},"numbers":[10,20,30],"map":{"a":1,"b":2},"name":"root","pet":["Cat",{"lives":9,"name":"Milo"}]}""";

	[Test]
	public void Options_AllInputForms()
	{
		JsonSerializer.DeserializePathOptions<Root, int> options = new(x => x.Numbers[1]);
		byte[] bytes = System.Text.Encoding.UTF8.GetBytes(Document);
		ReadOnlySequence<byte> sequence = CreateSequence(bytes.AsMemory(0, 17), bytes.AsMemory(17));
		JsonReader reader = new(bytes);
		Assert.Equal(20, this.Serializer.DeserializePath(ref reader, Shape<Root, Root>(), options));
		reader.ReadValueSeparator();
		Assert.Equal("30", reader.ReadNumberToken());
		Assert.Equal(20, this.Serializer.DeserializePath(bytes.AsMemory(), Shape<Root, Root>(), options));
		Assert.Equal(20, this.Serializer.DeserializePath(sequence, Shape<Root, Root>(), options));
		Assert.Equal(20, this.Serializer.DeserializePath(Document, Shape<Root, Root>(), options));
		using MemoryStream stream = new(bytes);
		Assert.Equal(20, this.Serializer.DeserializePath(stream, Shape<Root, Root>(), options));
		Assert.True(stream.CanRead);
		Assert.Equal(20, this.Serializer.DeserializePath<Root, int, Root>(bytes.AsMemory(), options));
		Assert.Equal(20, this.Serializer.DeserializePath<Root, int, Root>(sequence, options));
		Assert.Equal(20, this.Serializer.DeserializePath<Root, int, Root>(Document, options));
		stream.Position = 0;
		Assert.Equal(20, this.Serializer.DeserializePath<Root, int, Root>(stream, options));
		reader = new(bytes);
		Assert.Equal(20, this.Serializer.DeserializePath<Root, int, Root>(ref reader, options));
		Assert.Equal(20, this.Serializer.DeserializePath<Root, int>(Document, options));
	}

	[Test]
	public void RawPath_WitnessAndLegacyInputForms()
	{
		JsonPath path = JsonPath.Root.Member("numbers").Index(2);
		byte[] bytes = System.Text.Encoding.UTF8.GetBytes(Document);
		ReadOnlySequence<byte> sequence = CreateSequence(bytes.AsMemory(0, 25), bytes.AsMemory(25));
		JsonReader reader = new(bytes);
		Assert.Equal(30, this.Serializer.DeserializePath<int, Witness>(ref reader, path));
		Assert.Equal(30, this.Serializer.DeserializePath<int, Witness>(bytes.AsMemory(), path));
		Assert.Equal(30, this.Serializer.DeserializePath<int, Witness>(sequence, path));
		Assert.Equal(30, this.Serializer.DeserializePath<int, Witness>(Document, path));
		using MemoryStream stream = new(bytes);
		Assert.Equal(30, this.Serializer.DeserializePath<int, Witness>(stream, path));
		Assert.True(stream.CanRead);
		path = JsonPath.Root.Member("inner");
		Assert.Equal(new Inner("hi", 3), JsonSerializerExtensions.DeserializePath<Inner>(this.Serializer, Document, path));
		Assert.Equal(new Inner("hi", 3), JsonSerializerExtensions.DeserializePath<Inner>(this.Serializer, bytes.AsMemory(), path));
		Assert.Equal(new Inner("hi", 3), JsonSerializerExtensions.DeserializePath<Inner>(this.Serializer, sequence, path));
		stream.Position = 0;
		Assert.Equal(new Inner("hi", 3), JsonSerializerExtensions.DeserializePath<Inner>(this.Serializer, stream, path));
		reader = new(bytes);
		Assert.Equal(new Inner("hi", 3), JsonSerializerExtensions.DeserializePath<Inner>(this.Serializer, ref reader, path));
	}

	[Test]
	public void Options_MissingPath()
	{
		JsonSerializer.DeserializePathOptions<Root, int> options = new(x => x.Numbers[99]);
		Assert.Throws<JsonPathNotFoundException>(() => this.Serializer.DeserializePath(Document, Shape<Root, Root>(), options));
		Assert.Equal(0, this.Serializer.DeserializePath(Document, Shape<Root, Root>(), options with { DefaultForUndiscoverablePath = true }));
		Assert.Throws<ArgumentNullException>(() => this.Serializer.DeserializePath(Document, Shape<Root, Root>(), default(JsonSerializer.DeserializePathOptions<Root, int>)));
	}

	[Test]
	public void PreParsed_Property_DeserializesSubtree()
	{
		Inner? inner = this.Serializer.DeserializePath(Document, JsonPath.Root.Member("inner"), Shape<Inner, Inner>());
		Assert.Equal(new Inner("hi", 3), inner);
	}

	[Test]
	public void PreParsed_NestedProperty()
	{
		string? label = this.Serializer.DeserializePath(Document, JsonPath.Root.Member("inner").Member("label"), Shape<string, Witness>());
		Assert.Equal("hi", label);
	}

	[Test]
	public void PreParsed_ArrayIndex()
	{
		int value = this.Serializer.DeserializePath(Document, JsonPath.Root.Member("numbers").Index(1), Shape<int, Witness>());
		Assert.Equal(20, value);
	}

	[Test]
	public void PreParsed_DictionaryKey()
	{
		int value = this.Serializer.DeserializePath(Document, JsonPath.Root.Member("map").Member("b"), Shape<int, Witness>());
		Assert.Equal(2, value);
	}

	[Test]
	public void Expression_Property_TranslatesNamingPolicy()
	{
		string? label = this.Serializer.DeserializePath<Root, string>(Document, Shape<Root, Root>(), new(x => x.Inner.Label));
		Assert.Equal("hi", label);
	}

	[Test]
	public void Expression_ArrayIndex()
	{
		int value = this.Serializer.DeserializePath<Root, int>(Document, Shape<Root, Root>(), new(x => x.Numbers[2]));
		Assert.Equal(30, value);
	}

	[Test]
	public void Expression_DictionaryKey()
	{
		int value = this.Serializer.DeserializePath<Root, int>(Document, Shape<Root, Root>(), new(x => x.Map["a"]));
		Assert.Equal(1, value);
	}

	[Test]
	public void Missing_Throws()
	{
		JsonPathNotFoundException ex = Assert.Throws<JsonPathNotFoundException>(
			() => this.Serializer.DeserializePath(Document, JsonPath.Root.Member("inner").Member("missing"), Shape<int, Witness>()));
		Assert.Contains("missing", ex.Path, StringComparison.Ordinal);
	}

	[Test]
	public void Missing_ReturnsDefault()
	{
		int value = this.Serializer.DeserializePath(Document, JsonPath.Root.Member("absent"), Shape<int, Witness>(), MissingPathBehavior.ReturnDefault);
		Assert.Equal(0, value);
	}

	[Test]
	public void Missing_ArrayOutOfRange_ReturnsDefault()
	{
		int? value = this.Serializer.DeserializePath(Document, JsonPath.Root.Member("numbers").Index(9), Shape<int, Witness>(), MissingPathBehavior.ReturnDefault);
		Assert.Equal(0, value);
	}

	[Test]
	public void CaseInsensitive_MatchesMembers()
	{
		JsonSerializer serializer = new() { PropertyNameCaseInsensitive = true };
		string? label = serializer.DeserializePath(Document, JsonPath.Root.Member("INNER").Member("LABEL"), Shape<string, Witness>());
		Assert.Equal("hi", label);
	}

	[Test]
	public void Comments_AreSkippedDuringNavigation()
	{
		JsonSerializer serializer = new() { ReadCommentHandling = JsonCommentHandling.Skip };
		string json = """{/* a */"inner":{"label":"hi"/* b */},"name":"x"}""";
		string? label = serializer.DeserializePath(json, JsonPath.Root.Member("inner").Member("label"), Shape<string, Witness>());
		Assert.Equal("hi", label);
	}

	[Test]
	public void TrailingCommas_AreAllowedDuringNavigation()
	{
		JsonSerializer serializer = new() { AllowTrailingCommas = true };
		string json = """{"numbers":[10,20,],"name":"x",}""";
		int value = serializer.DeserializePath(json, JsonPath.Root.Member("numbers").Index(1), Shape<int, Witness>());
		Assert.Equal(20, value);
	}

	[Test]
	public void MultiSegmentInput()
	{
		byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(Document);
		ReadOnlySequence<byte> sequence = CreateSequence(utf8.AsMemory(0, 12), utf8.AsMemory(12, 20), utf8.AsMemory(32));
		int value = this.Serializer.DeserializePath(sequence, JsonPath.Root.Member("numbers").Index(0), Shape<int, Witness>());
		Assert.Equal(10, value);
	}

	[Test]
	public void UnionTarget_DeserializesEnvelope()
	{
		Animal? pet = this.Serializer.DeserializePath(Document, JsonPath.Root.Member("pet"), Shape<Animal, Animal>());
		Assert.Equal(new Cat("Milo", 9), pet);
	}

	[Test]
	public void ReferencePreservation_IsNotSupported()
	{
		JsonSerializer serializer = new() { PreserveReferences = ReferencePreservationMode.RejectCycles };
		Assert.Throws<NotSupportedException>(
			() => serializer.DeserializePath(Document, JsonPath.Root.Member("name"), Shape<string, Witness>()));
	}

	[Test]
	public void Expression_UnsupportedMethodCall_ThrowsEarly()
	{
		Assert.Throws<NotSupportedException>(
			() => this.Serializer.DeserializePath<Root, string>(Document, Shape<Root, Root>(), new(x => x.Name.Trim())));
	}

	[Test]
	public void Expression_DynamicIndex_ThrowsEarly()
	{
		int i = 1;
		Assert.Throws<NotSupportedException>(
			() => this.Serializer.DeserializePath<Root, int>(Document, Shape<Root, Root>(), new(x => x.Numbers[i])));
	}

	[Test]
	public void PreParsedPath_IsReusable()
	{
		JsonPath path = JsonPath.Root.Member("inner").Member("count");
		int first = this.Serializer.DeserializePath(Document, path, Shape<int, Witness>());
		int second = this.Serializer.DeserializePath(Document, path, Shape<int, Witness>());
		Assert.Equal(3, first);
		Assert.Equal(3, second);
	}

	[Test]
	public void IntermediateNull_IsMissing()
	{
		string json = """{"inner":null}""";
		int value = this.Serializer.DeserializePath(json, JsonPath.Root.Member("inner").Member("count"), Shape<int, Witness>(), MissingPathBehavior.ReturnDefault);
		Assert.Equal(0, value);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Expression_ThroughGeneratedUnion(bool useObjects)
	{
		JsonSerializer serializer = this.Serializer with { UseDiscriminatorObjects = useObjects };
		string json = useObjects ? """{"pet":{"Cat":{"lives":9,"name":"Milo"}}}""" : Document;
		string? name = serializer.DeserializePath<Root, string>(json, Shape<Root, Root>(), new(x => x.Pet.Name));
		Assert.Equal("Milo", name);
	}

	[Test]
	public void Expression_ThroughGeneratedUnion_MissingMember_ReturnsDefault()
	{
		string json = """{"inner":{"label":"hi","count":3},"numbers":[],"map":{},"name":"root","pet":["Cat",{"lives":9}]}""";
		string? name = this.Serializer.DeserializePath<Root, string>(json, Shape<Root, Root>(), new(x => x.Pet.Name) { DefaultForUndiscoverablePath = true });
		Assert.Null(name);
	}

	[Test]
	public void PreParsed_ThroughUnion_ThrowsClearError()
	{
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(
			() => this.Serializer.DeserializePath(Document, JsonPath.Root.Member("pet").Member("name"), Shape<string, Witness>()));
		NotSupportedException ex = Assert.IsType<NotSupportedException>(error.InnerException);
		Assert.Contains("expression-based", ex.Message, StringComparison.Ordinal);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Expression_ThroughRuntimeUnion(bool useObjects)
	{
		JsonSerializer serializer = new()
		{
			UseDiscriminatorObjects = useObjects,
			DerivedTypeUnions = [new DerivedShapeMapping<Shape2> { { "circle", Shape<Circle2, Circle2>() }, { "square", Shape<Square2, Square2>() } }],
		};

		string json = useObjects ? """{"shape":{"circle":{"kind":"c","radius":2.5}}}""" : """{"shape":["circle",{"kind":"c","radius":2.5}]}""";
		string? kind = serializer.DeserializePath<RuntimeHolder, string>(json, Shape<RuntimeHolder, RuntimeHolder>(), new(h => h.Shape.Kind));
		Assert.Equal("c", kind);
	}

	[Test]
	public void Expression_ThroughDuckUnion()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<Shape2, Shape2>(), Shape<Circle2, Circle2>(), Shape<Square2, Square2>())],
		};

		string json = """{"shape":{"kind":"c","radius":2.5}}""";
		string? kind = serializer.DeserializePath<RuntimeHolder, string>(json, Shape<RuntimeHolder, RuntimeHolder>(), new(h => h.Shape.Kind));
		Assert.Equal("c", kind);
	}

	[Test]
	public void Expression_ThroughCustomConverter_RemapsMember()
	{
		JsonNamingPolicy policy = JsonNamingPolicy.CamelCase;
		JsonSerializer serializer = new()
		{
			Converters = [new Point3Converter()],
			PropertyNamingPolicy = policy,
		};

		string json = serializer.Serialize(new CustomRoot(new Point3(3, 7)), Shape<CustomRoot, CustomRoot>());
		Assert.Equal("""{"point":[3,7]}""", json);

		int y = serializer.DeserializePath<CustomRoot, int>(json, Shape<CustomRoot, CustomRoot>(), new(r => r.Point.Y));
		int x = serializer.DeserializePath<CustomRoot, int>(json, Shape<CustomRoot, CustomRoot>(), new(r => r.Point.X));
		Assert.Equal(7, y);
		Assert.Equal(3, x);
	}

	[Test]
	public void Expression_ThroughCustomConverter_MissingElement_ReturnsDefault()
	{
		JsonNamingPolicy policy = JsonNamingPolicy.CamelCase;
		JsonSerializer serializer = new()
		{
			Converters = [new Point3Converter()],
			PropertyNamingPolicy = policy,
		};

		string json = """{"point":[]}""";
		int y = serializer.DeserializePath<CustomRoot, int>(json, Shape<CustomRoot, CustomRoot>(), new(r => r.Point.Y) { DefaultForUndiscoverablePath = true });
		Assert.Equal(0, y);
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

	[GenerateShape]
	internal partial record RuntimeHolder(Shape2 Shape);

	[GenerateShape]
	internal abstract partial record Shape2(string Kind);

	[GenerateShape]
	internal partial record Circle2(string Kind, double Radius) : Shape2(Kind);

	[GenerateShape]
	internal partial record Square2(string Kind, double Side) : Shape2(Kind);

	[GenerateShape]
	internal partial record CustomRoot(Point3 Point);

	[GenerateShape]
	internal partial record Point3(int X, int Y);

	[GenerateShapeFor<int>]
	[GenerateShapeFor<string>]
	internal partial class Witness;

	private sealed class Point3Converter : JsonConverter<Point3>
	{
		public override void Write(ref JsonWriter writer, in Point3? value, SerializationContext context)
		{
			writer.WriteStartArray();
			writer.Write(value!.X);
			writer.WriteValueSeparator();
			writer.Write(value.Y);
			writer.WriteEndArray();
		}

		public override Point3? Read(ref JsonReader reader, SerializationContext context)
		{
			reader.ReadStartArray();
			int x = int.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture);
			reader.ReadValueSeparator();
			int y = int.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture);
			reader.ReadEndArray();
			return new Point3(x, y);
		}

		public override bool SkipToPropertyValue(ref JsonReader reader, IPropertyShape propertyShape, SerializationContext context)
		{
			int target =
				propertyShape.Name == "X" ? 0 : propertyShape.Name == "Y" ? 1 : -1;
			if (target < 0)
			{
				return false;
			}

			reader.ReadStartArray();
			if (reader.TryReadEndArray())
			{
				return false;
			}

			for (int i = 0; i < target; i++)
			{
				reader.Skip(context);
				if (reader.TryReadEndArray())
				{
					return false;
				}

				reader.ReadValueSeparator();
			}

			return true;
		}
	}

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
