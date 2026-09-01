// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS1591 // Public analyzer helper types are intentionally undocumented in-source.
#pragma warning disable SA1600 // Public analyzer helper types are intentionally undocumented in-source.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Nerdbank.Json.Analyzers;

/// <summary>
/// Shared helpers and constants for the Nerdbank.Json analyzers.
/// </summary>
internal static class AnalyzerUtilities
{
	/// <summary>
	/// Builds the published help link URL for a diagnostic's documentation page.
	/// </summary>
	/// <param name="diagnosticId">The diagnostic ID (for example, <c>NBJson033</c>).</param>
	/// <returns>The absolute URL of the diagnostic's documentation page.</returns>
	internal static string GetHelpLink(string diagnosticId)
		=> $"https://aarnott.github.io/Nerdbank.Json/analyzers/{diagnosticId}.html";

	/// <summary>
	/// Determines whether a type is, or derives from, a (possibly unbound generic) base type.
	/// </summary>
	/// <param name="type">The type to test.</param>
	/// <param name="baseTypeOrUnbound">The base type. May be an unbound generic type (for example <c>JsonConverter&lt;&gt;</c>).</param>
	/// <returns><see langword="true"/> if <paramref name="type"/> is or derives from <paramref name="baseTypeOrUnbound"/>.</returns>
	internal static bool IsOrDerivedFrom(this ITypeSymbol? type, INamedTypeSymbol baseTypeOrUnbound)
	{
		for (INamedTypeSymbol? current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
		{
			INamedTypeSymbol comparand = current.IsGenericType && !current.IsUnboundGenericType
				? current.ConstructUnboundGenericType()
				: current;
			if (SymbolEqualityComparer.Default.Equals(comparand, baseTypeOrUnbound))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Enumerates the members declared on a type and all of its base types.
	/// </summary>
	/// <param name="type">The type whose members (including inherited) to enumerate.</param>
	/// <returns>The members.</returns>
	internal static IEnumerable<ISymbol> GetAllMembers(this INamedTypeSymbol type)
	{
		for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
		{
			foreach (ISymbol member in current.GetMembers())
			{
				yield return member;
			}
		}
	}
}
