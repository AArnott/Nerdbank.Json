// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.JsonConverterAttributeAnalyzer>;

public class JsonConverterAttributeAnalyzerTests
{
	private const string ConverterBase = /* lang=c#-test */ """
		abstract class Stub<T> : Nerdbank.Json.JsonConverter<T>
		{
			public override T? Read(ref Nerdbank.Json.JsonReader reader, Nerdbank.Json.SerializationContext context) => default;
			public override void Write(ref Nerdbank.Json.JsonWriter writer, in T? value, Nerdbank.Json.SerializationContext context) { }
		}
		""";

	[Test]
	public Task CompatibleInheritedConverter()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			[JsonConverter(typeof(ModelConverter))]
			class Model { }
			class ModelConverter : Stub<Model> { }
			""" + ConverterBase);

	[Test]
	public Task WrongGenericArgument()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			[JsonConverter({|NBJson020:typeof(Wrong)|})]
			class Model { }
			class Wrong : Stub<string> { }
			""" + ConverterBase);

	[Test]
	public Task AbstractConverter()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			[JsonConverter({|NBJson020:typeof(Abstract)|})]
			class Model { }
			abstract class Abstract : Stub<Model> { }
			""" + ConverterBase);

	[Test]
	public Task MissingPublicParameterlessConstructor()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			[JsonConverter({|NBJson021:typeof(Private)|})]
			class Model { }
			class Private : Stub<Model> { private Private() { } }
			""" + ConverterBase);

	[Test]
	public Task OptionalArgumentIsNotParameterlessConstructor()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			[JsonConverter({|NBJson021:typeof(Optional)|})]
			class Model { }
			class Optional : Stub<Model> { public Optional(int value = 0) { } }
			""" + ConverterBase);

	[Test]
	public Task MemberAndParameterAttributes()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Alias = Nerdbank.Json.JsonConverterAttribute;
			class Model
			{
				[Alias({|NBJson020:typeof(Wrong)|})]
				public int Field = 0;
				[Alias({|NBJson020:typeof(Wrong)|})]
				public int Property { get; set; }
				public Model([Alias({|NBJson020:typeof(Wrong)|})] int parameter) { }
			}
			class Wrong : Stub<string> { }
			""" + ConverterBase);

	[Test]
	public Task UnboundGenericConverterIsClosedOverModelArguments()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			[JsonConverter(typeof(GenericConverter<>))]
			class Model<T> { }
			class GenericConverter<T> : Stub<Model<T>> { }
			""" + ConverterBase);

	[Test]
	public Task UnboundGenericArityMismatch()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			[JsonConverter({|NBJson020:typeof(GenericConverter<,>)|})]
			class Model<T> { }
			class GenericConverter<T, U> : Stub<Model<T>> { }
			""" + ConverterBase);

	[Test]
	public Task UnrelatedAttributeIsIgnored()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			[Other(typeof(string))]
			class Model { }
			class OtherAttribute(Type converter) : Attribute
			{
				public Type Converter { get; } = converter;
			}
			""");
}
