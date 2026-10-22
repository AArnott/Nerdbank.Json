// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable DuckTyping // Tests the experimental union strategy.

using System.Text;
using PolyType.Abstractions;

public partial class JsonRuntimeUnionTests
{
	[GenerateShape]
	internal partial interface IAnimal;

	[Test]
	public void DuckTyping_ConstructorValidatesCases()
	{
		Assert.Throws<ArgumentNullException>(() => new DerivedTypeDuckTyping(null!));
		Assert.Throws<ArgumentNullException>(() => new DerivedTypeDuckTyping(Shape<Shape2, Shape2>(), [null!, Shape<Circle, Circle>()]));
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<Shape2, Shape2>()));
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<Shape2, Shape2>(), Shape<Circle, Circle>()));
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<Shape2, Shape2>(), Shape<Circle, Circle>(), Shape<Circle, Circle>()));
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<Shape2, Shape2>(), Shape<Circle, Circle>(), Shape<Cat2, Cat2>()));
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<Shape2, Shape2>(), Shape<Shape2, Shape2>(), Shape<Circle, Circle>()));
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<Animal2, Animal2>(), Shape<Animal2, Animal2>()));
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<IAnimal, IAnimal>(), Shape<IAnimal, IAnimal>(), Shape<RequiredAnimal, RequiredAnimal>()));
	}

	[Test]
	public void DuckTyping_ConstructorValidatesConcreteBaseAndObjectContracts()
	{
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<EmptyConcreteBase, EmptyConcreteBase>(), Shape<ConcreteCase, ConcreteCase>()));
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<IAnimal, IAnimal>(), Shape<OptionalOnlyAnimal, OptionalOnlyAnimal>(), Shape<RequiredAnimal, RequiredAnimal>()));
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<IAnimal, IAnimal>(), Shape<ComputedAnimal, ComputedAnimal>(), Shape<RequiredAnimal, RequiredAnimal>()));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task DuckTyping_RequiredPropertyDistinction_RoundTrips(bool discriminatorObjects)
	{
		JsonSerializer serializer = new()
		{
			UseDiscriminatorObjects = discriminatorObjects,
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<Animal, Animal>(), Shape<Dog, Dog>(), Shape<Cat, Cat>())],
		};

		foreach (Animal value in new Animal[] { new Dog("Buddy", 5), new Cat("Whiskers", 3) })
		{
			string json = serializer.Serialize(value, Shape<Animal, Animal>());
			Assert.StartsWith("{", json);
			Assert.Equal(value, serializer.Deserialize(json, Shape<Animal, Animal>()));
			using MemoryStream stream = new();
			await serializer.SerializeAsync(stream, value, Shape<Animal, Animal>());
			Assert.Equal(json, Encoding.UTF8.GetString(stream.ToArray()));
			stream.Position = 0;
			Assert.Equal(value, await serializer.DeserializeAsync(stream, Shape<Animal, Animal>()));
		}
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task DuckTyping_RequiredShapeMetadataAndRenamedMembers_RoundTrip(bool caseInsensitive)
	{
		JsonSerializer serializer = new()
		{
			PropertyNameCaseInsensitive = caseInsensitive,
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<IAnimal, IAnimal>(), Shape<RequiredAnimal, RequiredAnimal>(), Shape<RenamedAnimal, RenamedAnimal>())],
		};

		IAnimal required = new RequiredAnimal { BarkVolume = 5 };
		Assert.Equal("""{"barkVolume":5}""", serializer.Serialize(required, Shape<IAnimal, IAnimal>()));
		string json = caseInsensitive ? """{"VOICE-LEVEL":3}""" : """{"voice-level":3}""";
		RenamedAnimal result = Assert.IsType<RenamedAnimal>(serializer.Deserialize(json, Shape<IAnimal, IAnimal>()));
		Assert.Equal(3, result.MeowPitch);
		Assert.Equal("""{"voice-level":3}""", serializer.Serialize<IAnimal>(result, Shape<IAnimal, IAnimal>()));
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
		Assert.Equal(3, Assert.IsType<RenamedAnimal>(await serializer.DeserializeAsync(stream, Shape<IAnimal, IAnimal>())).MeowPitch);
		Assert.Equal(5, Assert.IsType<RequiredAnimal>(serializer.Deserialize("""{"barkVolume":5}""", Shape<IAnimal, IAnimal>())).BarkVolume);
	}

	[Test]
	public async Task DuckTyping_ConstructorNameMatchingAndNamingPolicy_AgreeWithObjectConverter()
	{
		JsonSerializer serializer = new()
		{
			PropertyNamingPolicy = new UpperCaseNamingPolicy(),
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<IAnimal, IAnimal>(), Shape<ConstructorAnimal, ConstructorAnimal>(), Shape<RenamedAnimal, RenamedAnimal>())],
		};
		IAnimal value = new ConstructorAnimal(8);
		string json = serializer.Serialize(value, Shape<IAnimal, IAnimal>());
		Assert.Equal("""{"BARKVOLUME":8}""", json);
		Assert.Equal(8, Assert.IsType<ConstructorAnimal>(serializer.Deserialize(json, Shape<IAnimal, IAnimal>())).BarkVolume);
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
		Assert.Equal(8, Assert.IsType<ConstructorAnimal>(await serializer.DeserializeAsync(stream, Shape<IAnimal, IAnimal>())).BarkVolume);
	}

	[Test]
	public void DuckTyping_OptionalPropertyOverride_IsNotRequiredEvidence()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<IAnimal, IAnimal>(), Shape<OptionalAnimal, OptionalAnimal>(), Shape<RequiredAnimal, RequiredAnimal>())],
		};
		Assert.IsType<OptionalAnimal>(serializer.Deserialize("""{"name":"optional"}""", Shape<IAnimal, IAnimal>()));
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => serializer.Deserialize("""{"optional":1}""", Shape<IAnimal, IAnimal>()));
		Assert.Contains("insufficient", error.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Test]
	public void DuckTyping_SerializerDependentNameCollisions_RemainAmbiguous()
	{
		DerivedTypeDuckTyping union = new(Shape<Shape2, Shape2>(), Shape<Circle, Circle>(), Shape<Square, Square>());
		JsonSerializer serializer = new()
		{
			PropertyNamingPolicy = new ConstantNamingPolicy(),
			DerivedTypeUnions = [union],
		};
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => serializer.Deserialize("""{"value":1}""", Shape<Shape2, Shape2>()));
		Assert.Contains("ambiguous", error.Message, StringComparison.OrdinalIgnoreCase);
		JsonSerializer normalNames = serializer with { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		Assert.Equal(new Circle(1), normalNames.Deserialize("""{"radius":1}""", Shape<Shape2, Shape2>()));
	}

	[Test]
	public async Task DuckTyping_CustomCaseConverter_IsUsed()
	{
		DuckCircleConverter converter = new();
		JsonSerializer serializer = DuckSerializer() with { Converters = ConverterCollection.Create([converter]) };
		Shape2 value = new Circle(3);
		const string json = """{"radius":13}""";
		Assert.Equal(json, serializer.Serialize(value, Shape<Shape2, Shape2>()));
		Assert.Equal(value, serializer.Deserialize(json, Shape<Shape2, Shape2>()));
		using MemoryStream stream = new();
		await serializer.SerializeAsync(stream, value, Shape<Shape2, Shape2>());
		Assert.Equal(json, Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		Assert.Equal(value, await serializer.DeserializeAsync(stream, Shape<Shape2, Shape2>()));
		Assert.Equal(2, converter.ReadCount);
		System.Text.Json.Nodes.JsonObject schema = serializer.GetJsonSchema(Shape<Shape2, Shape2>());
		Assert.Equal("CustomCircle", schema["anyOf"]![0]!["oneOf"]![0]!["title"]!.GetValue<string>());
	}

	[Test]
	public async Task DuckTyping_RequiredNonNullableMember_RejectsNull()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<Animal, Animal>(), Shape<Dog, Dog>(), Shape<Cat, Cat>())],
		};
		const string json = """{"name":null,"barkVolume":5}""";
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json, Shape<Animal, Animal>()));
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
		await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync(stream, Shape<Animal, Animal>()));
	}

	[Test]
	public async Task DuckTyping_ConcreteGeneratedBase_IsCaseNotFallback()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<Animal2, Animal2>(), Shape<Cat2, Cat2>())],
		};
		Animal2 value = new("Ada");
		Assert.Equal("""{"name":"Ada"}""", serializer.Serialize(value, Shape<Animal2, Animal2>()));
		Assert.Equal(value, serializer.Deserialize("""{"name":"Ada"}""", Shape<Animal2, Animal2>()));
		foreach (string json in new[] { """{"name":"Milo","lives":9}""", """{"other":1}""" })
		{
			Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json, Shape<Animal2, Animal2>()));
			using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
			await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync(stream, Shape<Animal2, Animal2>()));
		}

		Assert.Throws<JsonSerializationException>(() => serializer.Serialize<Animal2>(new Dog2("Rex", "Lab"), Shape<Animal2, Animal2>()));
	}

	[Test]
	[Arguments("""{"radius":1,"side":2}""")]
	[Arguments("""{"side":2,"radius":1}""")]
	[Arguments("""{"unknown":1}""")]
	[Arguments("[]")]
	public async Task DuckTyping_AllCasesMustBeChecked_SyncAndAsync(string json)
	{
		JsonSerializer serializer = DuckSerializer();
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json, Shape<Shape2, Shape2>()));
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
		await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync(stream, Shape<Shape2, Shape2>()));
	}

	[Test]
	public async Task DuckTyping_Null_RoundTrips()
	{
		JsonSerializer serializer = DuckSerializer();
		Assert.Equal("null", serializer.Serialize<Shape2>(null, Shape<Shape2, Shape2>()));
		Assert.Null(serializer.Deserialize("null", Shape<Shape2, Shape2>()));
		using MemoryStream stream = new("null"u8.ToArray());
		Assert.Null(await serializer.DeserializeAsync(stream, Shape<Shape2, Shape2>()));
	}

	[Test]
	public async Task DuckTyping_CallbacksAndReferenceMetadata_ArePreserved()
	{
		JsonSerializer serializer = new()
		{
			PreserveReferences = ReferencePreservationMode.RejectCycles,
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<IAnimal, IAnimal>(), Shape<RequiredAnimal, RequiredAnimal>(), Shape<RenamedAnimal, RenamedAnimal>())],
		};
		RequiredAnimal animal = new() { BarkVolume = 5 };
		IAnimal[] values = [animal, animal];
		string json = serializer.Serialize(values, Shape<IAnimal[], DuckWitness>());
		Assert.Equal(1, animal.BeforeSerializeCount);
		Assert.Contains("\"$id\":", json, StringComparison.Ordinal);
		Assert.Contains("\"$ref\":", json, StringComparison.Ordinal);
		IAnimal[] result = serializer.Deserialize(json, Shape<IAnimal[], DuckWitness>())!;
		Assert.Same(result[0], result[1]);
		Assert.Equal(1, Assert.IsType<RequiredAnimal>(result[0]).AfterDeserializeCount);
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
		result = (await serializer.DeserializeAsync(stream, Shape<IAnimal[], DuckWitness>()))!;
		Assert.Same(result[0], result[1]);
		Assert.Equal(1, Assert.IsType<RequiredAnimal>(result[0]).AfterDeserializeCount);
	}

	[Test]
	public void DuckTyping_SchemaUsesBareObjectCases()
	{
		JsonSerializer serializer = DuckSerializer() with { UseDiscriminatorObjects = true };
		System.Text.Json.Nodes.JsonObject schema = serializer.GetJsonSchema(Shape<Shape2, Shape2>());
		System.Text.Json.Nodes.JsonArray cases = schema["anyOf"]![0]!["oneOf"]!.AsArray();
		Assert.Equal(2, cases.Count);
		Assert.Equal("object", cases[0]!["type"]!.GetValue<string>());
		Assert.Equal("radius", cases[0]!["required"]![0]!.GetValue<string>());
		Assert.Equal("side", cases[1]!["required"]![0]!.GetValue<string>());
	}

	[GenerateShape]
	internal partial class RequiredAnimal : IAnimal, IJsonSerializationCallbacks
	{
		[PropertyShape(IsRequired = true)]
		public int BarkVolume { get; set; }

		[PropertyShape(Ignore = true)]
		public int BeforeSerializeCount { get; private set; }

		[PropertyShape(Ignore = true)]
		public int AfterDeserializeCount { get; private set; }

		public void OnBeforeSerialize() => this.BeforeSerializeCount++;

		public void OnAfterDeserialize() => this.AfterDeserializeCount++;
	}

	[GenerateShape]
	internal partial class RenamedAnimal : IAnimal
	{
		[PropertyShape(Name = "voice-level")]
		public required int MeowPitch { get; init; }
	}

	[GenerateShape]
	internal partial class ConstructorAnimal(int barkVolume) : IAnimal
	{
		public int BarkVolume { get; } = barkVolume;
	}

	[GenerateShape]
	internal partial class OptionalAnimal(string name) : IAnimal
	{
		public string Name { get; } = name;

		[PropertyShape(IsRequired = false)]
		public required int Optional { get; init; }
	}

	[GenerateShape]
	internal partial class OptionalOnlyAnimal : IAnimal
	{
		[PropertyShape(IsRequired = false)]
		public required int Optional { get; init; }
	}

	[GenerateShape]
	internal partial class ComputedAnimal : IAnimal
	{
		[PropertyShape(IsRequired = true)]
		public int Computed => 1;
	}

	[GenerateShape]
	internal partial class EmptyConcreteBase;

	[GenerateShape]
	internal partial class ConcreteCase(int number) : EmptyConcreteBase
	{
		public int Number { get; } = number;
	}

	[GenerateShapeFor<IAnimal[]>]
	private partial class DuckWitness;

	private sealed class UpperCaseNamingPolicy : JsonNamingPolicy
	{
		public override string ConvertName(string name) => name.ToUpperInvariant();
	}

	private sealed class ConstantNamingPolicy : JsonNamingPolicy
	{
		public override string ConvertName(string name) => "value";
	}

	private sealed class DuckCircleConverter : JsonConverter<Circle>
	{
		internal int ReadCount { get; private set; }

		public override void Write(ref JsonWriter writer, in Circle? value, SerializationContext context)
		{
			writer.WriteStartObject();
			writer.WritePropertyName("radius");
			writer.Write(value!.Radius + 10);
			writer.WriteEndObject();
		}

		public override Circle? Read(ref JsonReader reader, SerializationContext context)
		{
			this.ReadCount++;
			reader.ReadStartObject();
			Assert.Equal("radius", reader.ReadRequiredString());
			reader.ReadNameSeparator();
			double radius = double.Parse(reader.ReadNumberToken(), System.Globalization.CultureInfo.InvariantCulture);
			Assert.True(reader.TryReadEndObject());
			return new Circle(radius - 10);
		}

		public override System.Text.Json.Nodes.JsonObject? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape)
			=> new() { ["type"] = "object", ["title"] = "CustomCircle" };
	}

	[GenerateShape]
	internal abstract partial record Animal(string Name);

	[GenerateShape]
	internal partial record Dog(string Name, int BarkVolume) : Animal(Name);

	[GenerateShape]
	internal partial record Cat(string Name, int MeowPitch) : Animal(Name);
}
