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
/// Guides custom converters away from top-level serializer calls and undocumented schemas.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConverterUsageAnalyzer : DiagnosticAnalyzer
{
	/// <summary>The diagnostic ID for top-level serialization within a converter.</summary>
	public const string CallbackToTopLevelSerializerDiagnosticId = "NBJson030";

	/// <summary>The diagnostic ID for a missing schema customization.</summary>
	public const string OverrideGetJsonSchemaDiagnosticId = "NBJson032";

	private static readonly DiagnosticDescriptor CallbackToTopLevelSerializer = new(
		CallbackToTopLevelSerializerDiagnosticId,
		"Do not call the top-level serializer from a converter",
		"Use SerializationContext to obtain nested converters instead of calling JsonSerializer",
		"Usage",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(CallbackToTopLevelSerializerDiagnosticId));

	private static readonly DiagnosticDescriptor OverrideGetJsonSchema = new(
		OverrideGetJsonSchemaDiagnosticId,
		"Describe the converter's JSON schema",
		"Override GetJsonSchema so the converter's format can be included in generated schemas",
		"Usage",
		DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(OverrideGetJsonSchemaDiagnosticId));

	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [CallbackToTopLevelSerializer, OverrideGetJsonSchema];

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
			INamedTypeSymbol? converterBase = context.Compilation.GetTypeByMetadataName(KnownNames.GenericConverter);
			INamedTypeSymbol? serializer = context.Compilation.GetTypeByMetadataName(KnownNames.Serializer);
			INamedTypeSymbol? schemaBase = context.Compilation.GetTypeByMetadataName(KnownNames.Converter);
			if (converterBase is null || serializer is null || schemaBase is null)
			{
				return;
			}

			context.RegisterSymbolStartAction(
				context =>
			{
				INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
				if (!type.IsOrDerivedFrom(converterBase.ConstructUnboundGenericType()))
				{
					return;
				}

				context.RegisterOperationAction(
					context =>
				{
					ITypeSymbol? calledType = context.Operation switch
					{
						IObjectCreationOperation creation => creation.Type,
						IInvocationOperation invocation => invocation.TargetMethod.ContainingType,
						_ => null,
					};
					if (calledType.IsOrDerivedFrom(serializer))
					{
						context.ReportDiagnostic(Diagnostic.Create(CallbackToTopLevelSerializer, context.Operation.Syntax.GetLocation()));
					}
				},
				OperationKind.ObjectCreation,
				OperationKind.Invocation);

				if (!type.IsAbstract && !type.GetAllMembers().OfType<IMethodSymbol>().Any(method => OverridesSchema(method, schemaBase)))
				{
					context.RegisterSymbolEndAction(context =>
					{
						Location? location = type.Locations.FirstOrDefault(l => l.IsInSource);
						context.ReportDiagnostic(Diagnostic.Create(OverrideGetJsonSchema, location));
					});
				}
			},
			SymbolKind.NamedType);
		});
	}

	private static bool OverridesSchema(IMethodSymbol method, INamedTypeSymbol schemaBase)
	{
		if (method.Name != KnownNames.GetJsonSchema || method.IsAbstract)
		{
			return false;
		}

		for (IMethodSymbol? overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
		{
			if (SymbolEqualityComparer.Default.Equals(overridden.ContainingType, schemaBase))
			{
				return true;
			}
		}

		return false;
	}
}
