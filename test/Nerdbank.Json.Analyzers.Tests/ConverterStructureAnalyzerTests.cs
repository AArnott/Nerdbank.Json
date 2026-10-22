// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.ConverterStructureAnalyzer>;

public class ConverterStructureAnalyzerTests
{
	[Test]
	[Arguments("")]
	[Arguments("writer.Write(1); writer.Write(2);")]
	[Arguments("writer.WriteStartArray();")]
	[Arguments("writer.WriteStartArray(); writer.WriteEndObject();")]
	[Arguments("writer.WriteStartObject(); writer.WritePropertyName(\"x\"); writer.WriteEndObject();")]
	[Arguments("if (value == 0) return; writer.Write(value);")]
	[Arguments("ref JsonWriter alias = ref writer; alias.Write(1); alias.Write(2);")]
	[Arguments("writer.WriteStartObject(); writer.Write(1); writer.WriteEndObject();")]
	[Arguments("void Other() { } Other();")]
	public Task InvalidWrite([StringSyntax("c#-test")] string body)
		=> VerifyWrite(body, invalid: true);

	[Test]
	[Arguments("writer.Write(value);")]
	[Arguments("writer.WriteNull();")]
	[Arguments("writer.WriteStartArray(); writer.Write(1); writer.Write(2); writer.WriteEndArray();")]
	[Arguments("writer.WriteStartObject(); writer.WritePropertyName(\"x\"); writer.WriteStartArray(); writer.Write(value); writer.WriteEndArray(); writer.WriteEndObject();")]
	[Arguments("if (value == 0) writer.WriteNull(); else writer.Write(value);")]
	[Arguments("if (value == 0) throw new System.Exception(); writer.Write(value);")]
	[Arguments("for (int i = 0; i < value; i++) { writer.Write(i); } writer.Write(value);")]
	[Arguments("writer.WriteRaw(\"1\");")]
	[Arguments("Helper(ref writer);")]
	[Arguments("nested.Write(ref writer, in value, context);")]
	[Arguments("ref JsonWriter alias = ref writer; alias.Write(value);")]
	[Arguments("try { writer.Write(value); } finally { writer.Flush(); }")]
	[Arguments("Fail();")]
	[Arguments("switch ((System.DayOfWeek)value) { case System.DayOfWeek.Sunday: case System.DayOfWeek.Monday: case System.DayOfWeek.Tuesday: case System.DayOfWeek.Wednesday: case System.DayOfWeek.Thursday: case System.DayOfWeek.Friday: case System.DayOfWeek.Saturday: writer.Write(value); break; }")]
	public Task ValidOrUnknownWrite([StringSyntax("c#-test")] string body)
		=> VerifyWrite(body, invalid: false);

	[Test]
	[Arguments("return 0;")]
	[Arguments("reader.ReadInt32(); return reader.ReadInt32();")]
	[Arguments("reader.ReadStartArray(); return 0;")]
	[Arguments("var peek = reader.CreatePeekReader(); return peek.ReadInt32();")]
	[Arguments("if (reader.TryReadNull()) { reader.ReadInt32(); } return reader.ReadInt32();")]
	[Arguments("ref JsonReader alias = ref reader; alias.ReadInt32(); return reader.ReadInt32();")]
	public Task InvalidRead([StringSyntax("c#-test")] string body)
		=> VerifyRead(body, invalid: true);

	[Test]
	[Arguments("return reader.ReadInt32();")]
	[Arguments("if (reader.TryReadNull()) return 0; return reader.ReadInt32();")]
	[Arguments("if (!reader.TryReadNull()) return reader.ReadInt32(); return 0;")]
	[Arguments("reader.ReadStartArray(); while (!reader.TryReadEndArray()) { reader.Skip(context); } return 0;")]
	[Arguments("reader.ReadStartArray(); if (!reader.TryReadEndArray()) { while (true) { reader.ReadInt32(); if (reader.TryReadEndArray()) break; reader.ReadValueSeparator(); } } return 0;")]
	[Arguments("reader.ReadStartObject(); while (!reader.TryReadEndObject()) { reader.ReadString(); reader.ReadNameSeparator(); reader.Skip(context); } return 0;")]
	[Arguments("reader.TryReadNull(); return 0;")]
	[Arguments("return nested.Read(ref reader, context);")]
	[Arguments("var peek = reader.CreatePeekReader(); peek.ReadInt32(); return reader.ReadInt32();")]
	[Arguments("ref JsonReader alias = ref reader; return alias.ReadInt32();")]
	[Arguments("throw new System.NotSupportedException();")]
	[Arguments("JsonReader copy = reader; copy.ReadInt32(); reader = copy; return 0;")]
	public Task ValidOrUnknownRead([StringSyntax("c#-test")] string body)
		=> VerifyRead(body, invalid: false);

	[Test]
	public Task UnrelatedMethodsAndAsyncOverrides()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			using System.Threading.Tasks;
			abstract class Base
			{
				public abstract int Read(ref JsonReader reader, SerializationContext context);
			}
			class Ordinary : Base
			{
				public override int Read(ref JsonReader reader, SerializationContext context) => 0;
			}
			abstract class Converter : JsonConverter<int>
			{
				public override ValueTask<int> ReadAsync(JsonAsyncReader reader, SerializationContext context) => new(0);
				public override ValueTask WriteAsync(JsonAsyncWriter writer, int value, SerializationContext context) => default;
			}
			""");

	private static Task VerifyWrite([StringSyntax("c#-test")] string body, bool invalid)
		=> VerifyCS.VerifyAnalyzerAsync(
			$$"""
			using Nerdbank.Json;
			abstract class Converter : JsonConverter<int>
			{
				protected readonly JsonConverter<int> nested = null!;
				public override void {{(invalid ? "{|NBJson031:Write|}" : "Write")}}(ref JsonWriter writer, in int value, SerializationContext context)
				{
					{{body}}
				}
				private static void Helper(ref JsonWriter writer) => writer.Write(1);
				[System.Diagnostics.CodeAnalysis.DoesNotReturn]
				private static void Fail() => throw new System.NotSupportedException();
			}
			""");

	private static Task VerifyRead([StringSyntax("c#-test")] string body, bool invalid)
		=> VerifyCS.VerifyAnalyzerAsync(
			$$"""
			using Nerdbank.Json;
			abstract class Converter : JsonConverter<int>
			{
				protected readonly JsonConverter<int> nested = null!;
				public override int {{(invalid ? "{|NBJson031:Read|}" : "Read")}}(ref JsonReader reader, SerializationContext context)
				{
					{{body}}
				}
			}
			""");
}
