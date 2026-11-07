// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1201 // Related resolution result follows its resolver.
#pragma warning disable SA1600 // Internal resolution result members are intentionally undocumented.

namespace Nerdbank.Json;

/// <summary>Resolves and validates aliases shared by union converters and schemas.</summary>
internal static class UnionCaseAliases
{
	/// <summary>Resolves the identifiers for all cases, in shape order.</summary>
	/// <param name="unionShape">The union shape.</param>
	/// <param name="provider">The optional C# union alias provider.</param>
	/// <returns>The resolved case identifiers.</returns>
	internal static DerivedTypeIdentifier[] Resolve(IUnionTypeShape unionShape, IUnionTypeAliasProvider? provider)
		=> ResolveDetailed(unionShape, provider).Aliases;

	/// <summary>Resolves identifiers and whether each one was explicitly supplied.</summary>
	/// <param name="unionShape">The union whose cases are being resolved.</param>
	/// <param name="provider">The optional C# union alias provider.</param>
	/// <returns>The resolved identifiers and their origins.</returns>
	internal static UnionCaseAliasesResolution ResolveDetailed(IUnionTypeShape unionShape, IUnionTypeAliasProvider? provider)
	{
		provider = unionShape.UnionKind == UnionTypeShapeKind.CSharpUnion ? provider : null;
		if (provider is UnionCaseMappingCollection mappings)
		{
			mappings.ValidateUnionCases(unionShape);
		}

		DerivedTypeIdentifier[] aliases = new DerivedTypeIdentifier[unionShape.UnionCases.Count];
		bool[] explicitlySpecified = new bool[aliases.Length];
		Dictionary<DerivedTypeIdentifier, Type> casesByAlias = [];
		for (int i = 0; i < aliases.Length; i++)
		{
			IUnionCaseShape unionCase = unionShape.UnionCases[i];
			DerivedTypeIdentifier? providerAlias = provider?.GetAlias(unionShape, unionCase);
			DerivedTypeIdentifier alias = providerAlias
				?? (unionCase.IsTagSpecified ? new(unionCase.Tag) : new DerivedTypeIdentifier(unionCase.Name));
			if (alias.Type == DerivedTypeIdentifier.AliasType.None)
			{
				throw new ArgumentException($"The alias for case '{unionCase.UnionCaseType.Type}' of union '{unionShape.Type}' must be initialized.");
			}

			if (casesByAlias.TryGetValue(alias, out Type? previousCase))
			{
				string identifier = alias.Type == DerivedTypeIdentifier.AliasType.String
					? alias.StringAlias
					: alias.IntAlias.ToString(System.Globalization.CultureInfo.InvariantCulture);
				throw new ArgumentException($"Duplicate alias '{identifier}' in union '{unionShape.Type}' for cases '{previousCase}' and '{unionCase.UnionCaseType.Type}'.");
			}

			casesByAlias.Add(alias, unionCase.UnionCaseType.Type);
			aliases[i] = alias;
			explicitlySpecified[i] = providerAlias is not null || unionCase.IsTagSpecified;
		}

		return new(aliases, explicitlySpecified);
	}
}

/// <summary>Resolved C# union identifiers and their explicit-mapping state.</summary>
internal readonly record struct UnionCaseAliasesResolution(DerivedTypeIdentifier[] Aliases, bool[] ExplicitlySpecified);
