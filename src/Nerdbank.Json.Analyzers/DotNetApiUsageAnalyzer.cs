// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Nerdbank.Json.Analyzers;

/// <summary>
/// Recommends statically resolved shape APIs when the target runtime supports them.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DotNetApiUsageAnalyzer : DiagnosticAnalyzer
{
	/// <summary>The diagnostic ID for a legacy API with a modern .NET alternative.</summary>
	public const string UseDotNetApiDiagnosticId = "NBJson051";

	private static readonly DiagnosticDescriptor UseDotNetApi = new(
		UseDotNetApiDiagnosticId,
		"Prefer the modern .NET API",
		"{0}",
		"Usage",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(UseDotNetApiDiagnosticId));

	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [UseDotNetApi];

	/// <inheritdoc/>
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new ArgumentNullException(nameof(context));
		}

		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterCompilationStartAction(context =>
		{
			INamedTypeSymbol? marker = context.Compilation.GetTypeByMetadataName(KnownNames.PreferDotNetAlternative);
			INamedTypeSymbol? obsolete = context.Compilation.GetTypeByMetadataName("System.ObsoleteAttribute");
			if (marker is null || context.Compilation.GetTypeByMetadataName(KnownNames.RuntimeFeature)?
				.GetMembers(KnownNames.VirtualStaticsInInterfaces).IsEmpty is not false)
			{
				return;
			}

			context.RegisterOperationAction(
				context =>
			{
				ISymbol? symbol = context.Operation switch
				{
					IInvocationOperation invocation => invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod,
					IObjectCreationOperation creation => creation.Constructor,
					IPropertyReferenceOperation property => property.Property,
					_ => null,
				};
				if (symbol is null)
				{
					return;
				}

				if (IsHardObsolete(symbol) || IsHardObsolete(symbol.ContainingType))
				{
					return;
				}

				AttributeData? attribute = FindMarker(symbol) ?? FindMarker(symbol.ContainingType);
				if (attribute?.ConstructorArguments is [{ Value: string advice }])
				{
					context.ReportDiagnostic(Diagnostic.Create(UseDotNetApi, context.Operation.Syntax.GetLocation(), advice));
				}

				AttributeData? FindMarker(ISymbol target)
					=> target.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, marker));

				bool IsHardObsolete(ISymbol target)
					=> target.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, obsolete) &&
						a.ConstructorArguments is [_, { Value: true }]);
			},
				OperationKind.Invocation,
				OperationKind.ObjectCreation,
				OperationKind.PropertyReference);
		});
	}
}
