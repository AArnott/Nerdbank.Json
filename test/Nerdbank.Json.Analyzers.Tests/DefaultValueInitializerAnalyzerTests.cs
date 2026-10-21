// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1118 // Code-fix examples intentionally use paired multiline source literals.

using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.DefaultValueInitializerAnalyzer, Nerdbank.Json.Analyzers.CodeFixes.DefaultValueInitializerCodeFix>;

public class DefaultValueInitializerAnalyzerTests
{
	[Test]
	public Task InitializersOnShapeMembers()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				public int {|NBJson110:Number|} = 42;
				public string {|NBJson110:Name|} { get; set; } = "Ada";
			}
			""");

	[Test]
	public Task UnshapedAndIgnoredMembers()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using System.ComponentModel;
			using PolyType;
			class Unshaped { public int Number = 42; }
			[GenerateShape]
			partial class Model
			{
				[DefaultValue(42)]
				public int Number = 42;
				[PropertyShape(Ignore = true)]
				public string Name { get; set; } = "Ada";
				public static int StaticNumber = 42;
				public const int Constant = 42;
				private int PrivateNumber { get; set; } = 42;
				public int NotInitialized { get; set; }
				public DateTime Unsupported { get; set; } = DateTime.Now;
			}
			""");

	[Test]
	public Task ExplicitPrivateMemberIsIncluded()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				[PropertyShape]
				private int {|NBJson110:Number|} { get; set; } = 42;
			}
			""");

	[Test]
	public Task ReachableNestedModel()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Root { public Child Child { get; set; } = new(); }
			class Child
			{
				public string {|NBJson110:Name|} { get; set; } = "Ada";
			}
			""");

	[Test]
	public Task InheritedMemberIsReportedOnlyOnceAcrossRoots()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using PolyType;
			class Base { public int {|NBJson110:Number|} { get; set; } = 42; }
			[GenerateShape]
			partial class First : Base { }
			[GenerateShape]
			partial class Second : Base { }
			""");

	[Test]
	public Task CustomizedWitnessIsIgnored()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShapeFor<Model>(Kind = TypeShapeKind.None)]
			partial class Witness;
			class Model { public int Number = 42; }
			""");

	[Test]
	public Task GenericParameterCannotBeDescribedByAnAttribute()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShapeFor<Model<int>>]
			partial class Witness;
			class Model<T> { public T? Value = default; }
			""");

	[Test]
	public Task WitnessesAndClosedGenericModels()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShapeFor(typeof(Model))]
			[GenerateShapeFor<Box<int>>]
			partial class Witness;
			class Model { public int {|NBJson110:Number|} = 42; }
			class Box<T> { public string {|NBJson110:Name|} { get; set; } = "Ada"; }
			""");

	[Test]
	public Task WildcardWitness()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShapeFor("Models.?at*")]
			partial class Witness;
			namespace Models
			{
				class Matched { public int {|NBJson110:Number|} = 42; }
				class Unmatched { public int Number = 42; }
				class MatchedGeneric<T> { public int Number = 42; }
			}
			""");

	[Test]
	public Task NonConstantInitializerStillRequiresMetadataButHasNoFix()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using System;
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				public int {|NBJson110:Number|} = Math.Abs(-42);
			}
			""");

	[Test]
	public Task FixPropertyPreservesComment()
		=> VerifyCS.VerifyCodeFixAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				// A meaningful default.
				public int {|NBJson110:Number|} { get; set; } = 42;
			}
			""",
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				// A meaningful default.
				[global::System.ComponentModel.DefaultValue(42)]
				public int Number { get; set; } = 42;
			}
			""");

	[Test]
	public Task FixFieldWithConvertedConstant()
		=> VerifyCS.VerifyCodeFixAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				public byte {|NBJson110:Number|} = 42;
			}
			""",
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				[global::System.ComponentModel.DefaultValue((byte)42)]
				public byte Number = 42;
			}
			""");

	[Test]
	public Task FixDefaultLiteral()
		=> VerifyCS.VerifyCodeFixAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				public int {|NBJson110:Number|} = default;
			}
			""",
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				[global::System.ComponentModel.DefaultValue((int)default)]
				public int Number = default;
			}
			""");

	[Test]
	public Task FixEnum()
		=> VerifyCS.VerifyCodeFixAsync(
			/* lang=c#-test */ """
			using PolyType;
			enum Choice { One, Two }
			[GenerateShape]
			partial class Model
			{
				public Choice {|NBJson110:Value|} { get; set; } = Choice.Two;
			}
			""",
			/* lang=c#-test */ """
			using PolyType;
			enum Choice { One, Two }
			[GenerateShape]
			partial class Model
			{
				[global::System.ComponentModel.DefaultValue(Choice.Two)]
				public Choice Value { get; set; } = Choice.Two;
			}
			""");

	[Test]
	public Task MultiVariableFieldDoesNotOfferUnsafeFix()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				public int {|NBJson110:First|} = 1, {|NBJson110:Second|} = 2;
			}
			""");

	[Test]
	public Task FixAllIndependentMembers()
		=> VerifyCS.VerifyCodeFixAsync(
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				public int {|NBJson110:Number|} = -42;
				public string {|NBJson110:Name|} { get; set; } = "Ada";
			}
			""",
			/* lang=c#-test */ """
			using PolyType;
			[GenerateShape]
			partial class Model
			{
				[global::System.ComponentModel.DefaultValue(-42)]
				public int Number = -42;
				[global::System.ComponentModel.DefaultValue("Ada")]
				public string Name { get; set; } = "Ada";
			}
			""");
}
