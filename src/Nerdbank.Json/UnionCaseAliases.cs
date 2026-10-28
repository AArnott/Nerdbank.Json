// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>Resolves and validates aliases shared by union converters and schemas.</summary>
internal static class UnionCaseAliases
{
	/// <summary>Resolves the identifiers for all cases, in shape order.</summary>
	/// <param name="unionShape">The union shape.</param>
	/// <param name="provider">The optional C# union alias provider.</param>
	/// <returns>The resolved case identifiers.</returns>
	internal static DerivedTypeIdentifier[] Resolve(IUnionTypeShape unionShape, IUnionTypeAliasProvider? provider)
	{
		provider = unionShape.UnionKind == UnionTypeShapeKind.CSharpUnion ? provider : null;
		if (provider is UnionCaseMappingCollection mappings)
		{
			mappings.ValidateUnionCases(unionShape);
		}

		DerivedTypeIdentifier[] aliases = new DerivedTypeIdentifier[unionShape.UnionCases.Count];
		Dictionary<DerivedTypeIdentifier, Type> casesByAlias = [];
		for (int i = 0; i < aliases.Length; i++)
		{
			IUnionCaseShape unionCase = unionShape.UnionCases[i];
			DerivedTypeIdentifier alias = provider?.GetAlias(unionShape, unionCase)
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
		}

		return aliases;
	}
}
