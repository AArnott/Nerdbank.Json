// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.DotNetApiUsageAnalyzer>;

public class DotNetApiUsageAnalyzerTests
{
	[Test]
	[Arguments("mapping.AddSourceGenerated<Model>(1)", "Use Add<TDerived> instead, with a type that implements IShapeable<TDerived>.")]
	[Arguments("mapping.AddSourceGenerated<Model, Model>(1)", "Use Add<TDerived, TProvider> instead, with a witness that implements IShapeable<TDerived>.")]
	public Task LegacyMappingApi([StringSyntax("c#-test")] string invocation, string advice)
		=> VerifyCS.VerifyAnalyzerAsync(
			$$"""
			using Nerdbank.Json;
			using PolyType;
			class Model : IShapeable<Model>
			{
				public static ITypeShape<Model> GetTypeShape() => throw new System.NotImplementedException();
				void M(DerivedShapeMapping<object> mapping) => {|#0:{{invocation}}|};
			}
			""",
			VerifyCS.Diagnostic().WithLocation(0).WithArguments(advice));

	[Test]
	public Task ModernAndExplicitShapeApisAreUsable()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			using PolyType;
			class Model : IShapeable<Model>
			{
				public static ITypeShape<Model> GetTypeShape() => throw new System.NotImplementedException();
				void M(DerivedShapeMapping<object> mapping, ITypeShape<Model> shape, ITypeShapeProvider provider)
				{
					mapping.Add<Model>(1);
					mapping.Add<Model, Model>(2);
					mapping.Add(3, shape);
					mapping.Add<Model>(4, provider);
				}
			}
			""");

	[Test]
	public Task UnrelatedMethodNamesAreIgnored()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			class Mapping
			{
				public void AddSourceGenerated<T>(int alias) { }
				void M() => this.AddSourceGenerated<int>(1);
			}
			""");

	[Test]
	public Task HardObsoleteSerializerApiDoesNotGetADuplicateWarning()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;
			class Model
			{
				void M(JsonSerializer serializer) => {|#0:JsonSerializerExtensions.Serialize<Model>(serializer, this)|};
			}
			""",
			Microsoft.CodeAnalysis.Testing.DiagnosticResult.CompilerError("CS0619").WithLocation(0));

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task LegacyTargetDoesNotReport(bool framework)
	{
		VerifyCS.Test test = new()
		{
			ReferenceAssemblies = framework
				? Microsoft.CodeAnalysis.Testing.ReferenceAssemblies.NetFramework.Net472.Default
				: Microsoft.CodeAnalysis.Testing.ReferenceAssemblies.NetStandard.NetStandard20,
			TestCode = /* lang=c#-test */ """
				using System;
				namespace Nerdbank.Json
				{
					internal class PreferDotNetAlternativeApiAttribute : Attribute
					{
						public PreferDotNetAlternativeApiAttribute(string advice) { }
					}
					class Mapping
					{
						[PreferDotNetAlternativeApi("Use the modern API.")]
						public void AddSourceGenerated() { }
						void M() => AddSourceGenerated();
					}
				}
				""",
		};
		test.TestState.AdditionalReferences.Clear();
		await test.RunAsync();
	}
}
