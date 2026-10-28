// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using PolyType.SourceGenerator;

public partial class CSharpUnionTests : TestBase
{
	[Test]
	public void ScalarCases()
	{
		Assert.Equal(42, this.Roundtrip(new Scalar(42))!.Value);
		Assert.Equal("""["Int32",42]""", this.LastSerializedJson);
		Assert.Equal("hi", this.Roundtrip(new Scalar("hi"))!.Value);
		Assert.Equal("""["String","hi"]""", this.LastSerializedJson);
	}

	[Test]
	public void NullPayload()
	{
		Assert.Null(this.Roundtrip(default(Scalar))!.Value);
		Assert.Equal("""["String",null]""", this.LastSerializedJson);
	}

	[Test]
	public void ClassUnion()
	{
		Assert.Null(this.Roundtrip<PetClass>(null!));
		Assert.Equal("null", this.LastSerializedJson);
		PetClass result = this.Roundtrip(new PetClass(new Dog("Rover", 10)))!;
		Assert.Equal(new Dog("Rover", 10), result.Value);
		Assert.Equal("""["Dog",{"name":"Rover","barkVolume":10}]""", this.LastSerializedJson);
	}

	[Test]
	public void ObjectPayload()
	{
		Assert.Equal(new Dog("Rover", 10), this.Roundtrip(new Pet(new Dog("Rover", 10)))!.Value);
		Assert.Equal("""["Dog",{"name":"Rover","barkVolume":10}]""", this.LastSerializedJson);
	}

	[Test]
	public void RecursiveUnion()
	{
		Nested result = this.Roundtrip(new Nested(new Nested(new Nested(true))))!;
		Assert.Equal("""["Nested",["Nested",["Boolean",true]]]""", this.LastSerializedJson);
		Assert.Equal(true, Assert.IsType<Nested>(Assert.IsType<Nested>(result.Value).Value).Value);
	}

	[Test]
	public void NonBoxingUnion()
	{
		Assert.Equal(42, this.Roundtrip(new NumberOrText(42)).Value);
		Assert.Equal("""["Int32",42]""", this.LastSerializedJson);
		Assert.Equal("hi", this.Roundtrip(new NumberOrText("hi")).Value);
		Assert.Equal("""["String","hi"]""", this.LastSerializedJson);
	}

	[Test]
	public void NoMatchingCase_Throws()
	{
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(default(Numeric)));
	}

	[Test]
	public void DiscriminatorObjects()
	{
		this.Serializer = this.Serializer with { UseDiscriminatorObjects = true };
		Assert.Equal(42, this.Roundtrip(new Scalar(42))!.Value);
		Assert.Equal("""{"Int32":42}""", this.LastSerializedJson);
	}

	[Test]
	public async Task AsyncRoundtrip()
	{
		using MemoryStream stream = new();
		Nested value = new(new Nested(true));
		await this.Serializer.SerializeAsync(stream, value);
		stream.Position = 0;
		Nested result = (await this.Serializer.DeserializeAsync<Nested>(stream))!;
		Assert.Equal(true, Assert.IsType<Nested>(result.Value).Value);
	}

	[Test]
	public void PayloadReferences_ArePreserved()
	{
		this.Serializer = this.Serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		Dog dog = new("Rover", 10);
		Pets result = this.Roundtrip(new Pets { First = new Pet(dog), Second = new Pet(dog) })!;
		Assert.Same(result.First.Value, result.Second.Value);
	}

	[Test]
	public void UnionReferences_ArePreserved()
	{
		this.Serializer = this.Serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		PetClass pet = new(new Dog("Rover", 10));
		ClassPets result = this.Roundtrip(new ClassPets { First = pet, Second = pet })!;
		Assert.Same(result.First, result.Second);
	}

	[Test]
	public void Schema_ContainsCasesWithoutBaseFallback()
	{
		string schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Scalar>()!).ToJsonString();
		Assert.Contains("\"const\":\"Int32\"", schema);
		Assert.Contains("\"const\":\"String\"", schema);
		Assert.DoesNotContain("\"const\":null", schema);
	}

	[Test]
	public void RecursiveSchema()
	{
		string schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Nested>()!).ToJsonString();
		Assert.Contains("\"$ref\":", schema);
		Assert.Contains("\"const\":\"Nested\"", schema);
	}

	[Test]
	public void DocumentationSamples()
	{
		Assert.Equal(42, CSharpUnions.Examples.Roundtrip().Value);
		JsonSerializer mapped = CSharpUnions.Examples.CreateSerializer();
		Assert.Equal("""["number",42]""", mapped.Serialize(new CSharpUnions.Result(42)));
		Assert.Equal("hi", mapped.Deserialize<CSharpUnions.Result>("""["text","hi"]""").Value);
		JsonSerializer delegated = CSharpUnions.Examples.CreateSerializerWithDelegate();
		Assert.Equal("""[1,42]""", delegated.Serialize(new CSharpUnions.Result(42)));
	}

	[Test]
	public void UnionSet_UsesConfiguredComparer()
	{
		HashSet<Nested> result = this.Roundtrip<HashSet<Nested>, SetWitness>([new Nested(new Nested(true))])!;
		Assert.Equal(true, Assert.IsType<Nested>(Assert.Single(result).Value).Value);
	}

	[Test, MatrixDataSource]
	public async Task RecursiveUnion_DepthLimit(bool asynchronous, bool discriminatorObjects, bool exceedsLimit)
	{
		const int MaxDepth = 4;
		this.Serializer = this.Serializer with
		{
			UseDiscriminatorObjects = discriminatorObjects,
			StartingContext = this.Serializer.StartingContext with { MaxDepth = MaxDepth },
		};
		int depth = MaxDepth + (exceedsLimit ? 1 : 0);
		Nested value = new(true);
		string json = discriminatorObjects ? """{"Boolean":true}""" : """["Boolean",true]""";
		for (int i = 1; i < depth; i++)
		{
			value = new(value);
			json = discriminatorObjects ? """{"Nested":""" + json + "}" : """["Nested",""" + json + "]";
		}

		if (exceedsLimit)
		{
			if (asynchronous)
			{
				using MemoryStream output = new();
				await Assert.ThrowsAsync<JsonSerializationException>(() => this.Serializer.SerializeAsync(output, value).AsTask());
				using MemoryStream input = new(System.Text.Encoding.UTF8.GetBytes(json));
				await Assert.ThrowsAsync<JsonSerializationException>(() => this.Serializer.DeserializeAsync<Nested>(input).AsTask());
			}
			else
			{
				Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(value));
				Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<Nested>(json));
			}
		}
		else if (asynchronous)
		{
			using MemoryStream output = new();
			await this.Serializer.SerializeAsync(output, value);
			Assert.Equal(json, System.Text.Encoding.UTF8.GetString(output.ToArray()));
			output.Position = 0;
			await this.Serializer.DeserializeAsync<Nested>(output);
		}
		else
		{
			Assert.Equal(json, this.Serializer.Serialize(value));
			this.Serializer.Deserialize<Nested>(json);
		}
	}

#pragma warning disable SA1201 // StyleCop does not yet recognize union declarations.
	[GenerateShape]
	public partial union Scalar(int, string?);

	[GenerateShape]
	public partial union Pet(Dog, Cat);

	[GenerateShape]
	public partial union Nested(bool, Nested);

	[GenerateShape]
	public partial union Numeric(int, bool);
#pragma warning restore SA1201

	[Union, GenerateShape]
	public readonly partial struct NumberOrText
	{
		private readonly int number;
		private readonly string? text;
		private readonly bool isText;

		public NumberOrText(int value) => this.number = value;

		public NumberOrText(string? value)
		{
			this.text = value;
			this.isText = true;
		}

		public object? Value => this.isText ? this.text : this.number;

		public bool HasValue => !this.isText || this.text is not null;

		public bool TryGetValue(out int value)
		{
			value = this.number;
			return !this.isText;
		}

		public bool TryGetValue([NotNullWhen(true)] out string? value)
		{
			value = this.text;
			return this.isText && this.text is not null;
		}
	}

	[Union, GenerateShape]
	public sealed partial class PetClass
	{
		public PetClass(Dog value) => this.Value = value;

		public PetClass(Cat value) => this.Value = value;

		public object? Value { get; }
	}

	public record Dog(string Name, int BarkVolume);

	public record Cat(string Name, int MeowPitch);

	[GenerateShape]
	public partial class Pets
	{
		public Pet First { get; set; }

		public Pet Second { get; set; }
	}

	[GenerateShape]
	public partial class ClassPets
	{
		public PetClass? First { get; set; }

		public PetClass? Second { get; set; }
	}

	[GenerateShapeFor<HashSet<Nested>>]
	public partial class SetWitness;
}
