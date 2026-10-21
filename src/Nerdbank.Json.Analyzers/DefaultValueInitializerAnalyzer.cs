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

	private static readonly SymbolDisplayFormat QualifiedNameOnlyFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

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
			INamedTypeSymbol? generateShapeFor = context.Compilation.GetTypeByMetadataName("PolyType.GenerateShapeForAttribute");
			INamedTypeSymbol? generateShapeForGeneric = context.Compilation.GetTypeByMetadataName("PolyType.GenerateShapeForAttribute`1");
			INamedTypeSymbol? propertyShape = context.Compilation.GetTypeByMetadataName("PolyType.PropertyShapeAttribute");
			if (defaultValue is null || generateShape is null || propertyShape is null ||
				context.Compilation.GetTypeByMetadataName(KnownNames.Serializer) is null)
			{
				return;
			}

			INamedTypeSymbol[] declarations = GetDeclaredTypes(context.Compilation.Assembly.GlobalNamespace).ToArray();
			ShapeSynthesis generator = new(context.Compilation.Assembly, new KnownSymbols(context.Compilation), propertyShape, context.CancellationToken);
			foreach (INamedTypeSymbol type in declarations)
			{
				foreach (AttributeData attribute in type.GetAttributes())
				{
					// Customized shapes can use a different wire model; do not infer their defaults.
					if (!attribute.NamedArguments.IsEmpty)
					{
						continue;
					}

					if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, generateShape))
					{
						generator.IncludeType(type);
					}
					else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, generateShapeFor))
					{
						if (attribute.ConstructorArguments is [{ Value: ITypeSymbol shapedType }])
						{
							generator.IncludeType(shapedType);
						}
						else if (attribute.ConstructorArguments is [{ Value: string { Length: > 0 } pattern }])
						{
							foreach (INamedTypeSymbol candidate in declarations)
							{
								if (!(candidate.IsGenericType && candidate.IsDefinition) &&
									IsMatch(candidate.ToDisplayString(QualifiedNameOnlyFormat), pattern))
								{
									generator.IncludeType(candidate);
								}
							}
						}
					}
					else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass?.OriginalDefinition, generateShapeForGeneric) &&
						attribute.AttributeClass?.TypeArguments is [{ } argument])
					{
						generator.IncludeType(argument);
					}
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

	private static IEnumerable<INamedTypeSymbol> GetDeclaredTypes(INamespaceOrTypeSymbol container)
	{
		foreach (INamedTypeSymbol type in container.GetTypeMembers())
		{
			yield return type;
			foreach (INamedTypeSymbol nested in GetDeclaredTypes(type))
			{
				yield return nested;
			}
		}

		if (container is INamespaceSymbol ns)
		{
			foreach (INamespaceSymbol child in ns.GetNamespaceMembers())
			{
				foreach (INamedTypeSymbol type in GetDeclaredTypes(child))
				{
					yield return type;
				}
			}
		}
	}

	private static bool IsMatch(string value, string pattern)
	{
		int valueIndex = 0, patternIndex = 0, wildcardIndex = -1, wildcardValueIndex = -1;
		while (valueIndex < value.Length)
		{
			if (patternIndex < pattern.Length && (pattern[patternIndex] == '?' || pattern[patternIndex] == value[valueIndex]))
			{
				valueIndex++;
				patternIndex++;
			}
			else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
			{
				wildcardIndex = patternIndex++;
				wildcardValueIndex = valueIndex;
			}
			else if (wildcardIndex >= 0)
			{
				patternIndex = wildcardIndex + 1;
				valueIndex = ++wildcardValueIndex;
			}
			else
			{
				return false;
			}
		}

		while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
		{
			patternIndex++;
		}

		return patternIndex == pattern.Length;
	}

	private static bool IsAttributeCompatibleType(ITypeSymbol type)
		=> type.TypeKind == TypeKind.Enum || type.SpecialType is
			SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or
			SpecialType.System_Char or SpecialType.System_Double or SpecialType.System_Int16 or
			SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
			SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_String;

	private sealed class ShapeSynthesis(
		ISymbol scope,
		KnownSymbols knownSymbols,
		INamedTypeSymbol propertyShape,
		System.Threading.CancellationToken cancellationToken) : TypeDataModelGenerator(scope, knownSymbols, cancellationToken)
	{
		protected override bool IncludeField(IFieldSymbol field, out string? customName, out int order, out bool includeGetter, out bool includeSetter)
		{
			AttributeData? attribute = field.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, propertyShape));
			if (attribute is null)
			{
				return base.IncludeField(field, out customName, out order, out includeGetter, out includeSetter);
			}

			customName = null;
			order = 0;
			includeGetter = true;
			includeSetter = !field.IsReadOnly;
			return !attribute.NamedArguments.Any(a => a.Key == "Ignore" && a.Value.Value is true);
		}

		protected override bool IncludeProperty(IPropertySymbol property, out string? customName, out int order, out bool includeGetter, out bool includeSetter)
		{
			AttributeData? attribute = property.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, propertyShape));
			if (attribute is null)
			{
				return base.IncludeProperty(property, out customName, out order, out includeGetter, out includeSetter);
			}

			customName = null;
			order = 0;
			includeGetter = property.GetMethod is not null;
			includeSetter = property.SetMethod is not null;
			return !attribute.NamedArguments.Any(a => a.Key == "Ignore" && a.Value.Value is true);
		}
	}
}
