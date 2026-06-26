// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS1591 // Public analyzer types are intentionally undocumented in-source.
#pragma warning disable SA1600 // Public analyzer types are intentionally undocumented in-source.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nerdbank.Json.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class JsonShapeUsageAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor DelegateMembersNotSupported = new(
		id: "NBJson001",
		title: "Delegate types are not supported by Nerdbank.Json",
		messageFormat: "'{0}' uses delegate type '{1}', which Nerdbank.Json cannot serialize",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	internal static readonly DiagnosticDescriptor UnsupportedDictionaryKeyType = new(
		id: "NBJson002",
		title: "Dictionary key type is not supported by Nerdbank.Json",
		messageFormat: "'{0}' uses dictionary key type '{1}', which Nerdbank.Json cannot encode as a JSON property name",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DelegateMembersNotSupported, UnsupportedDictionaryKeyType];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new System.ArgumentNullException(nameof(context));
		}

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
	}

	private static void AnalyzeNamedType(SymbolAnalysisContext context)
	{
	}
}
