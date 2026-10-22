// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PolyType.Roslyn;

namespace Nerdbank.Json.Analyzers;

/// <summary>
/// Finds initializers whose values should be described to generated shapes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DefaultValueInitializerAnalyzer : DiagnosticAnalyzer
{
	/// <summary>The diagnostic ID for an initializer without default-value metadata.</summary>
	public const string MissingDefaultValueAttributeDiagnosticId = "NBJson110";

	private static readonly DiagnosticDescriptor MissingDefaultValueAttribute = new(
		MissingDefaultValueAttributeDiagnosticId,
		"Describe the initialized default value",
		"Add DefaultValueAttribute to '{0}' to describe its initialized default value",
		"Usage",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(MissingDefaultValueAttributeDiagnosticId));

	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [MissingDefaultValueAttribute];

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
			INamedTypeSymbol? defaultValue = context.Compilation.GetTypeByMetadataName("System.ComponentModel.DefaultValueAttribute");
			INamedTypeSymbol? generateShape = context.Compilation.GetTypeByMetadataName("PolyType.GenerateShapeAttribute");
			INamedTypeSymbol? propertyShape = context.Compilation.GetTypeByMetadataName("PolyType.PropertyShapeAttribute");
			if (defaultValue is null || generateShape is null || propertyShape is null ||
				context.Compilation.GetTypeByMetadataName(KnownNames.Serializer) is null)
			{
				return;
			}

			ShapeModelProvider.ShapeSynthesis generator = new(context.Compilation.Assembly, new KnownSymbols(context.Compilation), propertyShape, context.CancellationToken);
			foreach ((ITypeSymbol Type, INamedTypeSymbol Declaration, AttributeData Attribute) root in ShapeModelProvider.GetRoots(context.Compilation, context.CancellationToken))
			{
				// Customized shapes can use a different wire model; do not infer their defaults.
				if (root.Attribute.NamedArguments.IsEmpty)
				{
					generator.IncludeType(root.Type);
				}
			}

			Dictionary<INamedTypeSymbol, HashSet<ISymbol>> membersByDeclaration = new(SymbolEqualityComparer.Default);
			foreach (KeyValuePair<ITypeSymbol, TypeDataModel> pair in generator.GeneratedModels)
			{
				if (pair.Value is not ObjectDataModel model)
				{
					continue;
				}

				foreach (PropertyDataModel property in model.Properties)
				{
					ISymbol member = property.PropertySymbol.OriginalDefinition;
					INamedTypeSymbol declaringType = member.ContainingType.OriginalDefinition;
					if (!membersByDeclaration.TryGetValue(declaringType, out HashSet<ISymbol>? members))
					{
						membersByDeclaration.Add(declaringType, members = new(SymbolEqualityComparer.Default));
					}

					members.Add(member);
				}
			}

			context.RegisterSymbolAction(
				context =>
				{
					if (!membersByDeclaration.TryGetValue((INamedTypeSymbol)context.Symbol, out HashSet<ISymbol>? members))
					{
						return;
					}

					foreach (ISymbol member in members)
					{
						ITypeSymbol? memberType = member switch
						{
							IFieldSymbol { IsConst: false } field => field.Type,
							IPropertySymbol { IsIndexer: false } propertySymbol => propertySymbol.Type,
							_ => null,
						};
						if (member.IsStatic || member.IsImplicitlyDeclared || memberType is null || !IsAttributeCompatibleType(memberType) ||
							member.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, defaultValue)))
						{
							continue;
						}

						if (member.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax(context.CancellationToken) is
							VariableDeclaratorSyntax { Initializer: not null } or PropertyDeclarationSyntax { Initializer: not null }))
						{
							context.ReportDiagnostic(Diagnostic.Create(MissingDefaultValueAttribute, member.Locations.FirstOrDefault(), member.Name));
						}
					}
				},
				SymbolKind.NamedType);
		});
	}

	private static bool IsAttributeCompatibleType(ITypeSymbol type)
		=> type.TypeKind == TypeKind.Enum || type.SpecialType is
			SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or
			SpecialType.System_Char or SpecialType.System_Double or SpecialType.System_Int16 or
			SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
			SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_String;
}
