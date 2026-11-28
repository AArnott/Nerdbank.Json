// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable DuckTyping // Tests the experimental union strategy.

using System.Text;
using PolyType.Abstractions;

public partial class JsonRuntimeUnionTests
{
	[GenerateShape]
	internal partial interface IAnimal;

	[GenerateShape]
	internal partial interface ISame;

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
		DerivedTypeDuckTyping catchAll = new(Shape<EmptyConcreteBase, EmptyConcreteBase>(), Shape<ConcreteCase, ConcreteCase>());
		Assert.Equal(typeof(EmptyConcreteBase), catchAll.BaseType);
		DerivedTypeDuckTyping optionalCatchAll = new(Shape<IAnimal, IAnimal>(), Shape<OptionalOnlyAnimal, OptionalOnlyAnimal>(), Shape<RequiredAnimal, RequiredAnimal>());
		Assert.Equal(typeof(IAnimal), optionalCatchAll.BaseType);
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<IAnimal, IAnimal>(), Shape<OptionalOnlyAnimal, OptionalOnlyAnimal>(), Shape<EmptyAnimal, EmptyAnimal>()));
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<IAnimal, IAnimal>(), Shape<ComputedAnimal, ComputedAnimal>(), Shape<RequiredAnimal, RequiredAnimal>()));
		Assert.Throws<ArgumentException>(() => new DerivedTypeDuckTyping(Shape<IAnimal, IAnimal>(), Shape<MixedEvidenceAnimal, MixedEvidenceAnimal>(), Shape<RequiredAnimal, RequiredAnimal>()));
	}

	[Test]
	public async Task DuckTyping_RequiredPropertyDistinction_RoundTrips()
	{
		JsonSerializer serializer = new()
		{
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
	public void DuckTyping_SerializerDependentNameCollisions_AreRejectedWhenBuilt()
	{
		DerivedTypeDuckTyping union = new(Shape<Shape2, Shape2>(), Shape<Circle, Circle>(), Shape<Square, Square>());
		JsonSerializer serializer = new()
		{
			PropertyNamingPolicy = new ConstantNamingPolicy(),
			DerivedTypeUnions = [union],
		};
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => serializer.Deserialize("""{"value":1}""", Shape<Shape2, Shape2>()));
		Assert.IsType<NotSupportedException>(error.InnerException);
		Assert.Contains("same properties", error.Message, StringComparison.OrdinalIgnoreCase);
		Assert.Throws<NotSupportedException>(() => serializer.GetJsonSchema(Shape<Shape2, Shape2>()));
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
		Assert.Equal("CustomCircle", schema["anyOf"]![0]!["anyOf"]![0]!["title"]!.GetValue<string>());
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
		Assert.Equal(new Cat2("Milo", 9), serializer.Deserialize("""{"name":"Milo","lives":9}""", Shape<Animal2, Animal2>()));
		const string unknown = """{"other":1}""";
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(unknown, Shape<Animal2, Animal2>()));
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(unknown));
		await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync(stream, Shape<Animal2, Animal2>()));

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
	public async Task DuckTyping_MostSpecificCase_WinsNestedSets()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<Speck, Speck>(), Shape<BiggerSpeck, BiggerSpeck>(), Shape<BiggestSpeck, BiggestSpeck>(), Shape<OtherSpeck, OtherSpeck>())],
		};

		Assert.IsType<Speck>(serializer.Deserialize("""{"a":"1"}""", Shape<Speck, Speck>()));
		Assert.IsType<BiggerSpeck>(serializer.Deserialize("""{"a":"1","b":"2"}""", Shape<Speck, Speck>()));
		BiggestSpeck nested = Assert.IsType<BiggestSpeck>(serializer.Deserialize("""{"a":"1","b":"2","c":"3"}""", Shape<Speck, Speck>()));
		Assert.Equal("3", nested.C);
		Assert.IsType<OtherSpeck>(serializer.Deserialize("""{"a":"1","d":"4"}""", Shape<Speck, Speck>()));

		foreach (string json in new[] { """{"a":"1","b":"2","d":"4"}""", """{"a":"1","b":"2","c":"3","d":"4"}""" })
		{
			JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json, Shape<Speck, Speck>()));
			Assert.Contains("ambiguous", error.Message, StringComparison.OrdinalIgnoreCase);
			using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
			await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync(stream, Shape<Speck, Speck>()));
		}
	}

	[Test]
	public async Task DuckTyping_CatchAll_IsChosenOnlyWhenNothingElseMatches()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<EmptyConcreteBase, EmptyConcreteBase>(), Shape<ConcreteCase, ConcreteCase>(), Shape<OtherConcreteCase, OtherConcreteCase>())],
		};

		Assert.IsType<EmptyConcreteBase>(serializer.Deserialize("{}", Shape<EmptyConcreteBase, EmptyConcreteBase>()));
		Assert.IsType<EmptyConcreteBase>(serializer.Deserialize("""{"extra":1}""", Shape<EmptyConcreteBase, EmptyConcreteBase>()));
		Assert.Equal(3, Assert.IsType<ConcreteCase>(serializer.Deserialize("""{"number":3}""", Shape<EmptyConcreteBase, EmptyConcreteBase>())).Number);
		Assert.Equal(3, Assert.IsType<ConcreteCase>(serializer.Deserialize("""{"number":3,"extra":true}""", Shape<EmptyConcreteBase, EmptyConcreteBase>())).Number);
		Assert.Equal("x", Assert.IsType<OtherConcreteCase>(serializer.Deserialize("""{"label":"x"}""", Shape<EmptyConcreteBase, EmptyConcreteBase>())).Label);
		JsonSerializationException ambiguous = Assert.Throws<JsonSerializationException>(() => serializer.Deserialize("""{"number":1,"label":"x"}""", Shape<EmptyConcreteBase, EmptyConcreteBase>()));
		Assert.Contains("ambiguous", ambiguous.Message, StringComparison.OrdinalIgnoreCase);
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("""{"extra":1}"""));
		Assert.IsType<EmptyConcreteBase>(await serializer.DeserializeAsync(stream, Shape<EmptyConcreteBase, EmptyConcreteBase>()));
	}

	[Test]
	public void DuckTyping_IdenticalRequiredNames_AreRejectedWhenBuilt()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<ISame, ISame>(), Shape<SameA, SameA>(), Shape<SameB, SameB>())],
		};
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => serializer.Serialize<ISame>(new SameA(1), Shape<ISame, ISame>()));
		Assert.IsType<NotSupportedException>(error.InnerException);
		Assert.Contains("same properties", error.Message, StringComparison.OrdinalIgnoreCase);
		Assert.Throws<NotSupportedException>(() => serializer.GetJsonSchema(Shape<ISame, ISame>()));
	}

	[Test]
	public void DuckTyping_CaseFoldingCollisions_AreRejectedWhenBuilt()
	{
		// A null naming policy keeps the explicit "Volume" and "volume" names distinct except for case.
		JsonSerializer serializer = new()
		{
			PropertyNamingPolicy = null,
			PropertyNameCaseInsensitive = true,
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<IAnimal, IAnimal>(), Shape<LoudAnimal, LoudAnimal>(), Shape<QuietAnimal, QuietAnimal>())],
		};
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => serializer.Deserialize("""{"volume":1}""", Shape<IAnimal, IAnimal>()));
		Assert.IsType<NotSupportedException>(error.InnerException);
		Assert.Contains("same properties", error.Message, StringComparison.OrdinalIgnoreCase);

		JsonSerializer sensitive = serializer with { PropertyNameCaseInsensitive = false };
		Assert.Equal(1, Assert.IsType<LoudAnimal>(sensitive.Deserialize("""{"Volume":1}""", Shape<IAnimal, IAnimal>())).Level);
		Assert.Equal(2, Assert.IsType<QuietAnimal>(sensitive.Deserialize("""{"volume":2}""", Shape<IAnimal, IAnimal>())).Pitch);
	}

	[Test]
	public void DuckTyping_SchemaUsesBareObjectCases()
	{
		JsonSerializer serializer = DuckSerializer();
		System.Text.Json.Nodes.JsonObject schema = serializer.GetJsonSchema(Shape<Shape2, Shape2>());
		System.Text.Json.Nodes.JsonArray cases = schema["anyOf"]![0]!["anyOf"]!.AsArray();
		Assert.Equal(2, cases.Count);
		Assert.Equal("object", cases[0]!["type"]!.GetValue<string>());
		Assert.Equal("radius", cases[0]!["required"]![0]!.GetValue<string>());
		Assert.Equal("side", cases[1]!["required"]![0]!.GetValue<string>());
		Assert.Null(schema["anyOf"]![0]!["oneOf"]);
	}

	[Test]
	public void DuckTyping_OverlappingCatchAllSchema_AcceptsSerializedValue()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<ValueHolder, ValueHolder>(), Shape<OptionalValueHolder, OptionalValueHolder>(), Shape<RequiredValueHolder, RequiredValueHolder>())],
		};
		string json = serializer.Serialize<ValueHolder>(new RequiredValueHolder { Value = 1 });
		Assert.Equal("""{"value":1}""", json);
		System.Text.Json.Nodes.JsonObject schema = serializer.GetJsonSchema(Shape<ValueHolder, ValueHolder>());
		System.Text.Json.Nodes.JsonObject union = schema["anyOf"]![0]!.AsObject();
		Assert.Null(union["oneOf"]);
		System.Text.Json.Nodes.JsonArray cases = union["anyOf"]!.AsArray();
		System.Text.Json.Nodes.JsonObject written = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
		Assert.True(CountObjectMatches(cases, written) > 1);
		Assert.Equal(1, CountObjectMatches(cases, []));
		Assert.True(SchemaMatches(schema, written));

		static int CountObjectMatches(System.Text.Json.Nodes.JsonArray alternatives, System.Text.Json.Nodes.JsonObject instance)
		{
			int matches = 0;
			foreach (System.Text.Json.Nodes.JsonNode? alternative in alternatives)
			{
				if (SchemaMatches(alternative!.AsObject(), instance))
				{
					matches++;
				}
			}

			return matches;
		}

		static bool SchemaMatches(System.Text.Json.Nodes.JsonObject schemaNode, System.Text.Json.Nodes.JsonNode? instance)
		{
			if (schemaNode["anyOf"] is System.Text.Json.Nodes.JsonArray anyOf)
			{
				foreach (System.Text.Json.Nodes.JsonNode? alternative in anyOf)
				{
					if (SchemaMatches(alternative!.AsObject(), instance))
					{
						return true;
					}
				}

				return false;
			}

			if (schemaNode.TryGetPropertyValue("type", out System.Text.Json.Nodes.JsonNode? typeNode)
				&& typeNode is System.Text.Json.Nodes.JsonValue typeValue
				&& typeValue.GetValue<string>() == "null")
			{
				return instance is null;
			}

			if (instance is not System.Text.Json.Nodes.JsonObject instanceObject)
			{
				return false;
			}

			System.Text.Json.Nodes.JsonObject? properties = schemaNode["properties"]?.AsObject();
			bool allowAdditional = schemaNode["additionalProperties"] is not System.Text.Json.Nodes.JsonValue additional || additional.GetValue<bool>();
			if (!allowAdditional)
			{
				foreach (KeyValuePair<string, System.Text.Json.Nodes.JsonNode?> property in instanceObject)
				{
					if (properties?.ContainsKey(property.Key) != true)
					{
						return false;
					}
				}
			}

			if (schemaNode["required"] is System.Text.Json.Nodes.JsonArray required)
			{
				foreach (System.Text.Json.Nodes.JsonNode? name in required)
				{
					if (!instanceObject.ContainsKey(name!.GetValue<string>()))
					{
						return false;
					}
				}
			}

			if (properties is null)
			{
				return true;
			}

			foreach (KeyValuePair<string, System.Text.Json.Nodes.JsonNode?> property in instanceObject)
			{
				if (properties[property.Key] is System.Text.Json.Nodes.JsonObject propertySchema
					&& propertySchema.TryGetPropertyValue("type", out System.Text.Json.Nodes.JsonNode? propertyType)
					&& propertyType is System.Text.Json.Nodes.JsonValue expected
					&& expected.GetValue<string>() == "integer"
					&& property.Value is System.Text.Json.Nodes.JsonValue number
					&& (!number.TryGetValue(out int _)))
				{
					return false;
				}
			}

			return true;
		}
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

		public void OnAfterSerialize()
		{
		}

		public void OnBeforeDeserialize()
		{
		}

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
	internal partial class MixedEvidenceAnimal(string name) : IAnimal
	{
		public string Name { get; } = name;

		[PropertyShape(IsRequired = true)]
		public int Computed => 1;
	}

	[GenerateShape]
	internal abstract partial record ValueHolder;

	[GenerateShape]
	internal partial record OptionalValueHolder : ValueHolder
	{
		public int Value { get; set; }
	}

	[GenerateShape]
	internal partial record RequiredValueHolder : ValueHolder
	{
		public required int Value { get; set; }
	}

	[GenerateShape]
	internal partial class EmptyConcreteBase;

	[GenerateShape]
	internal partial class ConcreteCase(int number) : EmptyConcreteBase
	{
		public int Number { get; } = number;
	}

	[GenerateShape]
	internal partial class OtherConcreteCase(string label) : EmptyConcreteBase
	{
		public string Label { get; } = label;
	}

	[GenerateShape]
	internal partial class EmptyAnimal : IAnimal;

	[GenerateShape]
	internal partial record SameA(int Value) : ISame;

	[GenerateShape]
	internal partial record SameB(int Value) : ISame;

	[GenerateShape]
	internal partial class LoudAnimal : IAnimal
	{
		[PropertyShape(Name = "Volume")]
		public required int Level { get; init; }
	}

	[GenerateShape]
	internal partial class QuietAnimal : IAnimal
	{
		[PropertyShape(Name = "volume")]
		public required int Pitch { get; init; }
	}

	[GenerateShape]
	internal partial class Speck
	{
		public required string A { get; init; }
	}

	[GenerateShape]
	internal partial class BiggerSpeck : Speck
	{
		public required string B { get; init; }
	}

	[GenerateShape]
	internal partial class BiggestSpeck : BiggerSpeck
	{
		public required string C { get; init; }
	}

	[GenerateShape]
	internal partial class OtherSpeck : Speck
	{
		public required string D { get; init; }
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
