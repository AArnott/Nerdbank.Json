// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PolyType.Abstractions;

public partial class JsonRuntimeUnionTests : TestBase
{
	[Test]
	public void AddUnion_ForBaseWithoutGeneratedUnion_RoundTrips()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Shape2>.Create()
					.AddCase("circle", Shape<Circle, Circle>())
					.AddCase("square", Shape<Square, Square>())),
		};

		Shape2 value = new Circle(2.5);
		string json = serializer.Serialize(value, Shape<Shape2, Shape2>());
		Assert.Equal("""["circle",{"radius":2.5}]""", json);

		Shape2? result = serializer.Deserialize(json, Shape<Shape2, Shape2>());
		Assert.Equal(value, result);
	}

	[Test]
	public void AddUnion_IntegerTag_RoundTrips()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Shape2>.Create()
					.AddCase(1, Shape<Circle, Circle>())
					.AddCase(2, Shape<Square, Square>())),
		};

		Shape2 value = new Square(4);
		string json = serializer.Serialize(value, Shape<Shape2, Shape2>());
		Assert.Equal("""[2,{"side":4}]""", json);
		Assert.Equal(value, serializer.Deserialize(json, Shape<Shape2, Shape2>()));
	}

	[Test]
	public void ReplaceGeneratedUnion_UsesRuntimeAliases()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Animal2>.Create().AddCase("feline", Shape<Cat2, Cat2>())),
		};

		Animal2 value = new Cat2("Milo", 9);
		string json = serializer.Serialize(value, Shape<Animal2, Animal2>());
		Assert.Equal("""["feline",{"lives":9,"name":"Milo"}]""", json);
		Assert.Equal(value, serializer.Deserialize(json, Shape<Animal2, Animal2>()));
	}

	[Test]
	public void ExtendGeneratedUnion_AddsRuntimeCase()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Animal2>.Create().IncludeGenerated().AddCase("dog", Shape<Dog2, Dog2>())),
		};

		Animal2 cat = new Cat2("Milo", 9);
		Animal2 dog = new Dog2("Rex", "Lab");

		Assert.Equal("""["Cat2",{"lives":9,"name":"Milo"}]""", serializer.Serialize(cat, Shape<Animal2, Animal2>()));
		Assert.Equal("""["dog",{"breed":"Lab","name":"Rex"}]""", serializer.Serialize(dog, Shape<Animal2, Animal2>()));

		Assert.Equal(dog, serializer.Deserialize("""["dog",{"breed":"Lab","name":"Rex"}]""", Shape<Animal2, Animal2>()));
		Assert.Equal(cat, serializer.Deserialize("""["Cat2",{"lives":9,"name":"Milo"}]""", Shape<Animal2, Animal2>()));
	}

	[Test]
	public void DisableUnion_SerializesAsBase()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithoutUnion<Animal2>(),
		};

		Animal2 value = new Animal2("Ada");
		string json = serializer.Serialize(value, Shape<Animal2, Animal2>());
		Assert.Equal("""{"name":"Ada"}""", json);
		Assert.Equal(value, serializer.Deserialize(json, Shape<Animal2, Animal2>()));
	}

	[Test]
	public void DefaultBehavior_Unchanged_WhenNoConfiguration()
	{
		JsonSerializer serializer = new();

		Animal2 value = new Cat2("Milo", 9);
		Assert.Equal("""["Cat2",{"lives":9,"name":"Milo"}]""", serializer.Serialize(value, Shape<Animal2, Animal2>()));
	}

	[Test]
	public void UnknownDiscriminator_Throws()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Shape2>.Create().AddCase("circle", Shape<Circle, Circle>())),
		};

		Assert.Throws<FormatException>(() => serializer.Deserialize("""["square",{"side":4}]""", Shape<Shape2, Shape2>()));
	}

	[Test]
	public void UnionProperty_UsesRuntimeConfiguration()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Shape2>.Create().AddCase("circle", Shape<Circle, Circle>())),
		};

		ShapeContainer value = new() { Shape = new Circle(1) };
		string json = serializer.Serialize(value, Shape<ShapeContainer, ShapeContainer>());
		Assert.Equal("""{"shape":["circle",{"radius":1}]}""", json);

		ShapeContainer? result = serializer.Deserialize(json, Shape<ShapeContainer, ShapeContainer>());
		Assert.Equal(new Circle(1), result?.Shape);
	}

	[Test]
	public void DuckTyping_RoundTrips_WithoutDiscriminator()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Shape2>.Create()
					.AddCase("circle", Shape<Circle, Circle>())
					.AddCase("square", Shape<Square, Square>())
					.UseDuckTyping()),
		};

		Shape2 value = new Circle(2.5);
		string json = serializer.Serialize(value, Shape<Shape2, Shape2>());
		Assert.Equal("""{"radius":2.5}""", json);

		Shape2? result = serializer.Deserialize(json, Shape<Shape2, Shape2>());
		Assert.Equal(value, result);
		Assert.Equal(new Square(4), serializer.Deserialize("""{"side":4}""", Shape<Shape2, Shape2>()));
	}

	[Test]
	public void DuckTyping_AmbiguousInput_Throws()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Overlap>.Create()
					.AddCase("a", Shape<OverlapA, OverlapA>())
					.AddCase("b", Shape<OverlapB, OverlapB>())
					.UseDuckTyping()),
		};

		FormatException ex = Assert.Throws<FormatException>(() => serializer.Deserialize("""{"shared":1,"a":2,"b":3}""", Shape<Overlap, Overlap>()));
		Assert.Contains("ambiguous", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Test]
	public void DuckTyping_InsufficientEvidence_Throws()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Shape2>.Create()
					.AddCase("circle", Shape<Circle, Circle>())
					.AddCase("square", Shape<Square, Square>())
					.UseDuckTyping()),
		};

		FormatException ex = Assert.Throws<FormatException>(() => serializer.Deserialize("""{"unrelated":1}""", Shape<Shape2, Shape2>()));
		Assert.Contains("insufficient", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Test]
	public void DuckTyping_CaseWithoutRequiredProperties_Throws()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Shape2>.Create()
					.AddCase("empty", Shape<EmptyShape, EmptyShape>())
					.UseDuckTyping()),
		};

		Assert.Throws<NotSupportedException>(() => serializer.Deserialize("""{}""", Shape<Shape2, Shape2>()));
	}

	[Test]
	public void MultiLevelUnion_NestedRuntimeUnions_RoundTrip()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default
				.WithUnion(JsonUnion<Wrapper>.Create().AddCase("shape", Shape<ShapeWrapper, ShapeWrapper>()))
				.WithUnion(JsonUnion<Shape2>.Create().AddCase("circle", Shape<Circle, Circle>())),
		};

		Wrapper value = new ShapeWrapper(new Circle(1));
		string json = serializer.Serialize(value, Shape<Wrapper, Wrapper>());
		Assert.Equal("""["shape",{"inner":["circle",{"radius":1}]}]""", json);
		Assert.Equal(value, serializer.Deserialize(json, Shape<Wrapper, Wrapper>()));
	}

	[Test]
	public void UnionCase_UsesRegisteredCustomConverter()
	{
		JsonSerializer serializer = new()
		{
			Converters = new ConverterCollection([new CircleConverter()]),
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Shape2>.Create().AddCase("circle", Shape<Circle, Circle>())),
		};

		Shape2 value = new Circle(3);
		string json = serializer.Serialize(value, Shape<Shape2, Shape2>());
		Assert.Equal("""["circle",{"r":3}]""", json);
		Assert.Equal(value, serializer.Deserialize(json, Shape<Shape2, Shape2>()));
	}

	[Test]
	public void DuckTyping_HonorsAllowTrailingCommas()
	{
		JsonSerializer serializer = DuckSerializer() with { AllowTrailingCommas = true };

		Shape2? result = serializer.Deserialize("""{"radius":2.5,}""", Shape<Shape2, Shape2>());
		Assert.Equal(new Circle(2.5), result);
	}

	[Test]
	public void DuckTyping_TrailingComma_ThrowsWhenDisabled()
	{
		JsonSerializer serializer = DuckSerializer();

		Assert.Throws<FormatException>(() => serializer.Deserialize("""{"radius":2.5,}""", Shape<Shape2, Shape2>()));
	}

	[Test]
	public void DuckTyping_HonorsCommentSkip()
	{
		JsonSerializer serializer = DuckSerializer() with { ReadCommentHandling = JsonCommentHandling.Skip };

		string json =
			"""
			{/* which */ "radius": 2.5 // trailing
			}
			""";

		Shape2? result = serializer.Deserialize(json, Shape<Shape2, Shape2>());
		Assert.Equal(new Circle(2.5), result);
	}

	[Test]
	public void DuckTyping_Comments_ThrowWhenDisabled()
	{
		JsonSerializer serializer = DuckSerializer();

		Assert.Throws<FormatException>(() => serializer.Deserialize("""{/* c */"radius":2.5}""", Shape<Shape2, Shape2>()));
	}

	private static JsonSerializer DuckSerializer() => new()
	{
		Unions = JsonUnionConfiguration.Default.WithUnion(
			JsonUnion<Shape2>.Create()
				.AddCase("circle", Shape<Circle, Circle>())
				.AddCase("square", Shape<Square, Square>())
				.UseDuckTyping()),
	};

	private static ITypeShape<T> Shape<T, TProvider>()
#if NET
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#endif

	[GenerateShape]
	internal abstract partial record Shape2;

	[GenerateShape]
	internal partial record Circle(double Radius) : Shape2;

	[GenerateShape]
	internal partial record Square(double Side) : Shape2;

	[GenerateShape]
	internal partial record EmptyShape : Shape2;

	[GenerateShape]
	[DerivedTypeShape(typeof(Cat2))]
	internal partial record Animal2(string Name);

	[GenerateShape]
	internal partial record Cat2(string Name, int Lives) : Animal2(Name);

	[GenerateShape]
	internal partial record Dog2(string Name, string Breed) : Animal2(Name);

	[GenerateShape]
	internal partial class ShapeContainer
	{
		public Shape2? Shape { get; set; }
	}

	[GenerateShape]
	internal abstract partial record Overlap;

	[GenerateShape]
	internal partial record OverlapA(int Shared, int A) : Overlap;

	[GenerateShape]
	internal partial record OverlapB(int Shared, int B) : Overlap;

	[GenerateShape]
	internal abstract partial record Wrapper;

	[GenerateShape]
	internal partial record ShapeWrapper(Shape2 Inner) : Wrapper;

	private sealed class CircleConverter : JsonConverter<Circle>
	{
		public override void Write(ref JsonWriter writer, Circle? value, SerializationContext context)
		{
			if (value is null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName("r");
			writer.WriteNumberValue(value.Radius);
			writer.WriteEndObject();
		}

		public override Circle? Read(ref JsonReader reader, SerializationContext context)
		{
			if (reader.TryReadNull())
			{
				return null;
			}

			reader.ReadStartObject();
			string name = reader.ReadRequiredString();
			reader.ReadNameSeparator();
			double radius = double.Parse(reader.ReadNumberToken(), System.Globalization.CultureInfo.InvariantCulture);
			Assert.Equal("r", name);
			Assert.True(reader.TryReadEndObject());
			return new Circle(radius);
		}
	}
}
