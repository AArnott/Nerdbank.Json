// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using PolyType.SourceGenerator;
using JsonSchemaArray = System.Text.Json.Nodes.JsonArray;
using JsonSchemaNode = System.Text.Json.Nodes.JsonNode;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;

public partial class CSharpUnionTests : TestBase
{
	[Test]
	public void ScalarCases()
	{
		Assert.Equal(42, this.Roundtrip(new Scalar(42))!.Value);
		Assert.Equal("42", this.LastSerializedJson);
		Assert.Equal("hi", this.Roundtrip(new Scalar("hi"))!.Value);
		Assert.Equal("\"hi\"", this.LastSerializedJson);
	}

	[Test]
	public void NullPayload()
	{
		Assert.Null(this.Roundtrip(default(Scalar))!.Value);
		Assert.Equal("null", this.LastSerializedJson);
	}

	[Test]
	public void ClassUnion()
	{
		Assert.Null(this.Roundtrip<PetClass>(null!));
		Assert.Equal("null", this.LastSerializedJson);
		PetClass result = this.Roundtrip(new PetClass(new Dog("Rover", 10)))!;
		Assert.Equal(new Dog("Rover", 10), result.Value);
		Assert.Equal("""{"$type":"Dog","name":"Rover","barkVolume":10}""", this.LastSerializedJson);
	}

	[Test]
	public void ObjectPayload()
	{
		Assert.Equal(new Dog("Rover", 10), this.Roundtrip(new Pet(new Dog("Rover", 10)))!.Value);
		Assert.Equal("""{"$type":"Dog","name":"Rover","barkVolume":10}""", this.LastSerializedJson);
	}

	[Test]
	public void RecursiveUnion()
	{
		Nested result = this.Roundtrip(new Nested(new Nested(new Nested(true))))!;
		Assert.Equal("""{"$type":"Nested","$value":{"$type":"Nested","$value":true}}""", this.LastSerializedJson);
		Assert.Equal(true, Assert.IsType<Nested>(Assert.IsType<Nested>(result.Value).Value).Value);
	}

	[Test]
	public void NonBoxingUnion()
	{
		Assert.Equal(42, this.Roundtrip(new NumberOrText(42)).Value);
		Assert.Equal("42", this.LastSerializedJson);
		Assert.Equal("hi", this.Roundtrip(new NumberOrText("hi")).Value);
		Assert.Equal("\"hi\"", this.LastSerializedJson);
	}

	[Test]
	public void NoMatchingCase_Throws()
	{
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(default(Numeric)));
	}

	[Test]
	public void NativeScalarCasesRemainUntagged()
	{
		Assert.Equal(42, this.Roundtrip(new Scalar(42))!.Value);
		Assert.Equal("42", this.LastSerializedJson);
	}

	[Test]
	public void DiscriminatorObjects_TaggedScalar_UsesValueProperty()
	{
		this.Serializer = this.Serializer with
		{
			UnionTypeAliasProvider = new UnionCaseMappingCollection([new(typeof(Scalar)) { { "text", typeof(string) } }]),
		};
		Assert.Equal("hi", this.Roundtrip(new Scalar("hi"))!.Value);
		Assert.Equal("""{"$type":"text","$value":"hi"}""", this.LastSerializedJson);
		JsonSchemaObject schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Scalar>()!);
		Assert.Contains("\"$value\":{\"type\":[\"string\",\"null\"]}", schema.ToJsonString(), StringComparison.Ordinal);
	}

	[Test]
	public void ExplicitlyTaggedNullPayload_PreservesCase()
	{
		this.Serializer = this.Serializer with
		{
			UnionTypeAliasProvider = new UnionCaseMappingCollection([new(typeof(Scalar)) { { "text", typeof(string) } }]),
		};
		Assert.Null(this.Roundtrip(default(Scalar))!.Value);
		Assert.Equal("""{"$type":"text","$value":null}""", this.LastSerializedJson);
	}

	[Test]
	public void NullablePayloadOfReferenceTypeUnion_IsTagged()
	{
		NullableTextClass value = new(null);
		NullableTextClass result = this.Roundtrip(value)!;
		Assert.NotNull(result);
		Assert.Null(result.Value);
		Assert.Equal("""{"$type":"String","$value":null}""", this.LastSerializedJson);
	}

	[Test]
	public void InlineObjectUnionEnvelope_RejectsDuplicateDiscriminator()
	{
		string json = """{"$type":"Dog","name":"Rover","barkVolume":10,"$type":"Cat"}""";
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<PetClass>(json));
	}

	[Test]
	public void InlineObjectUnionEnvelope_RejectsNullAfterDiscriminator()
	{
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<Pet>("""{"$type":"Dog"null"""));
	}

	[Test]
	public async Task InlineObjectUnionEnvelope_RejectsNullAfterDiscriminatorAsync()
	{
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("""{"$type":"Dog"null"""));
		await Assert.ThrowsAsync<JsonSerializationException>(() => this.Serializer.DeserializeAsync<Pet>(stream).AsTask());
	}

	[Test]
	public async Task OutOfOrderMetadata_DoesNotCollideWithCaseVariantPayloadProperty()
	{
		JsonSerializer serializer = this.Serializer with
		{
			AllowOutOfOrderMetadataProperties = true,
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = new UppercaseTypeNamingPolicy(),
		};
		UppercaseTypeUnion value = new(new UppercaseTypeProperty("payload"));
		using MemoryStream stream = new();
		await serializer.SerializeAsync(stream, value);
		Assert.Equal("""{"$type":"UppercaseTypeProperty","$TYPE":"payload"}""", Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		UppercaseTypeUnion? result = await serializer.DeserializeAsync<UppercaseTypeUnion>(stream);
		Assert.Equal(value.Value, result!.Value.Value);
	}

	[Test]
	public void RecursiveObjectUnionFormatDoesNotDependOnConverterConstructionOrder()
	{
		RecursiveObjectUnion value = new(new RecursiveUnionContainingObject());
		JsonSerializer objectFirst = new();
		_ = objectFirst.Serialize(new RecursiveUnionContainingObject());
		string objectFirstJson = objectFirst.Serialize(value);

		string unionFirstJson = new JsonSerializer().Serialize(value);
		Assert.Equal("""{"$type":"RecursiveUnionContainingObject","next":null}""", unionFirstJson);
		Assert.Equal(unionFirstJson, objectFirstJson);
	}

	[Test]
	public void RecursiveObjectUnionRootSchema_InlinesUnionCase()
	{
		JsonSchemaObject schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<RecursiveUnionContainingObject>()!);
		JsonSchemaObject nextCase = schema["$defs"]!["NullableOfRecursiveObjectUnion"]!["anyOf"]![0]!["oneOf"]![0]!.AsObject();
		Assert.NotNull(nextCase["properties"]?["$type"]);
		Assert.NotNull(nextCase["properties"]?["next"]);
		Assert.Contains("$ref", nextCase["properties"]!["next"]!.ToJsonString(), StringComparison.Ordinal);
		Assert.Null(nextCase["properties"]?["$value"]);
	}

	[Test]
	public void RecursiveObjectUnionSchema_InlinesMultipleRecursiveUnionCases()
	{
		JsonSchemaObject schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<MultipleRecursiveUnionCases>()!);
		string schemaJson = schema.ToJsonString();
		Assert.Contains("\"allOf\"", schemaJson, StringComparison.Ordinal);
		Assert.DoesNotContain("\"$value\"", schemaJson, StringComparison.Ordinal);
	}

	[Test]
	public void ReferencePreservedRecursiveObjectUnion_KeepsSharedPayloadIdentity()
	{
		JsonSerializer unionFirst = this.Serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		JsonSerializer objectFirst = this.Serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		RecursiveUnionContainingObject shared = new();
		RecursiveObjectUnionHolder value = new()
		{
			First = new(shared),
			Second = new(shared),
		};

		string unionFirstJson = unionFirst.Serialize(value);
		_ = objectFirst.Serialize(new RecursiveUnionContainingObject());
		string objectFirstJson = objectFirst.Serialize(value);
		Assert.Equal(unionFirstJson, objectFirstJson);

		RecursiveObjectUnionHolder result = unionFirst.Deserialize<RecursiveObjectUnionHolder>(unionFirstJson)!;
		Assert.Same(result.First.Value, result.Second.Value);
	}

	[Test]
	public void RecursivePayloadUnionSchema_InlinesPreviouslyDefinedCase()
	{
		JsonSchemaObject schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<RecursivePayloadUnionHolder>()!);
		JsonSchemaObject unionCase = schema["properties"]!["union"]!["oneOf"]![0]!.AsObject();
		Assert.Contains("$type", unionCase["required"]!.AsArray().Select(static item => item!.GetValue<string>()));
		Assert.NotNull(unionCase["properties"]?["name"]);
		Assert.NotNull(unionCase["properties"]?["next"]);
	}

	[Test]
	public void MultipleNullableNativeCases_AllowNullInOnlyFirstSchemaCase()
	{
		Assert.Null(this.Roundtrip(default(MultipleNullableCases))!.Value);
		Assert.Equal("null", this.LastSerializedJson);

		JsonSchemaObject schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<MultipleNullableCases>()!);
		JsonSchemaArray cases = schema["oneOf"]!.AsArray();
		Assert.Single(cases, candidate => candidate!["type"] is JsonSchemaArray types && types.Any(node => node!.GetValue<string>() == "null"));
	}

	[Test]
	public async Task ReferencePreservedCollectionCase_RoundTrips()
	{
		this.Serializer = this.Serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		ArrayOrDog value = new([1, 2]);
		Assert.Equal(value.Value, this.Roundtrip(value)!.Value);

		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, value);
		stream.Position = 0;
		Assert.Equal(value.Value, (await this.Serializer.DeserializeAsync<ArrayOrDog>(stream))!.Value);

		JsonSchemaObject schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<ArrayOrDog>()!);
		Assert.Contains("\"$id\":", schema.ToJsonString(), StringComparison.Ordinal);
		Assert.Contains("\"$value\":", schema.ToJsonString(), StringComparison.Ordinal);
	}

	[Test]
	public void ReferencePreservedObjectCase_UsesIdentityEnvelopeSchema()
	{
		this.Serializer = this.Serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		JsonSchemaObject schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Pet>()!);
		Assert.Contains("\"$id\":", schema.ToJsonString(), StringComparison.Ordinal);
		Assert.Contains("\"$value\":", schema.ToJsonString(), StringComparison.Ordinal);
	}

	[Test]
	public void CollectionCases_SelectEnvelopeThatAvoidsCollisions()
	{
		ArrayOrDog array = new([1, 2]);
		Assert.Equal("""[1,2]""", this.Serializer.Serialize(array));
		Assert.Equal(array.Value, this.Serializer.Deserialize<ArrayOrDog>("""[1,2]""").Value);

		ArrayOrDog dog = new(new Dog("Rover", 10));
		Assert.Equal("""{"$type":"Dog","name":"Rover","barkVolume":10}""", this.Serializer.Serialize(dog));
		Assert.Equal(dog.Value, this.Serializer.Deserialize<ArrayOrDog>("""{"$type":"Dog","name":"Rover","barkVolume":10}""").Value);

		DictionaryOrDog dictionary = new(new Dictionary<string, int> { ["one"] = 1 });
		Assert.Equal("""{"$type":"Dictionary_String_Int32","$value":{"one":1}}""", this.Serializer.Serialize(dictionary));
		Assert.Equal(dictionary.Value, this.Serializer.Deserialize<DictionaryOrDog>("""{"$type":"Dictionary_String_Int32","$value":{"one":1}}""").Value);

		DictionaryOrDog taggedDog = new(new Dog("Rover", 10));
		Assert.Equal("""{"$type":"Dog","name":"Rover","barkVolume":10}""", this.Serializer.Serialize(taggedDog));
		Assert.Equal(taggedDog.Value, this.Serializer.Deserialize<DictionaryOrDog>("""{"$type":"Dog","name":"Rover","barkVolume":10}""").Value);

		ByteBufferOrDog bytes = new(new byte[] { 1, 2 });
		Assert.Equal("\"AQI=\"", this.Serializer.Serialize(bytes));
		Assert.Equal(bytes.Value, this.Serializer.Deserialize<ByteBufferOrDog>("\"AQI=\"").Value);
	}

	[Test]
	public void DictionaryUnionSchema_UsesPayloadEnvelope()
	{
		JsonSchemaObject schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<DictionaryOrDog>()!);
		string json = schema.ToJsonString();

		Assert.Contains("\"$value\":", json, StringComparison.Ordinal);
		Assert.Contains("\"additionalProperties\":{\"type\":\"integer\"", json, StringComparison.Ordinal);
	}

	[Test]
	public void NullableObjectCase_UsesValueEnvelopeForNullPayload()
	{
		NullableDog value = new((Dog?)null);
		string json = this.Serializer.Serialize(value);
		Assert.Equal("""{"$type":"Dog","$value":null}""", json);
		Assert.Null(this.Serializer.Deserialize<NullableDog>(json).Value);

		JsonSchemaObject schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<NullableDog>()!);
		JsonSchemaObject objectCase = Assert.Single(schema["oneOf"]!.AsArray(), candidate => candidate!["properties"]?["$value"] is not null)!.AsObject();
		IEnumerable<string> valueTypes = objectCase["properties"]!["$value"]!["type"]!.AsArray().Select(static item => item!.GetValue<string>());
		Assert.Contains("object", valueTypes);
		Assert.Contains("null", valueTypes);
	}

	[Test]
	public void UnionSchema_UsesConverterInlineCapability()
	{
		JsonSerializer serializer = this.Serializer with { Converters = [new CustomPayloadConverter()] };
		CustomPayloadUnion value = new(new CustomPayload("payload"));
		string json = serializer.Serialize(value);
		Assert.Equal("""{"$type":"CustomPayload","$value":{"name":"payload"}}""", json);

		JsonSchemaObject schema = serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<CustomPayloadUnion>()!);
		Assert.Contains("\"$value\":{\"type\":\"object\"", schema.ToJsonString(), StringComparison.Ordinal);
	}

	[Test]
	public void RecursiveObjectUnionSchema_InlinesDiscriminator()
	{
		RecursivePayloadUnion value = new(new RecursivePayload { Name = "root", Next = new() { Name = "child" } });
		string json = this.Serializer.Serialize(value);
		Assert.Equal("""{"$type":"RecursivePayload","name":"root","next":{"name":"child","next":null}}""", json);

		JsonSchemaObject schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<RecursivePayloadUnion>()!);
		JsonSchemaObject objectCase = Assert.Single(schema["oneOf"]!.AsArray(), candidate => candidate!["type"]?.GetValue<string>() == "object")!.AsObject();
		Assert.Contains("$type", objectCase["required"]!.AsArray().Select(static item => item!.GetValue<string>()));
		Assert.Contains("\"$ref\":", schema.ToJsonString(), StringComparison.Ordinal);
	}

	[Test]
	public void AmbiguousNativeCases_AreRejected()
	{
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(new Numbers(1)));
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(new TextCases("text")));
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(new Collections(new int[] { 1 })));

		this.Serializer = this.Serializer with
		{
			UnionTypeAliasProvider = new UnionCaseMappingCollection([new(typeof(TextOrDateTime)) { { "date-time", typeof(DateTime) } }]),
		};
		TextOrDateTime value = new(new DateTime(2026, 6, 26, 12, 30, 45, DateTimeKind.Utc));
		string json = """{"$type":"date-time","$value":"2026-06-26T12:30:45.0000000Z"}""";
		Assert.Equal(json, this.Serializer.Serialize(value));
		Assert.Equal(value.Value, this.Serializer.Deserialize<TextOrDateTime>(json).Value);
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
	public async Task NullableObjectCase_AsyncRoundTrips()
	{
		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, new NullableDog((Dog?)null));
		Assert.Equal("""{"$type":"Dog","$value":null}""", Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		Assert.Null((await this.Serializer.DeserializeAsync<NullableDog>(stream))!.Value);
	}

	[Test]
	public async Task AsyncEnumerablePayload()
	{
		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, new StreamingUnion(new StreamingPayload { Values = ValuesAsync() }));
		Assert.Equal(
			"""{"$type":"StreamingPayload","values":[1,2]}""",
			Encoding.UTF8.GetString(stream.ToArray()));

		static async IAsyncEnumerable<int> ValuesAsync()
		{
			yield return 1;
			await Task.Yield();
			yield return 2;
		}
	}

	[Test, MatrixDataSource]
	public async Task AsyncOnlyPayloadConverter(bool integerAlias)
	{
		this.Serializer = this.Serializer with
		{
			Converters = [new AsyncOnlyIntConverter()],
			UnionTypeAliasProvider = integerAlias ? new UnionCaseMappingCollection([new(typeof(Scalar)) { { 7, typeof(int) } }]) : null,
		};
		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, new ScalarHolder { Value = new Scalar(42) });
		string alias = integerAlias ? "7" : "Int32";
		string expected = "{\"value\":{\"$type\":\"" + alias + "\",\"$value\":42}}";
		Assert.Equal(expected, Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		ScalarHolder result = (await this.Serializer.DeserializeAsync<ScalarHolder>(stream))!;
		Assert.Equal(42, result.Value.Value);
	}

	[Test]
	[Arguments("""{"$type":"Int32","$value":42,"$type":"String"}""")]
	[Arguments("""["Int32",42,43]""")]
	[Arguments("""["Unknown",42]""")]
	public async Task AsyncRead_InvalidEnvelope_Throws(string json)
	{
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
		await Assert.ThrowsAsync<JsonSerializationException>(() => this.Serializer.DeserializeAsync<Scalar>(stream).AsTask());
	}

	[Test]
	public async Task AsyncNulls()
	{
		using MemoryStream payloadStream = new();
		await this.Serializer.SerializeAsync(payloadStream, default(Scalar));
		Assert.Equal("null", Encoding.UTF8.GetString(payloadStream.ToArray()));
		payloadStream.Position = 0;
		Assert.Null((await this.Serializer.DeserializeAsync<Scalar>(payloadStream)).Value);

		using MemoryStream unionStream = new();
		await this.Serializer.SerializeAsync<PetClass>(unionStream, null);
		Assert.Equal("null", Encoding.UTF8.GetString(unionStream.ToArray()));
		unionStream.Position = 0;
		Assert.Null(await this.Serializer.DeserializeAsync<PetClass>(unionStream));
	}

	[Test]
	public async Task AsyncReferences()
	{
		this.Serializer = this.Serializer with
		{
			PreserveReferences = ReferencePreservationMode.RejectCycles,
		};
		Dog dog = new("Rover", 10);
		using MemoryStream payloadStream = new();
		await this.Serializer.SerializeAsync(payloadStream, new Pets { First = new Pet(dog), Second = new Pet(dog) });
		payloadStream.Position = 0;
		Pets payloads = (await this.Serializer.DeserializeAsync<Pets>(payloadStream))!;
		Assert.Same(payloads.First.Value, payloads.Second.Value);

		PetClass pet = new(dog);
		using MemoryStream unionStream = new();
		await this.Serializer.SerializeAsync(unionStream, new ClassPets { First = pet, Second = pet });
		unionStream.Position = 0;
		ClassPets unions = (await this.Serializer.DeserializeAsync<ClassPets>(unionStream))!;
		Assert.Same(unions.First, unions.Second);
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
		Assert.Contains("\"type\":\"integer\"", schema);
		Assert.Contains("\"string\"", schema);
		Assert.DoesNotContain("\"const\":\"Int32\"", schema);
		Assert.DoesNotContain("\"const\":null", schema);
	}

	[Test]
	public void Schema_NullPayload()
	{
		Assert.Null(this.Roundtrip(default(Scalar))!.Value);
		Assert.Equal("null", this.LastSerializedJson);
		JsonSchemaObject schema = this.Serializer.GetJsonSchema(TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Scalar>()!);
		JsonSchemaArray cases = schema["oneOf"]!.AsArray();
		JsonSchemaNode? stringCase = Assert.Single(cases, candidate => candidate!["type"] is JsonSchemaArray types && types.Any(node => node!.GetValue<string>() == "string"));
		Assert.Equal("""{"type":["string","null"]}""", stringCase!.ToJsonString());
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
		Assert.Equal("""{"$type":"number","$value":42}""", mapped.Serialize(new CSharpUnions.Result(42)));
		Assert.Equal("hi", mapped.Deserialize<CSharpUnions.Result>("""{"$type":"text","$value":"hi"}""").Value);
		JsonSerializer delegated = CSharpUnions.Examples.CreateSerializerWithDelegate();
		Assert.Equal("""{"$type":"1","$value":42}""", delegated.Serialize(new CSharpUnions.Result(42)));
	}

	[Test]
	public void UnionSet_UsesConfiguredComparer()
	{
		HashSet<Nested> result = this.Roundtrip<HashSet<Nested>, SetWitness>([new Nested(new Nested(true))])!;
		Assert.Equal(true, Assert.IsType<Nested>(Assert.Single(result).Value).Value);
	}

	[Test, MatrixDataSource]
	public async Task RecursiveUnion_DepthLimit(bool asynchronous, bool exceedsLimit)
	{
		const int MaxDepth = 4;
		this.Serializer = this.Serializer with
		{
			StartingContext = this.Serializer.StartingContext with { MaxDepth = MaxDepth },
		};
		int depth = MaxDepth + (exceedsLimit ? 1 : 0);
		Nested value = new(true);
		string json = "true";
		for (int i = 1; i < depth; i++)
		{
			value = new(value);
			json = """{"$type":"Nested","$value":""" + json + "}";
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

	[GenerateShape]
	public partial union StreamingUnion(StreamingPayload, int);

	[GenerateShape]
	public partial union ArrayOrDog(int[], Dog);

	[GenerateShape]
	public partial union DictionaryOrDog(Dictionary<string, int>, Dog);

	[GenerateShape]
	public partial union ByteBufferOrDog(byte[], Dog);

	[GenerateShape]
	public partial union MultipleNullableCases(string?, int[]?);

	[GenerateShape]
	public partial union Numbers(int, long);

	[GenerateShape]
	public partial union TextCases(string, char);

	[GenerateShape]
	public partial union TextOrDateTime(string, DateTime);

	[GenerateShape]
	public partial union Collections(int[], List<int>);

	[GenerateShape]
	public partial union NullableDog(Dog?, int);

	[GenerateShape]
	public partial union CustomPayloadUnion(CustomPayload, int);

	[GenerateShape]
	public partial union RecursivePayloadUnion(RecursivePayload);

	[GenerateShape]
	public partial union RecursiveObjectUnion(RecursiveUnionContainingObject);

	[GenerateShape]
	public partial union FirstRecursiveObjectUnion(MultipleRecursiveUnionCases);

	[GenerateShape]
	public partial union SecondRecursiveObjectUnion(MultipleRecursiveUnionCases);

	[GenerateShape]
	public partial union UppercaseTypeUnion(UppercaseTypeProperty, int);

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

	[GenerateShape]
	public partial record CustomPayload(string Name);

	[GenerateShape]
	public partial record UppercaseTypeProperty(string Type);

	[GenerateShape]
	public partial class RecursiveUnionContainingObject
	{
		public RecursiveObjectUnion? Next { get; set; }
	}

	[GenerateShape]
	public partial class MultipleRecursiveUnionCases
	{
		public FirstRecursiveObjectUnion? FirstUnion { get; set; }

		public SecondRecursiveObjectUnion? SecondUnion { get; set; }
	}

	[GenerateShape]
	public partial class RecursivePayload
	{
		public string Name { get; set; } = string.Empty;

		public RecursivePayload? Next { get; set; }
	}

	[GenerateShape]
	public partial class RecursivePayloadUnionHolder
	{
		public RecursivePayload? Payload { get; set; }

		public RecursivePayloadUnion Union { get; set; }
	}

	[GenerateShape]
	public partial class RecursiveObjectUnionHolder
	{
		public RecursiveObjectUnion First { get; set; }

		public RecursiveObjectUnion Second { get; set; }
	}

	[GenerateShape]
	public partial class StreamingPayload
	{
		public IAsyncEnumerable<int>? Values { get; set; }
	}

	[GenerateShape]
	public partial class ScalarHolder
	{
		public Scalar Value { get; set; }
	}

	[Union, GenerateShape]
	public sealed partial class PetClass
	{
		public PetClass(Dog value) => this.Value = value;

		public PetClass(Cat value) => this.Value = value;

		public object? Value { get; }
	}

	[Union, GenerateShape]
	public sealed partial class NullableTextClass
	{
		public NullableTextClass(string? value) => this.Value = value;

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

	private sealed class AsyncOnlyIntConverter : JsonConverter<int>
	{
		public override bool PreferAsyncSerialization => true;

		public override int Read(ref JsonReader reader, SerializationContext context) => throw new NotSupportedException();

		public override void Write(ref JsonWriter writer, in int value, SerializationContext context) => throw new NotSupportedException();

		public override async ValueTask<int> ReadAsync(JsonAsyncReader reader, SerializationContext context)
			=> int.Parse(await reader.ReadNumberTokenAsync(context), System.Globalization.CultureInfo.InvariantCulture);

		public override ValueTask WriteAsync(JsonAsyncWriter writer, int value, SerializationContext context)
		{
			JsonWriter syncWriter = writer.CreateWriter();
			syncWriter.Write(value);
			writer.ReturnWriter(ref syncWriter);
			return writer.FlushIfAppropriateAsync(context);
		}
	}

	private sealed class CustomPayloadConverter : JsonConverter<CustomPayload>
	{
		public override void Write(ref JsonWriter writer, in CustomPayload? value, SerializationContext context)
		{
			if (value is null)
			{
				writer.WriteNull();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName("name");
			writer.Write(value.Name);
			writer.WriteEndObject();
		}

		public override CustomPayload? Read(ref JsonReader reader, SerializationContext context)
			=> throw new NotSupportedException();

		public override JsonSchemaObject GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape)
			=> new()
			{
				["type"] = "object",
				["properties"] = new JsonSchemaObject { ["name"] = new JsonSchemaObject { ["type"] = "string" } },
				["additionalProperties"] = false,
			};
	}

	private sealed class UppercaseTypeNamingPolicy : JsonNamingPolicy
	{
		public override string ConvertName(string name) => name == "Type" ? "$TYPE" : name;
	}
}
