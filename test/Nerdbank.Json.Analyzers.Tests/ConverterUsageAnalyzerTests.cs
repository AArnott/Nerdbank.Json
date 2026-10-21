// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.ConverterUsageAnalyzer>;

public class ConverterUsageAnalyzerTests
{
	[Test]
	public Task MissingSchemaOverride()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class {|NBJson032:Converter|} : JsonConverter<int>
			{
				public override int Read(ref JsonReader reader, SerializationContext context) => 0;
				public override void Write(ref JsonWriter writer, int value, SerializationContext context) { }
			}
			""");

	[Test]
	public Task AbstractConverterIsIgnored()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			abstract class Converter : JsonConverter<int> { }
			""");

	[Test]
	public Task InheritedSchemaOverride()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			using PolyType;
			abstract class Base : JsonConverter<int>
			{
				public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape shape) => new();
				public override int Read(ref JsonReader reader, SerializationContext context) => 0;
				public override void Write(ref JsonWriter writer, int value, SerializationContext context) { }
			}
			class Converter : Base { }
			""");

	[Test]
	public Task UnrelatedSchemaMethodDoesNotSatisfyOverride()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class {|NBJson032:Converter|} : JsonConverter<int>
			{
				public void GetJsonSchema() { }
				public override int Read(ref JsonReader reader, SerializationContext context) => 0;
				public override void Write(ref JsonWriter writer, int value, SerializationContext context) { }
			}
			""");

	[Test]
	public Task TopLevelConstructionAndInvocationInConverter()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			using PolyType;
			class Converter : JsonConverter<int>
			{
				private readonly JsonSerializer serializer;
				public Converter(JsonSerializer serializer) { this.serializer = serializer; }
				public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape shape) => new();
				public override int Read(ref JsonReader reader, SerializationContext context)
				{
					_ = {|NBJson030:new JsonSerializer()|};
					return {|NBJson030:serializer.Deserialize<int>("0", ReflectionShape())|};
				}
				public override void Write(ref JsonWriter writer, int value, SerializationContext context) { }
				private static ITypeShape<int> ReflectionShape() => PolyType.ReflectionProvider.ReflectionTypeShapeProvider.Default.GetTypeShapeOrThrow<int>();
			}
			""");

	[Test]
	public Task OrdinarySerializerConsumerIsIgnored()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Consumer
			{
				public JsonSerializer Serializer { get; } = new();
			}
			""");
}
