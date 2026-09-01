// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Dynamic;
using System.Text.Json;
using PolyType;
using JsonSerializer = Nerdbank.Json.JsonSerializer;
using StjNode = System.Text.Json.Nodes.JsonNode;

public partial class JsonUntypedConvertersTests : TestBase
{
	private readonly JsonSerializer untyped = new JsonSerializer().WithUntypedConverters();

	[Test]
	public void Object_TopLevel_DeserializesToJsonValue()
	{
		object? value = this.untyped.Deserialize<object, Witness>("""{"a":1,"b":[true,null]}""");
		JsonValue jsonValue = Assert.IsType<JsonObject>(value);
		Assert.Equal(2, ((JsonObject)jsonValue).Count);
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
		Assert.Throws<NotSupportedException>(() => this.untyped.Serialize<object, Witness>(new Random()));
	}

	[Test]
	public void Object_Member_RoundTrips()
	{
		Holder holder = new() { Value = this.untyped.DeserializeJsonValue("[1,2,3]"), Tag = "t" };
		string json = this.untyped.Serialize(holder);
		Assert.Equal("""{"value":[1,2,3],"tag":"t"}""", json);

		Holder? result = this.untyped.Deserialize<Holder>(json);
		Assert.NotNull(result);
		JsonArray arr = Assert.IsType<JsonArray>(result.Value);
		Assert.Equal(3, arr.Count);
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
		Assert.Throws<NotSupportedException>(() => plain.Serialize<object, Witness>(JsonValue.Create(true)));
	}

	[Test]
	public void Expando_RoundTrips()
	{
		object? value = this.untyped.Deserialize<ExpandoObject, Witness>("""{"name":"Ada","age":36,"active":true}""");
		ExpandoObject expando = Assert.IsType<ExpandoObject>(value);
		IDictionary<string, object?> members = expando;
		Assert.Equal(3, members.Count);
		Assert.Equal("Ada", ((JsonString)members["name"]!).Value);
		Assert.Equal(36, ((JsonNumber)members["age"]!).GetInt64());

		string json = this.untyped.Serialize<ExpandoObject, Witness>(expando);
		Assert.Equal("""{"name":"Ada","age":36,"active":true}""", json);
	}

	[Test]
	public void Expando_MemberCountLimit_Enforced()
	{
		JsonSerializer limited = new JsonSerializer
		{
			StartingContext = new SerializationContext { Security = new SecuritySettings { MaxObjectMemberCount = 1 } },
		}.WithUntypedConverters();
		Assert.Throws<FormatException>(() => limited.Deserialize<ExpandoObject, Witness>("""{"a":1,"b":2}"""));
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
	private partial class Witness;
}
