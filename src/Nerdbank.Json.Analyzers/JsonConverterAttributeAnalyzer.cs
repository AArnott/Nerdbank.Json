// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nerdbank.Json.Analyzers;

/// <summary>
/// Validates the converter type and activation constructor in custom converter attributes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class JsonConverterAttributeAnalyzer : DiagnosticAnalyzer
{
	/// <summary>The diagnostic ID for incompatible or abstract converters.</summary>
	public const string InvalidConverterTypeDiagnosticId = "NBJson020";

	/// <summary>The diagnostic ID for converters without a public parameterless constructor.</summary>
	public const string ConverterMissingDefaultCtorDiagnosticId = "NBJson021";

	private static readonly DiagnosticDescriptor InvalidConverterType = new(
		InvalidConverterTypeDiagnosticId,
		"Converter type must be compatible",
		"The converter must be a non-abstract JsonConverter<{0}>",
		"Usage",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(InvalidConverterTypeDiagnosticId));

	private static readonly DiagnosticDescriptor ConverterMissingDefaultCtor = new(
		ConverterMissingDefaultCtorDiagnosticId,
		"Converter requires a public parameterless constructor",
		"The converter for '{0}' must have a public parameterless constructor",
		"Usage",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(ConverterMissingDefaultCtorDiagnosticId));

	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [InvalidConverterType, ConverterMissingDefaultCtor];

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
			INamedTypeSymbol? attributeType = context.Compilation.GetTypeByMetadataName(KnownNames.ConverterAttribute);
			INamedTypeSymbol? converterBase = context.Compilation.GetTypeByMetadataName(KnownNames.GenericConverter);
			if (attributeType is null || converterBase is null)
			{
				return;
			}

			context.RegisterSymbolAction(
				context =>
			{
				INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
				Analyze(type, type);
				foreach (ISymbol member in type.GetMembers())
				{
					switch (member)
					{
						case IPropertySymbol property:
							Analyze(property, property.Type);
							break;
						case IFieldSymbol field:
							Analyze(field, field.Type);
							break;
						case IMethodSymbol method:
							foreach (IParameterSymbol parameter in method.Parameters)
							{
								Analyze(parameter, parameter.Type);
							}

							break;
					}
				}

				void Analyze(ISymbol target, ITypeSymbol valueType)
				{
					foreach (AttributeData attribute in target.GetAttributes())
					{
						if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType) ||
							attribute.ConstructorArguments is not [{ Value: INamedTypeSymbol converter }])
						{
							continue;
						}

						if (converter.IsUnboundGenericType && valueType is INamedTypeSymbol { IsUnboundGenericType: false } named &&
							converter.Arity == named.TypeArguments.Length)
						{
							converter = converter.OriginalDefinition.Construct(named.TypeArguments.ToArray());
						}

						INamedTypeSymbol expectedBase = converterBase.Construct(valueType);
						bool compatible = !converter.IsAbstract && !converter.IsUnboundGenericType;
						INamedTypeSymbol? current = converter;
						while (current is not null && !SymbolEqualityComparer.Default.Equals(current, expectedBase))
						{
							current = current.BaseType;
						}

						DiagnosticDescriptor? rule = !compatible || current is null ? InvalidConverterType :
							!converter.InstanceConstructors.Any(c => c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public) ? ConverterMissingDefaultCtor : null;
						if (rule is not null)
						{
							SyntaxNode? syntax = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken);
							Location? location = syntax is AttributeSyntax { ArgumentList.Arguments: [var argument, ..] }
								? argument.GetLocation() : syntax?.GetLocation();
							context.ReportDiagnostic(Diagnostic.Create(rule, location, valueType.ToDisplayString()));
						}
					}
				}
			},
			SymbolKind.NamedType);
		});
	}
}
