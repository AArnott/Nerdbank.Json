// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.UnionDiscriminatorAnalyzer, Nerdbank.Json.Analyzers.CodeFixes.UnionDiscriminatorCodeFix>;

public class UnionDiscriminatorAnalyzerTests
{
	[Test]
	public Task MatchingConstantIsAccepted()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Model
			{
				[UnionDiscriminator("create")]
				public string Kind => "create";
			}
			""");

	[Test]
	public Task MismatchedConstantIsAWarningWithAFix()
		=> VerifyCS.VerifyCodeFixAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Model
			{
				[UnionDiscriminator("wrong")]
				public string {|NBJson120:Kind|} => "create";
			}
			""",
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Model
			{
				[UnionDiscriminator("create")]
				public string Kind => "create";
			}
			""");

	[Test]
	public Task ComputedGetterIsAWarningWithoutAFix()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Model
			{
				public string? Uri { get; set; }
				[UnionDiscriminator("create")]
				public string {|NBJson120:Kind|} => this.Uri ?? "create";
			}
			""");

	[Test]
	public Task ConstFieldIsAnError()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Model
			{
				[UnionDiscriminator("create")]
				public const string {|NBJson121:Kind|} = "create";
			}
			""");

	[Test]
	public Task SetterIsAnError()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Model
			{
				[UnionDiscriminator("create")]
				public string? {|NBJson121:Kind|} { get; set; }
			}
			""");

	[Test]
	public Task TwoDiscriminatorsAreAnError()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class {|NBJson121:Model|}
			{
				[UnionDiscriminator("create")]
				public string Kind => "create";
				[UnionDiscriminator("delete")]
				public string Other => "delete";
			}
			""");

	[Test]
	public Task UnsupportedTypeIsAnError()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Model
			{
				[UnionDiscriminator(1.5)]
				public double {|NBJson122:Kind|} => 1.5;
			}
			""");

	[Test]
	public Task DerivedCasesMustShareANameAndValue()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			using PolyType;
			[DerivedTypeShape(typeof(Create))]
			[DerivedTypeShape(typeof(Delete))]
			class {|NBJson123:Change|}
			{
			}
			class Create
			{
				[UnionDiscriminator("create")]
				public string Kind => "create";
			}
			class Delete
			{
				[UnionDiscriminator("delete")]
				public string Op => "delete";
			}
			""");

	[Test]
	public Task RepeatedDiscriminatorValueIsAnError()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			using PolyType;
			[DerivedTypeShape(typeof(Create))]
			[DerivedTypeShape(typeof(Other))]
			class {|NBJson123:Change|}
			{
			}
			class Create
			{
				[UnionDiscriminator("create")]
				public string Kind => "create";
			}
			class Other
			{
				[UnionDiscriminator("create")]
				public string Kind => "create";
			}
			""");

	[Test]
	public Task CaseWithoutDiscriminatorIsAllowed()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			using PolyType;
			[DerivedTypeShape(typeof(Create))]
			[DerivedTypeShape(typeof(Edit))]
			class Change
			{
			}
			class Create
			{
				[UnionDiscriminator("create")]
				public string Kind => "create";
			}
			class Edit
			{
				public string? Uri { get; set; }
			}
			""");

	[Test]
	public Task KeyIsAWarningOnlyWhenTheTypeUsesIt()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Plain
			{
				[UnionDiscriminator("create")]
				public string Kind => "create";
				public string? Uri { get; set; }
			}
			class Keyed
			{
				[UnionDiscriminator("create")]
				public string {|NBJson124:Kind|} => "create";
				[Nerdbank.MessagePack.Key(1)]
				public string? Uri { get; set; }
			}
			class KeyedAtZero
			{
				[Nerdbank.MessagePack.Key(0)]
				[UnionDiscriminator("create")]
				public string Kind => "create";
				[Nerdbank.MessagePack.Key(1)]
				public string? Uri { get; set; }
			}
			namespace Nerdbank.MessagePack
			{
				[System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
				public sealed class KeyAttribute : System.Attribute
				{
					public KeyAttribute(int index) { }
				}
			}
			""");

	[Test]
	public Task PrivateFieldDoesNotCollideWithATypeLevelName()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			[UnionDiscriminator("kind", "create")]
			class Model
			{
				private string kind = "hidden";
				public string? Uri { get; set; }
				public string Hidden => this.kind;
			}
			""");

	[Test]
	public Task IncludedPrivatePropertyCollidesWithATypeLevelName()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			using PolyType;
			[UnionDiscriminator("kind", "create")]
			class {|NBJson121:Model|}
			{
				[PropertyShape]
				private string kind => "hidden";
			}
			""");

	[Test]
	public Task NullInferredConstantIsAnError()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Model
			{
				[UnionDiscriminator]
				public string? {|NBJson122:Kind|} => null;
			}
			""");

	[Test]
	public Task UnselectedConstructorParameterIsIgnored()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Model
			{
				public Model() { }
				public Model(string kind) { }
				[UnionDiscriminator("create")]
				public string Kind => "create";
			}
			""");

	[Test]
	public Task SelectedConstructorParameterIsAnError()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Model
			{
				public Model(string kind) { }
				[UnionDiscriminator("create")]
				public string {|NBJson121:Kind|} => "create";
			}
			""");

	[Test]
	public async Task PartialConstantIsReadFromItsOwnDeclaration()
	{
		VerifyCS.Test test = new();
		test.TestState.Sources.Add(("Model.Properties.cs", /* lang=c#-test */ """
			using Nerdbank.Json;
			partial class Model
			{
				public string? Uri { get; set; }
			}
			"""));
		test.TestState.Sources.Add(("Model.Discriminator.cs", /* lang=c#-test */ """
			using Nerdbank.Json;
			partial class Model
			{
				[UnionDiscriminator("create")]
				public string Kind => "create";
			}
			"""));
		await test.RunAsync();
	}
}
