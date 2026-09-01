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
public class JsonCollectionComparerAnalyzer : DiagnosticAnalyzer
{
	public const string InvalidComparerDiagnosticId = "NBJson003";

	public static readonly DiagnosticDescriptor InvalidComparerDiagnostic = new(
		id: InvalidComparerDiagnosticId,
		title: "JsonCollectionComparer attribute is invalid",
		messageFormat: "{0}",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: "https://aarnott.github.io/Nerdbank.Json/docs/collection-comparers.html");

	private const string AttributeMetadataName = "Nerdbank.Json.JsonCollectionComparerAttribute";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [InvalidComparerDiagnostic];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new System.ArgumentNullException(nameof(context));
		}

		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterCompilationStartAction(static compilationStartContext =>
		{
			INamedTypeSymbol? attributeSymbol = compilationStartContext.Compilation.GetTypeByMetadataName(AttributeMetadataName);
			if (attributeSymbol is null)
			{
				return;
			}

			WellKnownSymbols symbols = new(compilationStartContext.Compilation, attributeSymbol);

			compilationStartContext.RegisterSymbolAction(c => AnalyzeMember(c, ((IPropertySymbol)c.Symbol).Type, c.Symbol, symbols), SymbolKind.Property);
			compilationStartContext.RegisterSymbolAction(c => AnalyzeMember(c, ((IFieldSymbol)c.Symbol).Type, c.Symbol, symbols), SymbolKind.Field);
			compilationStartContext.RegisterSymbolAction(
				c =>
				{
					var method = (IMethodSymbol)c.Symbol;
					foreach (IParameterSymbol parameter in method.Parameters)
					{
						AnalyzeMember(c, parameter.Type, parameter, symbols);
					}
				},
				SymbolKind.Method);
		});
	}

	private static void AnalyzeMember(SymbolAnalysisContext context, ITypeSymbol memberType, ISymbol member, WellKnownSymbols symbols)
	{
		AttributeData? attribute = member.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.Attribute));
		if (attribute is null)
		{
			return;
		}

		Location location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
			?? member.Locations.FirstOrDefault()
			?? Location.None;

		if (attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol comparerType)
		{
			// The compiler already reports malformed attribute arguments.
			return;
		}

		if (!TryGetComparedType(memberType, symbols, out ITypeSymbol? comparedType))
		{
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidComparerDiagnostic,
				location,
				$"'{member.Name}' is not a dictionary or set member, so JsonCollectionComparer cannot be applied to it."));
			return;
		}

		if (!HasPublicParameterlessConstructor(comparerType))
		{
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidComparerDiagnostic,
				location,
				$"Comparer type '{comparerType.Name}' must declare a public parameterless constructor."));
			return;
		}

		if (!ImplementsComparerFor(comparerType, comparedType, symbols))
		{
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidComparerDiagnostic,
				location,
				$"Comparer type '{comparerType.Name}' must implement IEqualityComparer<{comparedType.Name}> or IComparer<{comparedType.Name}>."));
		}
	}

	private static bool TryGetComparedType(ITypeSymbol memberType, WellKnownSymbols symbols, out ITypeSymbol comparedType)
	{
		comparedType = null!;
		if (memberType.SpecialType == SpecialType.System_String || memberType.TypeKind == TypeKind.Array)
		{
			return false;
		}

		// Dictionaries compare their key type.
		foreach (INamedTypeSymbol iface in EnumerateSelfAndInterfaces(memberType))
		{
			if ((symbols.Dictionary is not null && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, symbols.Dictionary)) ||
				(symbols.ReadOnlyDictionary is not null && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, symbols.ReadOnlyDictionary)))
			{
				comparedType = iface.TypeArguments[0];
				return true;
			}
		}

		// Sets compare their element type.
		bool isSet = false;
		ITypeSymbol? elementType = null;
		foreach (INamedTypeSymbol iface in EnumerateSelfAndInterfaces(memberType))
		{
			if (symbols.Set is not null && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, symbols.Set))
			{
				isSet = true;
				elementType = iface.TypeArguments[0];
			}
			else if (symbols.Enumerable is not null && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, symbols.Enumerable))
			{
				elementType ??= iface.TypeArguments[0];
			}
		}

		if (isSet && elementType is not null)
		{
			comparedType = elementType;
			return true;
		}

		return false;
	}

	private static IEnumerable<INamedTypeSymbol> EnumerateSelfAndInterfaces(ITypeSymbol type)
	{
		if (type is INamedTypeSymbol named)
		{
			yield return named;
		}

		foreach (INamedTypeSymbol iface in type.AllInterfaces)
		{
			yield return iface;
		}
	}

	private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
		=> type.TypeKind == TypeKind.Class
			&& !type.IsAbstract
			&& type.InstanceConstructors.Any(c => c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public);

	private static bool ImplementsComparerFor(INamedTypeSymbol comparerType, ITypeSymbol comparedType, WellKnownSymbols symbols)
	{
		foreach (INamedTypeSymbol iface in comparerType.AllInterfaces)
		{
			if ((symbols.EqualityComparer is not null && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, symbols.EqualityComparer)) ||
				(symbols.Comparer is not null && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, symbols.Comparer)))
			{
				if (SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], comparedType))
				{
					return true;
				}
			}
		}

		return false;
	}

	private sealed class WellKnownSymbols
	{
		internal WellKnownSymbols(Compilation compilation, INamedTypeSymbol attribute)
		{
			this.Attribute = attribute;
			this.Dictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2");
			this.ReadOnlyDictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyDictionary`2");
			this.Set = compilation.GetTypeByMetadataName("System.Collections.Generic.ISet`1");
			this.Enumerable = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
			this.EqualityComparer = compilation.GetTypeByMetadataName("System.Collections.Generic.IEqualityComparer`1");
			this.Comparer = compilation.GetTypeByMetadataName("System.Collections.Generic.IComparer`1");
		}

		internal INamedTypeSymbol Attribute { get; }

		internal INamedTypeSymbol? Dictionary { get; }

		internal INamedTypeSymbol? ReadOnlyDictionary { get; }

		internal INamedTypeSymbol? Set { get; }

		internal INamedTypeSymbol? Enumerable { get; }

		internal INamedTypeSymbol? EqualityComparer { get; }

		internal INamedTypeSymbol? Comparer { get; }
	}
}
