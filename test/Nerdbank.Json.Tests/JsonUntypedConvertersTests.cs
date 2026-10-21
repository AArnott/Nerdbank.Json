// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Dynamic;
using System.Text.Json;
using PolyType;
using PolyType.Abstractions;
using JsonSerializer = Nerdbank.Json.JsonSerializer;
using StjNode = System.Text.Json.Nodes.JsonNode;

public partial class JsonUntypedConvertersTests : TestBase
{
	private readonly JsonSerializer untyped = new JsonSerializer().WithObjectConverter().WithExpandoObjectConverter().WithJsonValueConverter();

	[Test]
	public void Object_TopLevel_DeserializesToPrimitives()
	{
		object? value = this.untyped.Deserialize<object, Witness>("""{"a":1,"b":[true,null]}""");
		IReadOnlyDictionary<object, object?> members = Assert.IsAssignableFrom<IReadOnlyDictionary<object, object?>>(value);
		Assert.Equal(2, members.Count);
		Assert.Equal(1UL, members["a"]);
		Assert.Equal(new object?[] { true, null }, Assert.IsType<object[]>(members["b"]));
	}

	[Test]
	public void Object_Null_DeserializesToClrNull()
	{
		Assert.Null(this.untyped.Deserialize<object, Witness>("null"));
	}

	[Test]
	public async Task Object_Null_DeserializesToClrNull_Async()
	{
		using MemoryStream stream = new("null"u8.ToArray());
		Assert.Null(await this.untyped.DeserializeAsync(stream, Shape<object, Witness>()));
	}

	[Test]
	public void Object_MemberNull_DeserializesToClrNull()
	{
		Holder? holder = this.untyped.Deserialize<Holder>("""{"value":null}""");
		Assert.NotNull(holder);
		Assert.Null(holder.Value);
	}

	[Test]
	public void Object_TopLevel_SerializesJsonValue()
	{
		JsonValue? value = this.untyped.DeserializeJsonValue("""{"x":[1,2]}""");
		string json = this.untyped.Serialize<object, Witness>(value);
		Assert.Equal("""{"x":[1,2]}""", json);
	}

	[Test]
	[Arguments(42, "42")]
	[Arguments(true, "true")]
	[Arguments("hi", "\"hi\"")]
	[Arguments(3.5, "3.5")]
	public void Object_SerializesBoxedPrimitive(object value, string expected)
	{
		Assert.Equal(expected, this.untyped.Serialize<object, Witness>(value));
	}

	[Test]
	public void Object_SerializeArbitraryType_Throws()
	{
		Assert.Throws<JsonSerializationException>(() => this.untyped.Serialize<object, Witness>(new Random()));
	}

	[Test]
	public void Object_Member_RoundTrips()
	{
		Holder holder = new() { Value = this.untyped.DeserializeJsonValue("[1,2,3]"), Tag = "t" };
		string json = this.untyped.Serialize(holder);
		Assert.Equal("""{"value":[1,2,3],"tag":"t"}""", json);

		Holder? result = this.untyped.Deserialize<Holder>(json);
		Assert.NotNull(result);
		object?[] arr = Assert.IsType<object[]>(result.Value);
		Assert.Equal(3, arr.Length);
		Assert.Equal("t", result.Tag);
	}

	[Test]
	public void JsonValue_Member_RoundTrips()
	{
		NodeHolder holder = new() { Node = this.untyped.DeserializeJsonValue("""{"k":true}""") };
		string json = this.untyped.Serialize(holder);
		Assert.Equal("""{"node":{"k":true}}""", json);

		NodeHolder? result = this.untyped.Deserialize<NodeHolder>(json);
		Assert.Equal(holder.Node, result!.Node);
	}

	[Test]
	public void Object_WithoutOptIn_CannotSerializeArbitraryObject()
	{
		JsonSerializer plain = new();
		Assert.Throws<JsonSerializationException>(() => plain.Serialize<object, Witness>(JsonValue.Create(true)));
	}

	[Test]
	public void Expando_RoundTrips()
	{
		object? value = this.untyped.Deserialize<ExpandoObject, Witness>("""{"name":"Ada","age":36,"active":true}""");
		ExpandoObject expando = Assert.IsType<ExpandoObject>(value);
		IDictionary<string, object?> members = expando;
		Assert.Equal(3, members.Count);
		Assert.Equal("Ada", members["name"]);
		Assert.Equal(36UL, members["age"]);

		string json = this.untyped.Serialize<ExpandoObject, Witness>(expando);
		Assert.Equal("""{"name":"Ada","age":36,"active":true}""", json);
	}

	[Test]
	public void Expando_MemberCountLimit_Enforced()
	{
		JsonSerializer limited = new JsonSerializer
		{
			StartingContext = new SerializationContext { Security = new SecuritySettings { MaxObjectMemberCount = 1 } },
		}.WithObjectConverter().WithExpandoObjectConverter();
		Assert.Throws<JsonSerializationException>(() => limited.Deserialize<ExpandoObject, Witness>("""{"a":1,"b":2}"""));
	}

	[Test]
	public void Stj_JsonNode_RoundTrips()
	{
		JsonSerializer stj = new JsonSerializer().WithSystemTextJsonConverters();
		StjNode? node = stj.Deserialize<StjNode, Witness>("""{"a":1,"b":[true,null]}""");
		Assert.NotNull(node);
		Assert.Equal(1, (int)node!["a"]!);
		string json = stj.Serialize<StjNode, Witness>(node);
		Assert.Equal("""{"a":1,"b":[true,null]}""", json);
	}

	[Test]
	public void Stj_JsonElement_RoundTrips()
	{
		JsonSerializer stj = new JsonSerializer().WithSystemTextJsonConverters();
		JsonElement element = stj.Deserialize<JsonElement, Witness>("""[1,2,3]""");
		Assert.Equal(System.Text.Json.JsonValueKind.Array, element.ValueKind);
		string json = stj.Serialize<JsonElement, Witness>(element);
		Assert.Equal("[1,2,3]", json);
	}

	[Test]
	public void Stj_JsonDocument_RoundTrips()
	{
		JsonSerializer stj = new JsonSerializer().WithSystemTextJsonConverters();
		using JsonDocument? doc = stj.Deserialize<JsonDocument, Witness>("""{"n":5}""");
		Assert.NotNull(doc);
		Assert.Equal(5, doc!.RootElement.GetProperty("n").GetInt32());
		string json = stj.Serialize<JsonDocument, Witness>(doc);
		Assert.Equal("""{"n":5}""", json);
	}

	[Test]
	public void Object_DuplicateProperty_Rejected()
	{
		Assert.Throws<JsonSerializationException>(() => this.untyped.Deserialize<object, Witness>("""{"a":1,"a":2}"""));
	}

	[Test]
	public void RuntimeShapedObjectUsesContextProvider()
	{
		Holder value = new() { Value = new RuntimeModel { Name = "Ada" } };
		Assert.Equal("""{"value":{"name":"Ada"},"tag":null}""", this.untyped.Serialize(value));
	}

	[Test]
	public async Task RuntimeShapedObjectAsync()
	{
		using MemoryStream stream = new();
		await this.untyped.SerializeAsync(stream, (object)new RuntimeModel { Name = "Ada" }, Shape<object, Witness>());
		Assert.Equal("""{"name":"Ada"}""", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
	}

	[Test]
	public void DynamicObjectAndNestedExpandoMembers()
	{
		JsonSerializer serializer = new JsonSerializer().WithDynamicObjectConverter();
		object? value = serializer.Deserialize<object, Witness>("""{"name":"Ada","nested":{"active":true}}""");
		DynamicObject dynamicObject = Assert.IsAssignableFrom<DynamicObject>(value);
		Assert.Contains("name", dynamicObject.GetDynamicMemberNames());
		IReadOnlyDictionary<object, object?> map = Assert.IsAssignableFrom<IReadOnlyDictionary<object, object?>>(value);
		Assert.IsAssignableFrom<DynamicObject>(map["nested"]);
		Assert.Equal("""{"name":"Ada","nested":{"active":true}}""", serializer.Serialize<object, Witness>(value));

		ExpandoObject expando = this.untyped.Deserialize<ExpandoObject, Witness>("""{"nested":{"active":true}}""")!;
		Assert.IsAssignableFrom<DynamicObject>(((IDictionary<string, object?>)expando)["nested"]);
	}

	[Test]
	public void GranularRegistrationsRejectDuplicates()
	{
		Assert.Throws<ArgumentException>(() => this.untyped.WithObjectConverter());
		Assert.Throws<ArgumentException>(() => this.untyped.WithDynamicObjectConverter());
		Assert.Throws<ArgumentException>(() => this.untyped.WithExpandoObjectConverter());
		Assert.Throws<ArgumentException>(() => this.untyped.WithJsonValueConverter());
		JsonSerializer serializer = new JsonSerializer().WithSystemTextJsonConverters();
		Assert.Throws<ArgumentException>(() => serializer.WithSystemTextJsonConverters());
	}

	[Test]
	public void PrimitiveNumberTypesAndNativeNumberFidelity()
	{
		Assert.IsType<ulong>(this.untyped.Deserialize<object, Witness>("18446744073709551615"));
		Assert.IsType<long>(this.untyped.Deserialize<object, Witness>("-9223372036854775808"));
		Assert.IsType<double>(this.untyped.Deserialize<object, Witness>("1.5"));
		Assert.Throws<JsonSerializationException>(() => this.untyped.Deserialize<object, Witness>("1e999"));
		JsonNumber number = this.untyped.Deserialize<JsonNumber, Witness>("123456789012345678901234567890.00100e99")!;
		Assert.Equal("123456789012345678901234567890.00100e99", number.RawToken);
		Assert.Equal(number.RawToken, this.untyped.Serialize<JsonNumber, Witness>(number));
	}

	[Test]
	[Arguments(ReferencePreservationMode.AllowCycles)]
	[Arguments(ReferencePreservationMode.RejectCycles)]
	public void RuntimeShapedObjectIsWrappedOnlyOnce(ReferencePreservationMode mode)
	{
		JsonSerializer serializer = this.untyped with { PreserveReferences = mode };
		string json = serializer.Serialize<object, Witness>(new RuntimeModel { Name = "Ada" });
		Assert.Equal("""{"$id":1,"$value":{"name":"Ada"}}""", json);
		IReadOnlyDictionary<object, object?> result = Assert.IsAssignableFrom<IReadOnlyDictionary<object, object?>>(serializer.Deserialize<object, Witness>(json));
		Assert.Equal("Ada", result["name"]);
	}

	[Test]
	public async Task RuntimeShapedObjectCycleAsync()
	{
		JsonSerializer serializer = this.untyped with { PreserveReferences = ReferencePreservationMode.AllowCycles };
		ReferenceRuntimeModel value = new();
		value.Next = value;
		using MemoryStream stream = new();
		await serializer.SerializeAsync(stream, (object)value, Shape<object, Witness>());
		Assert.Equal("""{"$id":1,"$value":{"next":{"$ref":1}}}""", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		object? result = await serializer.DeserializeAsync(stream, Shape<object, Witness>());
		IReadOnlyDictionary<object, object?> dictionary = Assert.IsAssignableFrom<IReadOnlyDictionary<object, object?>>(result);
		Assert.Same(result, dictionary["next"]);
	}

	[Test]
	public void DictionaryAndExpandoCycles()
	{
		JsonSerializer serializer = this.untyped with { PreserveReferences = ReferencePreservationMode.AllowCycles };
		Dictionary<object, object?> dictionary = [];
		dictionary["self"] = dictionary;
		string json = serializer.Serialize<object, Witness>(dictionary);
		object? result = serializer.Deserialize<object, Witness>(json);
		Assert.Same(result, Assert.IsAssignableFrom<IReadOnlyDictionary<object, object?>>(result)["self"]);

		ExpandoObject expando = new();
		((IDictionary<string, object?>)expando)["self"] = expando;
		json = serializer.Serialize<ExpandoObject, Witness>(expando);
		ExpandoObject roundTrip = serializer.Deserialize<ExpandoObject, Witness>(json)!;
		Assert.Same(roundTrip, ((IDictionary<string, object?>)roundTrip)["self"]);
	}

	private static ITypeShape<T> Shape<T, TProvider>()
#if NET
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#endif

	[GenerateShape]
	public partial class RuntimeModel
	{
		public string? Name { get; set; }
	}

	[GenerateShape]
	public partial class ReferenceRuntimeModel
	{
		public object? Next { get; set; }
	}

	[GenerateShape]
	public partial class Holder
	{
		public object? Value { get; set; }

		public string? Tag { get; set; }
	}

	[GenerateShape]
	public partial class NodeHolder
	{
		public JsonValue? Node { get; set; }
	}

	[GenerateShapeFor<object>]
	[GenerateShapeFor<ExpandoObject>]
	[GenerateShapeFor<StjNode>]
	[GenerateShapeFor<JsonElement>]
	[GenerateShapeFor<JsonDocument>]
	[GenerateShapeFor<JsonNumber>]
	[GenerateShapeFor<RuntimeModel>]
	private partial class Witness;
}
