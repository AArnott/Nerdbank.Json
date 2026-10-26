// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using JsonSchemaArray = System.Text.Json.Nodes.JsonArray;
using JsonSchemaNode = System.Text.Json.Nodes.JsonNode;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;
using JsonSchemaValue = System.Text.Json.Nodes.JsonValue;

public partial class JsonSchemaTests
{
	[Test]
	public void ProviderOverloads_MatchExplicitShapeAndHonorOptions()
	{
		JsonSchemaOptions options = new() { Dialect = JsonSchemaDialect.Draft7 };
		Assert.Equal(
			this.Serializer.GetJsonSchema(Shape<int, Witness>(), options).ToJsonString(),
			this.Serializer.GetJsonSchema<int>(Witness.GeneratedTypeShapeProvider, options).ToJsonString());
		Assert.Equal(
			this.Serializer.GetJsonSchema(Shape<int, Witness>()).ToJsonString(),
			this.Serializer.GetJsonSchema<int>(Witness.GeneratedTypeShapeProvider).ToJsonString());
		Assert.Throws<ArgumentNullException>(() => this.Serializer.GetJsonSchema<int>((ITypeShapeProvider)null!));
		Assert.Throws<ArgumentNullException>(() => this.Serializer.GetJsonSchema<int>(Witness.GeneratedTypeShapeProvider, null!));
		Assert.Throws<ArgumentException>(() => this.Serializer.GetJsonSchema<System.Threading.Thread>(Witness.GeneratedTypeShapeProvider));
	}

	[Test]
	public void SourceGeneratedOverloads_MatchExplicitShapes()
	{
		JsonSchemaOptions options = new() { Dialect = JsonSchemaDialect.Draft7 };
		Assert.Equal(this.Serializer.GetJsonSchema(Shape<Person, Person>()).ToJsonString(), this.Serializer.GetJsonSchema<Person>().ToJsonString());
		Assert.Equal(this.Serializer.GetJsonSchema(Shape<Person, Person>(), options).ToJsonString(), this.Serializer.GetJsonSchema<Person>(options).ToJsonString());
		Assert.Equal(this.Serializer.GetJsonSchema(Shape<int, Witness>()).ToJsonString(), this.Serializer.GetJsonSchema<int, Witness>().ToJsonString());
		Assert.Equal(this.Serializer.GetJsonSchema(Shape<int, Witness>(), options).ToJsonString(), this.Serializer.GetJsonSchema<int, Witness>(options).ToJsonString());
	}

	[Test]
	[Arguments(JsonSchemaDialect.Draft4, "items")]
	[Arguments(JsonSchemaDialect.Draft7, "items")]
	[Arguments(JsonSchemaDialect.Draft2020_12, "prefixItems")]
	public void CustomConverter_TupleHelpersHonorDialectAndOwnership(JsonSchemaDialect dialect, string keyword)
	{
		JsonSerializer serializer = new()
		{
			Converters = ConverterCollection.Create([new CallbackSchemaConverter(context =>
			{
				Assert.Throws<ArgumentException>(() => context.CreateTupleSchema(new JsonSchemaArray()));
				JsonSchemaArray positions = new() { (JsonSchemaNode)context.GetJsonSchema(Shape<int, Witness>()) };
				JsonSchemaObject tuple = context.CreateTupleSchema(positions);
				Assert.Same(positions, tuple[keyword]);
				Assert.Equal("array", tuple["type"]!.GetValue<string>());

				JsonSchemaObject existing = new() { ["description"] = "Preserved", ["minItems"] = 1 };
				context.ApplyTupleSchema(existing, new JsonSchemaArray { (JsonSchemaNode)new JsonSchemaObject { ["type"] = "string" } });
				Assert.Null(existing["type"]);
				Assert.Equal("Preserved", existing["description"]!.GetValue<string>());
				Assert.Equal(1, existing["minItems"]!.GetValue<int>());
				return tuple;
			}),]),
		};
		JsonSchemaObject schema = serializer.GetJsonSchema(Shape<Opaque, Opaque>(), new JsonSchemaOptions { Dialect = dialect });
		Assert.Contains(keyword, schema.ToJsonString(), StringComparison.Ordinal);
	}

	[Test]
	public void CustomConverter_NestedSchemasRetainRecursiveDefinitions()
	{
		JsonSerializer serializer = new()
		{
			Converters = ConverterCollection.Create([new CallbackSchemaConverter(context => new JsonSchemaObject
			{
				["type"] = "array",
				["items"] = context.GetJsonSchema(Shape<TreeNode, TreeNode>()),
			}),]),
		};
		JsonSchemaObject schema = serializer.GetJsonSchema(Shape<Opaque, Opaque>());
		Assert.Contains("#/$defs/TreeNode", schema.ToJsonString(), StringComparison.Ordinal);
		Assert.NotNull(schema["$defs"]);
	}

	[Test]
	public void CustomFragments_AreNotSilentlyTranslatedBetweenDialects()
	{
		JsonSerializer serializer = new()
		{
			Converters = ConverterCollection.Create([new CallbackSchemaConverter(context => context.Dialect == JsonSchemaDialect.Draft4
				? throw new NotSupportedException("Draft 4 is not supported by this converter.")
				: new JsonSchemaObject { ["const"] = "opaque", ["customKeyword"] = new JsonSchemaArray(1, true, "text") }),]),
		};
		Assert.Throws<NotSupportedException>(() => serializer.GetJsonSchema(Shape<Opaque, Opaque>(), new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft4 }));
		Assert.Contains("customKeyword", serializer.GetJsonSchema(Shape<Opaque, Opaque>()).ToJsonString(), StringComparison.Ordinal);
	}

	[Test]
	public void ProtectedNullabilityHelper_HandlesCompositionsEnumsAndUnknownFragments()
	{
		JsonSchemaObject nullableEnum = CallbackSchemaConverter.Nullable(new JsonSchemaObject { ["type"] = "string", ["enum"] = new JsonSchemaArray("a", "b") });
		Assert.Equal("null", nullableEnum["anyOf"]![1]!["type"]!.GetValue<string>());
		Assert.Equal("a", nullableEnum["anyOf"]![0]!["enum"]![0]!.GetValue<string>());
		JsonSchemaObject unknown = new();
		Assert.Empty(CallbackSchemaConverter.Nullable(unknown));
		JsonSchemaObject reference = CallbackSchemaConverter.Nullable(new JsonSchemaObject { ["$ref"] = "#/$defs/Value" });
		Assert.Equal("#/$defs/Value", reference["anyOf"]![0]!["$ref"]!.GetValue<string>());
		JsonSchemaObject alreadyNullable = new() { ["type"] = new JsonSchemaArray("string", "null") };
		Assert.Equal(2, CallbackSchemaConverter.Nullable(alreadyNullable)["type"]!.AsArray().Count);
		Assert.NotNull(CallbackSchemaConverter.Undocumented(typeof(CallbackSchemaConverter))["$comment"]);
	}

	[Test]
	public void ProtectedValueHelper_SupportsBoxedPrimitivesWithoutReflection()
	{
		(object? Value, string Json)[] values =
		[
			(null, "null"), ("text", "\"text\""), ((short)-2, "-2"), (3, "3"), (4L, "4"),
			(1.5f, "1.5"), (2.5d, "2.5"), (3.5m, "3.5"), (true, "true"), ((byte)5, "5"),
			((sbyte)-6, "-6"), ((ushort)7, "7"), (8U, "8"), (ulong.MaxValue, "18446744073709551615"), ('x', "\"x\""),
		];
		foreach ((object? value, string json) in values)
		{
			Assert.Equal(json, CallbackSchemaConverter.Value(value)?.ToJsonString() ?? "null");
		}

		Assert.Throws<NotSupportedException>(() => CallbackSchemaConverter.Value(new object()));
	}

	private sealed class CallbackSchemaConverter(Func<JsonSchemaContext, JsonSchemaObject> callback) : JsonConverter<Opaque>
	{
		public static JsonSchemaObject Nullable(JsonSchemaObject schema) => ApplyJsonSchemaNullability(schema);

		public static JsonSchemaObject Undocumented(Type converter) => CreateUndocumentedSchema(converter);

		public static JsonSchemaValue? Value(object? value) => CreateJsonValue(value);

		public override JsonSchemaObject? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => callback(context);

		public override void Write(ref JsonWriter writer, in Opaque? value, SerializationContext context) => throw new NotSupportedException();

		public override Opaque? Read(ref JsonReader reader, SerializationContext context) => throw new NotSupportedException();
	}
}
