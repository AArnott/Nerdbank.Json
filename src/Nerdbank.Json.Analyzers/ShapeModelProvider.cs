// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using PolyType.Roslyn;

namespace Nerdbank.Json.Analyzers;

/// <summary>
/// Discovers generated shape roots and synthesizes their PolyType member contracts.
/// </summary>
internal static class ShapeModelProvider
{
	private static readonly SymbolDisplayFormat QualifiedNameOnlyFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

	/// <summary>Finds explicitly attributed roots, including witness and wildcard targets.</summary>
	/// <param name="compilation">The compilation being analyzed.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The target type, witness or type declaration, and its configuration attribute.</returns>
	internal static IEnumerable<(ITypeSymbol Type, INamedTypeSymbol Declaration, AttributeData Attribute)> GetRoots(Compilation compilation, CancellationToken cancellationToken)
	{
		INamedTypeSymbol? generateShape = compilation.GetTypeByMetadataName("PolyType.GenerateShapeAttribute");
		INamedTypeSymbol? generateShapeFor = compilation.GetTypeByMetadataName("PolyType.GenerateShapeForAttribute");
		INamedTypeSymbol? generateShapeForGeneric = compilation.GetTypeByMetadataName("PolyType.GenerateShapeForAttribute`1");
		INamedTypeSymbol[] declarations = GetDeclaredTypes(compilation.Assembly.GlobalNamespace).ToArray();
		foreach (INamedTypeSymbol type in declarations)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (AttributeData attribute in type.GetAttributes())
			{
				if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, generateShape))
				{
					yield return (type, type, attribute);
				}
				else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, generateShapeFor))
				{
					if (attribute.ConstructorArguments is [{ Value: ITypeSymbol shapedType }])
					{
						yield return (shapedType, type, attribute);
					}
					else if (attribute.ConstructorArguments is [{ Value: string { Length: > 0 } pattern }])
					{
						foreach (INamedTypeSymbol candidate in declarations)
						{
							cancellationToken.ThrowIfCancellationRequested();
							if (!(candidate.IsGenericType && candidate.IsDefinition) &&
								IsMatch(candidate.ToDisplayString(QualifiedNameOnlyFormat), pattern))
							{
								yield return (candidate, type, attribute);
							}
						}
					}
				}
				else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass?.OriginalDefinition, generateShapeForGeneric) &&
					attribute.AttributeClass?.TypeArguments is [{ } argument])
				{
					yield return (argument, type, attribute);
				}
			}
		}
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

	/// <summary>Models the member inclusion policy used by PolyType shape attributes.</summary>
	/// <param name="scope">The accessibility scope.</param>
	/// <param name="knownSymbols">The compilation's PolyType symbol catalog.</param>
	/// <param name="propertyShape">The member customization attribute.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	internal sealed class ShapeSynthesis(
		ISymbol scope,
		KnownSymbols knownSymbols,
		INamedTypeSymbol propertyShape,
		CancellationToken cancellationToken) : TypeDataModelGenerator(scope, knownSymbols, cancellationToken)
	{
		/// <inheritdoc/>
		protected override bool IncludeField(IFieldSymbol field, out string? customName, out int order, out bool includeGetter, out bool includeSetter)
		{
			AttributeData? attribute = this.FindAttribute(field);
			if (attribute is null)
			{
				return base.IncludeField(field, out customName, out order, out includeGetter, out includeSetter);
			}

			customName = GetName(attribute);
			order = 0;
			includeGetter = true;
			includeSetter = !field.IsReadOnly;
			return !IsIgnored(attribute);
		}

		/// <inheritdoc/>
		protected override bool IncludeProperty(IPropertySymbol property, out string? customName, out int order, out bool includeGetter, out bool includeSetter)
		{
			AttributeData? attribute = this.FindAttribute(property);
			if (attribute is null)
			{
				return base.IncludeProperty(property, out customName, out order, out includeGetter, out includeSetter);
			}

			customName = GetName(attribute);
			order = 0;
			includeGetter = property.GetMethod is not null;
			includeSetter = property.SetMethod is not null;
			return !IsIgnored(attribute);
		}

		private static string? GetName(AttributeData attribute)
			=> attribute.NamedArguments.FirstOrDefault(a => a.Key == "Name").Value.Value as string;

		private static bool IsIgnored(AttributeData attribute)
			=> attribute.NamedArguments.Any(a => a.Key == "Ignore" && a.Value.Value is true);

		private AttributeData? FindAttribute(ISymbol member)
			=> member.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, propertyShape));
	}
}
