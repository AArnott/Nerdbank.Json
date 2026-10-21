// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.JsonShapeUsageAnalyzer>;

public class JsonShapeUsageAnalyzerTests
{
	[Test]
	[Arguments("System.Action")]
	[Arguments("System.Delegate")]
	[Arguments("System.MulticastDelegate")]
	[Arguments("System.Func<int>")]
	[Arguments("System.Action[]")]
	[Arguments("System.Collections.Generic.List<System.Action>")]
	[Arguments("(System.Action Callback, int Number)")]
	public Task DelegateValuesInGeneratedContracts([StringSyntax("c#-test")] string memberType)
		=> VerifyCS.VerifyAnalyzerAsync(
			$$"""
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				public {{memberType}}? {|NBJson001:Callback|} { get; set; }
			}
			""");

	[Test]
	[Arguments("object")]
	[Arguments("System.IntPtr")]
	[Arguments("System.Uri")]
	[Arguments("(int X, int Y)")]
	public Task UnsupportedDictionaryKeys([StringSyntax("c#-test")] string keyType)
		=> VerifyCS.VerifyAnalyzerAsync(
			$$"""
			using PolyType;
			using System.Collections.Generic;
			[GenerateShape]
			partial class Model
			{
				public Dictionary<{{keyType}}, int> {|NBJson002:Values|} { get; set; } = new();
			}
			""");

	[Test]
	[Arguments("string")]
	[Arguments("char")]
	[Arguments("bool")]
	[Arguments("byte")]
	[Arguments("sbyte")]
	[Arguments("short")]
	[Arguments("ushort")]
	[Arguments("int")]
	[Arguments("uint")]
	[Arguments("long")]
	[Arguments("ulong")]
	[Arguments("float")]
	[Arguments("double")]
	[Arguments("decimal")]
	[Arguments("System.Numerics.BigInteger")]
	[Arguments("System.DateTime")]
	[Arguments("System.DateTimeOffset")]
	[Arguments("System.TimeSpan")]
	[Arguments("System.Guid")]
	[Arguments("System.DayOfWeek")]
	[Arguments("System.Half")]
	[Arguments("System.Int128")]
	[Arguments("System.UInt128")]
	[Arguments("System.DateOnly")]
	[Arguments("System.TimeOnly")]
	[Arguments("System.Text.Rune")]
	public Task EverySupportedDictionaryKey([StringSyntax("c#-test")] string keyType)
		=> VerifyCS.VerifyAnalyzerAsync(
			$$"""
			using PolyType;
			using System.Collections.Generic;
			[GenerateShape]
			partial class Model
			{
				public Dictionary<{{keyType}}, int> Values { get; set; } = new();
			}
			""");

	[Test]
	public Task WitnessCollectionRootAndNestedMembers()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using System.Collections.Generic;
			using PolyType;
			[{|NBJson002:GenerateShapeFor<Dictionary<object, int>>|}]
			[GenerateShapeFor(typeof(List<Child>))]
			partial class Witness;
			class Child { public Action? {|NBJson001:Callback|} { get; set; } }
			""");

	[Test]
	public Task ClosedGenericNullableAndRecursiveGraphs()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using PolyType;
			[GenerateShapeFor<Box<Action>>]
			partial class Witness;
			class Box<T>
			{
				public T? {|NBJson001:Value|} { get; set; }
				public Box<T>? Next { get; set; }
			}
			""");

	[Test]
	public Task WildcardWitnessAndInheritedMemberAreDeduplicated()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using PolyType;
			[GenerateShapeFor("Models.?at*")]
			partial class Witness;
			namespace Models
			{
				class Base { public Action? {|NBJson001:Callback|} { get; set; } }
				class Matched : Base { }
				class MatchedToo : Base { }
				class Unmatched { public Action? Callback { get; set; } }
			}
			""");

	[Test]
	public Task OnlyIncludedMembersAreChecked()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using PolyType;
			class Unshaped { public Action? Callback { get; set; } }
			[GenerateShape]
			partial class Model
			{
				[PropertyShape(Ignore = true)]
				public Action? Ignored { get; set; }
				private Action? NonPublic { get; set; }
				public static Action? Static { get; set; }
				public Action? ReadOnly => null;
				[PropertyShape]
				private Action? {|NBJson001:OptIn|} { get; set; }
			}
			""");

	[Test]
	public Task ConstructorBoundReadOnlyMemberIsIncluded()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using PolyType;
			[GenerateShape]
			partial class Model(Action? callback)
			{
				public Action? {|NBJson001:Callback|} { get; } = callback;
			}
			""");

	[Test]
	public Task MutableGetOnlyCollectionsAreIncluded()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using System.Collections.Generic;
			using System.Collections.ObjectModel;
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				public Dictionary<object, int> {|NBJson002:Map|} { get; } = new();
				public List<Action> {|NBJson001:Callbacks|} { get; } = new();
				public ReadOnlyDictionary<object, int> ReadOnly { get; } = new(new Dictionary<object, int>());
			}
			""");

	[Test]
	public Task DeclarativeUnionCasesAreIncluded()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using PolyType;
			[GenerateShape, DerivedTypeShape(typeof(Case))]
			abstract partial class Model;
			class Case : Model { public Action? {|NBJson001:Callback|} { get; set; } }
			""");

	[Test]
	public Task ExplicitConvertersStopTraversalOnTheirEdges()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using System.Collections.Generic;
			using Nerdbank.Json;
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				[JsonConverter(typeof(Custom<Action>))]
				public Action? Callback { get; set; }
				[JsonConverter(typeof(Custom<Dictionary<object, int>>))]
				public Dictionary<object, int>? Map { get; set; }
				public Converted? Child { get; set; }
			}
			[JsonConverter(typeof(Custom<Converted>))]
			class Converted { public Action? Callback { get; set; } }
			class Custom<T> : JsonConverter<T>
			{
				public override T? Read(ref JsonReader reader, SerializationContext context) => throw new NotImplementedException();
				public override void Write(ref JsonWriter writer, in T? value, SerializationContext context) => throw new NotImplementedException();
			}
			""");

	[Test]
	public Task ParameterConverterAndCustomNamesAreRespected()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using Nerdbank.Json;
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				[ConstructorShape]
				public Model([ParameterShape(Name = "handler"), JsonConverter(typeof(Custom))] Action? input) { this.Callback = input; }
				[PropertyShape(Name = "handler")]
				public Action? Callback { get; }
			}
			class Custom : JsonConverter<Action>
			{
				public override Action? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, in Action? value, SerializationContext context) => writer.WriteNull();
			}
			""");

	[Test]
	public Task AConverterOnADictionaryKeyDoesNotMakeItAPropertyName()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System.Collections.Generic;
			using Nerdbank.Json;
			using PolyType;
			[GenerateShape]
			partial class Model { public Dictionary<Key, int> {|NBJson002:Map|} { get; set; } = new(); }
			[JsonConverter(typeof(Custom))]
			class Key { }
			class Custom : JsonConverter<Key>
			{
				public override Key Read(ref JsonReader reader, SerializationContext context) => throw new System.NotImplementedException();
				public override void Write(ref JsonWriter writer, in Key? value, SerializationContext context) => writer.WriteNull();
			}
			""");

	[Test]
	public Task MarshalersUseTheirSurrogateContract()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using PolyType;
			[GenerateShape(Marshaler = typeof(Custom))]
			partial class Model { public Action? Callback { get; set; } }
			class Custom : IMarshaler<Model, string>
			{
				public string? Marshal(Model? value) => null;
				public Model? Unmarshal(string? value) => null;
			}
			[GenerateShape]
			partial class Root { public Nested? Child { get; set; } }
			[TypeShape(Marshaler = typeof(NestedMarshaler))]
			class Nested { public Action? Callback { get; set; } }
			class NestedMarshaler : IMarshaler<Nested, Surrogate>
			{
				public Surrogate? Marshal(Nested? value) => null;
				public Nested? Unmarshal(Surrogate? value) => null;
			}
			class Surrogate { public Action? {|NBJson001:Bad|} { get; set; } }
			""");

	[Test]
	public Task CustomizedAndOpenGenericShapesAreUnknown()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using System.Collections.Generic;
			using PolyType;
			[GenerateShapeFor<Model>(Kind = TypeShapeKind.None)]
			partial class Witness;
			class Model { public Action? Callback { get; set; } }
			[GenerateShape]
			partial class Generic<T> where T : notnull
			{
				public T? Value { get; set; }
				public Dictionary<T, int>? Map { get; set; }
			}
			""");

	[Test]
	public Task DirectReflectionSerializationIsChecked()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using Nerdbank.Json;
			using PolyType;
			using PolyType.ReflectionProvider;
			class Model { public Action? Callback { get; set; } }
			class Consumer
			{
				string M(JsonSerializer serializer, Model model)
					=> serializer.Serialize(model, {|NBJson001:ReflectionTypeShapeProvider.Default.GetTypeShapeOrThrow<Model>()|});
			}
			""");

	[Test]
	public Task RuntimeShapeProvidersAndUnusedReflectionShapesAreUnknown()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using Nerdbank.Json;
			using PolyType;
			using PolyType.ReflectionProvider;
			class Model { public Action? Callback { get; set; } }
			class Consumer
			{
				string M(JsonSerializer serializer, Model model, ITypeShapeProvider provider)
					=> serializer.Serialize(model, provider.GetTypeShapeOrThrow<Model>());
				ITypeShape<Model> Shape() => ReflectionTypeShapeProvider.Default.GetTypeShapeOrThrow<Model>();
			}
			""");

	[Test]
	public async Task GeneratedDeclarationsAreExcluded()
	{
		VerifyCS.Test test = new();
		test.TestState.Sources.Add(("Model.g.cs", /* lang=c#-test */ """
			// <auto-generated/>
			using System;
			using PolyType;
			[GenerateShape]
			partial class Model { public Action? Callback { get; set; } }
			"""));
		await test.RunAsync();
	}
}
