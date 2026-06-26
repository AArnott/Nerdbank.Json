// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS1591 // Public analyzer types are intentionally undocumented in-source.
#pragma warning disable SA1600 // Public analyzer types are intentionally undocumented in-source.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
		INamedTypeSymbol namedType = (INamedTypeSymbol)context.Symbol;

		bool hasGenerateShape = HasAttribute(namedType, "PolyType.GenerateShapeAttribute");
		if (hasGenerateShape)
		{
			foreach (ISymbol member in namedType.GetMembers())
			{
				if (member.IsStatic || member.IsImplicitlyDeclared)
				{
					continue;
				}

				ITypeSymbol? memberType = member switch
				{
					IPropertySymbol property => property.Type,
					IFieldSymbol field => field.Type,
					_ => null,
				};

				if (memberType is null || member.Locations.Length == 0)
				{
					continue;
				}

				AnalyzeType(memberType, member.Name, member.Locations[0], context.ReportDiagnostic);
			}
		}

		foreach (AttributeData attribute in namedType.GetAttributes())
		{
			if (!IsGenerateShapeForAttribute(attribute.AttributeClass))
			{
				continue;
			}

			if (attribute.AttributeClass is not INamedTypeSymbol { TypeArguments.Length: 1 } attributeClass)
			{
				continue;
			}

			ITypeSymbol targetType = attributeClass.TypeArguments[0];
			Location location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation() ?? namedType.Locations.FirstOrDefault() ?? Location.None;
			AnalyzeType(targetType, targetType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), location, context.ReportDiagnostic);
		}
	}

	private static void AnalyzeType(ITypeSymbol type, string displayName, Location location, Action<Diagnostic> reportDiagnostic)
	{
		if (type.TypeKind == TypeKind.Delegate)
		{
			reportDiagnostic(Diagnostic.Create(DelegateMembersNotSupported, location, displayName, type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
			return;
		}

		if (TryGetDictionaryKeyType(type, out ITypeSymbol? keyType) && keyType is not null && !IsSupportedDictionaryKeyType(keyType))
		{
			reportDiagnostic(Diagnostic.Create(UnsupportedDictionaryKeyType, location, displayName, keyType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
		}
	}

	private static bool TryGetDictionaryKeyType(ITypeSymbol type, out ITypeSymbol? keyType)
	{
		if (type is INamedTypeSymbol namedType)
		{
			foreach (INamedTypeSymbol candidate in EnumerateSelfAndInterfaces(namedType))
			{
				if (candidate.TypeArguments.Length == 2 && candidate.OriginalDefinition.SpecialType == SpecialType.None)
				{
					string metadataName = candidate.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
					if (metadataName is "global::System.Collections.Generic.IDictionary<TKey, TValue>" or "global::System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
					{
						keyType = candidate.TypeArguments[0];
						return true;
					}
				}
			}
		}

		keyType = null;
		return false;
	}

	private static IEnumerable<INamedTypeSymbol> EnumerateSelfAndInterfaces(INamedTypeSymbol namedType)
	{
		yield return namedType;
		foreach (INamedTypeSymbol @interface in namedType.AllInterfaces)
		{
			yield return @interface;
		}
	}

	private static bool IsSupportedDictionaryKeyType(ITypeSymbol keyType)
	{
		if (keyType.TypeKind == TypeKind.Enum)
		{
			return true;
		}

		if (keyType.SpecialType is SpecialType.System_String or SpecialType.System_Char or SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal)
		{
			return true;
		}

		return keyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is
			"global::System.Guid" or
			"global::System.DateTime" or
			"global::System.DateTimeOffset" or
			"global::System.TimeSpan" or
			"global::System.Numerics.BigInteger" or
			"global::System.Half" or
			"global::System.Int128" or
			"global::System.UInt128" or
			"global::System.DateOnly" or
			"global::System.TimeOnly" or
			"global::System.Text.Rune";
	}

	private static bool HasAttribute(ISymbol symbol, string metadataName)
		=> symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == $"global::{metadataName}");

	private static bool IsGenerateShapeForAttribute(INamedTypeSymbol? attributeClass)
		=> attributeClass?.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::PolyType.GenerateShapeForAttribute<T>";
}
