// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1649 // Helper verifier file name intentionally differs from type name.
#pragma warning disable SA1402 // Keep the analyzer-only and code-fix verifier arities together.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Text;

namespace Nerdbank.Json.Analyzers.Tests.Verifier;

internal class AnalyzerVerifier<TAnalyzer> : AnalyzerVerifier<TAnalyzer, EmptyCodeFixProvider>
	where TAnalyzer : DiagnosticAnalyzer, new()
{
}

internal class AnalyzerVerifier<TAnalyzer, TCodeFix>
	where TAnalyzer : DiagnosticAnalyzer, new()
	where TCodeFix : CodeFixProvider, new()
{
	public static DiagnosticResult Diagnostic()
		=> CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic();

	public static DiagnosticResult Diagnostic(string diagnosticId)
		=> CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(diagnosticId);

	public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
		=> new(descriptor);

	public static Task VerifyAnalyzerAsync([StringSyntax("c#-test")] string source, params DiagnosticResult[] expected)
	{
		Test test = new()
		{
			TestCode = source,
			TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
		};

		test.ExpectedDiagnostics.AddRange(expected);
		return test.RunAsync();
	}

	public static Task VerifyCodeFixAsync([StringSyntax("c#-test")] string source, [StringSyntax("c#-test")] string fixedSource)
		=> new Test
		{
			TestCode = source,
			FixedCode = fixedSource,
			TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
		}.RunAsync();

	internal class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
	{
		internal Test()
		{
			this.ReferenceAssemblies = new ReferenceAssemblies(
				"net10.0",
				new PackageIdentity("Microsoft.NETCore.App.Ref", "10.0.0"),
				Path.Combine("ref", "net10.0"));
			this.CompilerDiagnostics = CompilerDiagnostics.Warnings;
			this.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
				root = true

				[*.cs]
				indent_style = tab
				indent_size = 4
				tab_width = 4
				"""));
			this.TestState.AdditionalReferences.AddRange(ReferencesHelper.GetReferences());
			this.TestState.AdditionalFilesFactories.Add(() =>
			{
				const string additionalFilePrefix = "AdditionalFiles.";
				return from resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames()
					   where resourceName.StartsWith(additionalFilePrefix, StringComparison.Ordinal)
					   let content = ReadManifestResource(Assembly.GetExecutingAssembly(), resourceName)
					   select (filename: resourceName[additionalFilePrefix.Length..], SourceText.From(content));
			});
		}

		protected override ParseOptions CreateParseOptions()
			=> ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion.CSharp13);

		protected override CompilationOptions CreateCompilationOptions()
		{
			var compilationOptions = (CSharpCompilationOptions)base.CreateCompilationOptions();
			return compilationOptions
				.WithNullableContextOptions(NullableContextOptions.Enable)
				.WithWarningLevel(99)
				.WithSpecificDiagnosticOptions(compilationOptions.SpecificDiagnosticOptions.SetItem("CS1591", ReportDiagnostic.Suppress));
		}

		private static string ReadManifestResource(Assembly assembly, string resourceName)
		{
			using StreamReader reader = new(assembly.GetManifestResourceStream(resourceName) ?? throw new ArgumentException("No such resource stream", nameof(resourceName)));
			return reader.ReadToEnd();
		}
	}
}
