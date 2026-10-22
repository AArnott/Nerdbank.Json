// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using PolyType.Roslyn;

namespace Nerdbank.Json.Analyzers;

/// <summary>
/// Checks generated and directly reflected serialization contracts for unsupported value shapes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class JsonShapeUsageAnalyzer : DiagnosticAnalyzer
{
	/// <summary>The diagnostic for a delegate value in a serialized contract.</summary>
	internal static readonly DiagnosticDescriptor DelegateMembersNotSupported = new(
		id: "NBJson001",
		title: "Delegate types are not supported by Nerdbank.Json",
		messageFormat: "'{0}' uses delegate type '{1}', which Nerdbank.Json cannot serialize",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink("NBJson001"));

	/// <summary>The diagnostic for a key that cannot be represented as a JSON property name.</summary>
	internal static readonly DiagnosticDescriptor UnsupportedDictionaryKeyType = new(
		id: "NBJson002",
		title: "Dictionary key type is not supported by Nerdbank.Json",
		messageFormat: "'{0}' uses dictionary key type '{1}', which Nerdbank.Json cannot encode as a JSON property name",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink("NBJson002"));

	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DelegateMembersNotSupported, UnsupportedDictionaryKeyType];

	/// <inheritdoc/>
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new ArgumentNullException(nameof(context));
		}

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(context =>
		{
			INamedTypeSymbol? serializer = context.Compilation.GetTypeByMetadataName(KnownNames.Serializer);
			INamedTypeSymbol? propertyShape = context.Compilation.GetTypeByMetadataName(KnownNames.PropertyShapeAttribute);
			if (serializer is null || propertyShape is null)
			{
				return;
			}

			ContractAnalysis analysis = new(context.Compilation, propertyShape, context.CancellationToken);
			Dictionary<INamedTypeSymbol, List<Diagnostic>> diagnostics = new(SymbolEqualityComparer.Default);
			HashSet<(SyntaxTree? Tree, int Start, string Id)> reported = new();
			foreach ((ITypeSymbol Type, INamedTypeSymbol Declaration, AttributeData Attribute) root in ShapeModelProvider.GetRoots(context.Compilation, context.CancellationToken))
			{
				analysis.Visit(root.Type, root.Declaration, root.Attribute, (owner, rule, unsupportedType) =>
				{
					Location? location = SymbolEqualityComparer.Default.Equals(owner, root.Declaration)
						? root.Attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
						: owner.Locations.FirstOrDefault(l => l.IsInSource);
					if (location is null || !reported.Add((location.SourceTree, location.SourceSpan.Start, rule.Id)))
					{
						return;
					}

					INamedTypeSymbol declaration = (owner as INamedTypeSymbol ?? owner.ContainingType).OriginalDefinition;
					if (!diagnostics.TryGetValue(declaration, out List<Diagnostic>? list))
					{
						diagnostics.Add(declaration, list = new());
					}

					list.Add(Diagnostic.Create(rule, location, owner.Name, unsupportedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
				});
			}

			context.RegisterSymbolAction(
				context =>
				{
					if (diagnostics.TryGetValue((INamedTypeSymbol)context.Symbol, out List<Diagnostic>? list))
					{
						foreach (Diagnostic diagnostic in list)
						{
							context.ReportDiagnostic(diagnostic);
						}
					}
				},
				SymbolKind.NamedType);

			INamedTypeSymbol? reflectionProvider = context.Compilation.GetTypeByMetadataName(KnownNames.ReflectionShapeProvider);
			INamedTypeSymbol? shape = context.Compilation.GetTypeByMetadataName(KnownNames.GenericTypeShape);
			INamedTypeSymbol? provider = context.Compilation.GetTypeByMetadataName(KnownNames.TypeShapeProvider);
			if (reflectionProvider is null || shape is null || provider is null)
			{
				return;
			}

			context.RegisterOperationAction(
				context =>
				{
					IInvocationOperation invocation = (IInvocationOperation)context.Operation;
					if (!invocation.TargetMethod.ContainingType.IsOrDerivedFrom(serializer))
					{
						return;
					}

					foreach (IArgumentOperation argument in invocation.Arguments)
					{
						ITypeSymbol? target = null;
						if (argument.Parameter?.Type is INamedTypeSymbol parameterType &&
							SymbolEqualityComparer.Default.Equals(parameterType.OriginalDefinition, shape) &&
							Unwrap(argument.Value) is IInvocationOperation shapeCall &&
							SymbolEqualityComparer.Default.Equals(shapeCall.TargetMethod.ContainingAssembly, reflectionProvider.ContainingAssembly) &&
							(IsDefaultProvider(shapeCall.Instance) || shapeCall.Arguments.Any(a => IsDefaultProvider(a.Value))))
						{
							target = parameterType.TypeArguments[0];
						}
						else if (SymbolEqualityComparer.Default.Equals(argument.Parameter?.Type, provider) && IsDefaultProvider(argument.Value))
						{
							target = invocation.TargetMethod.TypeArguments.FirstOrDefault() ??
								invocation.Arguments.Select(a => Unwrap(a.Value)).OfType<ITypeOfOperation>().FirstOrDefault()?.TypeOperand;
						}

						if (target is not null)
						{
							ContractAnalysis reflectionAnalysis = new(context.Compilation, propertyShape, context.CancellationToken);
							HashSet<string> reflectedRules = new();
							reflectionAnalysis.Visit(target, invocation.TargetMethod, configuration: null, (owner, rule, unsupportedType) =>
							{
								if (reflectedRules.Add(rule.Id))
								{
									context.ReportDiagnostic(Diagnostic.Create(rule, argument.Syntax.GetLocation(), owner.Name, unsupportedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
								}
							});
						}
					}

					bool IsDefaultProvider(IOperation? operation)
						=> operation is not null && Unwrap(operation) is IPropertyReferenceOperation property &&
							property.Property.Name == KnownNames.DefaultProvider &&
							SymbolEqualityComparer.Default.Equals(property.Property.ContainingType, reflectionProvider);
				},
				OperationKind.Invocation);
		});
	}

	private static IOperation Unwrap(IOperation operation)
		=> operation is IConversionOperation conversion ? Unwrap(conversion.Operand) : operation;

	private sealed class ContractAnalysis
	{
		private readonly Compilation compilation;
		private readonly CancellationToken cancellationToken;
		private readonly ShapeModelProvider.ShapeSynthesis generator;
		private readonly INamedTypeSymbol? converterAttribute;
		private readonly INamedTypeSymbol? typeShapeAttribute;
		private readonly INamedTypeSymbol? generateShapeAttribute;
		private readonly INamedTypeSymbol? constructorShapeAttribute;
		private readonly INamedTypeSymbol? parameterShapeAttribute;
		private readonly INamedTypeSymbol? derivedTypeShapeAttribute;
		private readonly INamedTypeSymbol? marshalerInterface;
		private readonly ImmutableHashSet<ITypeSymbol> supportedKeys;

		/// <summary>Initializes a new instance of the <see cref="ContractAnalysis"/> class.</summary>
		/// <param name="compilation">The compilation being analyzed.</param>
		/// <param name="propertyShape">The member customization attribute.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		internal ContractAnalysis(Compilation compilation, INamedTypeSymbol propertyShape, CancellationToken cancellationToken)
		{
			this.compilation = compilation;
			this.cancellationToken = cancellationToken;
			this.generator = new(compilation.Assembly, new KnownSymbols(compilation), propertyShape, cancellationToken);
			this.converterAttribute = compilation.GetTypeByMetadataName(KnownNames.ConverterAttribute);
			this.typeShapeAttribute = compilation.GetTypeByMetadataName(KnownNames.TypeShapeAttribute);
			this.generateShapeAttribute = compilation.GetTypeByMetadataName("PolyType.GenerateShapeAttribute");
			this.constructorShapeAttribute = compilation.GetTypeByMetadataName(KnownNames.ConstructorShapeAttribute);
			this.parameterShapeAttribute = compilation.GetTypeByMetadataName(KnownNames.ParameterShapeAttribute);
			this.derivedTypeShapeAttribute = compilation.GetTypeByMetadataName(KnownNames.DerivedTypeShapeAttribute);
			this.marshalerInterface = compilation.GetTypeByMetadataName(KnownNames.Marshaler);
			this.supportedKeys = KnownNames.DictionaryKeyTypes
				.Concat(this.IsModernLibrary() ? KnownNames.ModernDictionaryKeyTypes : [])
				.Select(compilation.GetTypeByMetadataName)
				.OfType<ITypeSymbol>()
				.ToImmutableHashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
		}

		/// <summary>Visits one serialization root and its reachable contracts.</summary>
		/// <param name="type">The root type.</param>
		/// <param name="owner">The root's source owner.</param>
		/// <param name="configuration">The root's shape configuration.</param>
		/// <param name="report">The callback for unsupported contracts.</param>
		internal void Visit(ITypeSymbol type, ISymbol owner, AttributeData? configuration, Action<ISymbol, DiagnosticDescriptor, ITypeSymbol> report)
			=> this.Visit(type, owner, configuration, report, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

		private static bool IsMutableCollection(ITypeSymbol type)
			=> type is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } named &&
				named.InstanceConstructors.Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0) &&
				named.AllInterfaces.Any(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T);

		private static bool HasTypeParameters(ITypeSymbol type)
			=> type.TypeKind == TypeKind.TypeParameter ||
				(type is INamedTypeSymbol named && named.TypeArguments.Any(HasTypeParameters)) ||
				(type is IArrayTypeSymbol array && HasTypeParameters(array.ElementType));

		private void Visit(ITypeSymbol type, ISymbol owner, AttributeData? configuration, Action<ISymbol, DiagnosticDescriptor, ITypeSymbol> report, HashSet<ITypeSymbol> path)
		{
			this.cancellationToken.ThrowIfCancellationRequested();
			if (type.TypeKind is TypeKind.TypeParameter or TypeKind.Error || this.HasConverter(type) || !path.Add(type))
			{
				return;
			}

			try
			{
				if (configuration is null || !configuration.NamedArguments.Any(a => a.Key is "Kind" or "Marshaler"))
				{
					configuration = type.GetAttributes().FirstOrDefault(a =>
						(SymbolEqualityComparer.Default.Equals(a.AttributeClass, this.typeShapeAttribute) ||
							SymbolEqualityComparer.Default.Equals(a.AttributeClass, this.generateShapeAttribute)) &&
						a.NamedArguments.Any(argument => argument.Key is "Kind" or "Marshaler"));
				}

				if (configuration is not null)
				{
					if (configuration.NamedArguments.FirstOrDefault(a => a.Key == "Marshaler").Value.Value is INamedTypeSymbol marshaler)
					{
						ITypeSymbol? surrogate = marshaler.AllInterfaces.FirstOrDefault(i =>
							SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, this.marshalerInterface) &&
							SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], type))?.TypeArguments[1];
						if (surrogate is not null)
						{
							this.Visit(surrogate, owner, configuration: null, report, path);
						}

						return;
					}

					if (configuration.NamedArguments.Any(a => a.Key == "Kind"))
					{
						return;
					}
				}

				if (type.TypeKind == TypeKind.Delegate || type.SpecialType is SpecialType.System_Delegate or SpecialType.System_MulticastDelegate)
				{
					report(owner, DelegateMembersNotSupported, type);
					return;
				}

				this.generator.IncludeType(type);
				if (!this.generator.GeneratedModels.TryGetValue(type, out TypeDataModel? model))
				{
					return;
				}

				switch (model)
				{
					case DictionaryDataModel dictionary:
						if (!HasTypeParameters(dictionary.KeyType) && dictionary.KeyType.TypeKind is not (TypeKind.Enum or TypeKind.Error) &&
							!this.supportedKeys.Contains(dictionary.KeyType))
						{
							report(owner, UnsupportedDictionaryKeyType, dictionary.KeyType);
						}

						this.Visit(dictionary.ValueType, owner, configuration: null, report, path);
						break;
					case EnumerableDataModel enumerable:
						this.Visit(enumerable.ElementType, owner, configuration: null, report, path);
						break;
					case OptionalDataModel optional:
						this.Visit(optional.ElementType, owner, configuration: null, report, path);
						break;
					case ObjectDataModel objectModel:
						IMethodSymbol? constructor = this.GetConstructor(objectModel);
						foreach (PropertyDataModel property in objectModel.Properties)
						{
							ISymbol member = property.PropertySymbol;
							IParameterSymbol? parameter = constructor?.Parameters.FirstOrDefault(p =>
								SymbolEqualityComparer.Default.Equals(p.Type, property.PropertyType) &&
								StringComparer.OrdinalIgnoreCase.Equals(this.ParameterName(p), property.LogicalName ?? member.Name));
							if ((!property.IncludeGetter && !property.IncludeSetter) || this.HasConverter(member) ||
								(parameter is not null && this.HasConverter(parameter)) ||
								(!property.IncludeSetter && parameter is null && !IsMutableCollection(property.PropertyType)))
							{
								continue;
							}

							this.Visit(property.PropertyType, member.Locations.Any(l => l.IsInSource) ? member : owner, configuration: null, report, path);
						}

						break;
					case TupleDataModel tuple:
						foreach (PropertyDataModel element in tuple.Elements)
						{
							this.Visit(element.PropertyType, owner, configuration: null, report, path);
						}

						break;
				}

				foreach (AttributeData attribute in type.GetAttributes())
				{
					if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, this.derivedTypeShapeAttribute) &&
						attribute.ConstructorArguments.FirstOrDefault().Value is ITypeSymbol derivedType)
					{
						this.Visit(derivedType, owner, configuration: null, report, path);
					}
				}
			}
			finally
			{
				path.Remove(type);
			}
		}

		private bool HasConverter(ISymbol symbol)
			=> this.converterAttribute is not null && symbol.GetAttributes().Any(a => a.AttributeClass.IsOrDerivedFrom(this.converterAttribute));

		private string ParameterName(IParameterSymbol parameter)
			=> parameter.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, this.parameterShapeAttribute))?
				.NamedArguments.FirstOrDefault(a => a.Key == "Name").Value.Value as string ?? parameter.Name;

		private IMethodSymbol? GetConstructor(ObjectDataModel model)
		{
			IMethodSymbol[] constructors = model.Type.GetMembers().OfType<IMethodSymbol>()
				.Where(m => m.MethodKind == MethodKind.Constructor && !m.IsStatic).ToArray();
			IMethodSymbol[] attributed = constructors.Where(c => c.GetAttributes().Any(a =>
				SymbolEqualityComparer.Default.Equals(a.AttributeClass, this.constructorShapeAttribute))).ToArray();
			if (attributed.Length > 0)
			{
				return attributed.Length == 1 ? attributed[0] : null;
			}

			IMethodSymbol[] accessible = constructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).ToArray();
			return accessible.FirstOrDefault(c => c.Parameters.Length == 0) ??
				(accessible.Length == 1 ? accessible[0] : null);
		}

		private bool IsModernLibrary()
		{
			INamedTypeSymbol? targetFramework = this.compilation.GetTypeByMetadataName("System.Runtime.Versioning.TargetFrameworkAttribute");
			string? framework = this.compilation.GetTypeByMetadataName(KnownNames.Serializer)?.ContainingAssembly.GetAttributes()
				.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, targetFramework))?.ConstructorArguments.FirstOrDefault().Value as string;
			const string prefix = ".NETCoreApp,Version=v";
			return framework?.StartsWith(prefix, StringComparison.Ordinal) is true &&
				Version.TryParse(framework.Substring(prefix.Length), out Version? version) && version.Major >= 8;
		}
	}
}
