// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS1591 // Public analyzer types are intentionally undocumented in-source.
#pragma warning disable SA1600 // Public analyzer types are intentionally undocumented in-source.

using System.Collections.Frozen;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nerdbank.Json.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RefParametersForRefStructsAnalyzer : DiagnosticAnalyzer
{
	public const string UseRefParametersForRefStructsDiagnosticId = "NBJson050";

	public static readonly DiagnosticDescriptor UseRefParametersForRefStructsDiagnostic = new(
		id: UseRefParametersForRefStructsDiagnosticId,
		title: "Pass JsonWriter by ref",
		messageFormat: "'{0}' parameters should be passed by ref",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [UseRefParametersForRefStructsDiagnostic];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new ArgumentNullException(nameof(context));
		}

		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterCompilationStartAction(static compilationStartContext =>
		{
			INamedTypeSymbol? jsonWriter = compilationStartContext.Compilation.GetTypeByMetadataName("Nerdbank.Json.JsonWriter");

			if (jsonWriter is null)
			{
				return;
			}

			FrozenSet<ISymbol> guardedRefStructs = [jsonWriter];

			compilationStartContext.RegisterSymbolAction(
				symbolContext =>
				{
					IMethodSymbol method = (IMethodSymbol)symbolContext.Symbol;
					foreach (IParameterSymbol parameter in method.Parameters)
					{
						if (parameter.RefKind == RefKind.None && guardedRefStructs.Contains(parameter.Type))
						{
							Location? location = (
								(ParameterSyntax?)parameter.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(symbolContext.CancellationToken))?.Type?.GetLocation()
								?? parameter.Locations.FirstOrDefault();
							if (location is not null)
							{
								symbolContext.ReportDiagnostic(Diagnostic.Create(UseRefParametersForRefStructsDiagnostic, location, parameter.Type.Name));
							}
						}
					}
				},
				SymbolKind.Method);
		});
	}
}
