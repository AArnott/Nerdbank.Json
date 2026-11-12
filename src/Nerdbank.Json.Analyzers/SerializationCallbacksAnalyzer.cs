// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nerdbank.Json.Analyzers;

/// <summary>
/// Reports structs that implement <c>IJsonSerializationCallbacks</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SerializationCallbacksAnalyzer : DiagnosticAnalyzer
{
	/// <summary>The diagnostic ID for a value type that implements serialization callbacks.</summary>
	public const string StructCallbacksDiagnosticId = "NBJson004";

	private static readonly DiagnosticDescriptor StructCallbacks = new(
		StructCallbacksDiagnosticId,
		"Serialization callbacks are supported only on reference types",
		"'{0}' is a value type. IJsonSerializationCallbacks is supported only on reference types because calling the methods boxes a copy and mutations are lost.",
		"Usage",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(StructCallbacksDiagnosticId));

	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [StructCallbacks];

	/// <inheritdoc/>
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new ArgumentNullException(nameof(context));
		}

		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterCompilationStartAction(startContext =>
		{
			INamedTypeSymbol? callbacks = startContext.Compilation.GetTypeByMetadataName(KnownNames.SerializationCallbacks);
			if (callbacks is null)
			{
				return;
			}

			startContext.RegisterSyntaxNodeAction(
				syntaxContext => AnalyzeDeclaration(syntaxContext, callbacks),
				SyntaxKind.StructDeclaration,
				SyntaxKind.RecordDeclaration,
				SyntaxKind.RecordStructDeclaration);
		});
	}

	private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context, INamedTypeSymbol callbacks)
	{
		if (context.Node is not TypeDeclarationSyntax declaration || declaration.BaseList is null)
		{
			return;
		}

		if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol type
			|| type.TypeKind == TypeKind.Enum
			|| !type.IsValueType)
		{
			return;
		}

		foreach (BaseTypeSyntax baseType in declaration.BaseList.Types)
		{
			if (context.SemanticModel.GetTypeInfo(baseType.Type, context.CancellationToken).Type is not INamedTypeSymbol implemented)
			{
				continue;
			}

			if (!SymbolEqualityComparer.Default.Equals(implemented, callbacks)
				&& !implemented.AllInterfaces.Contains(callbacks, SymbolEqualityComparer.Default))
			{
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(StructCallbacks, baseType.Type.GetLocation(), type.Name));
		}
	}
}
