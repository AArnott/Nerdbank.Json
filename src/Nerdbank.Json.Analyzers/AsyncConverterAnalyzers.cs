// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS1591 // Public analyzer types are intentionally undocumented in-source.
#pragma warning disable SA1600 // Public analyzer types are intentionally undocumented in-source.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Nerdbank.Json.Analyzers;

/// <summary>
/// Diagnoses unsafe patterns in custom asynchronous <c>JsonConverter&lt;T&gt;</c> implementations that use the
/// <c>JsonAsyncReader</c>/<c>JsonAsyncWriter</c> rental APIs.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AsyncConverterAnalyzers : DiagnosticAnalyzer
{
	public const string AsyncConverterShouldReturnWriterDiagnosticId = "NBJson033";
	public const string AsyncConverterShouldNotReuseWriterDiagnosticId = "NBJson034";
	public const string AsyncConverterShouldReturnReaderDiagnosticId = "NBJson035";
	public const string AsyncConverterShouldNotReuseReaderDiagnosticId = "NBJson036";
	public const string AsyncConverterShouldOverridePreferAsyncSerializationDiagnosticId = "NBJson037";

	public static readonly DiagnosticDescriptor AsyncConverterShouldReturnWriterDescriptor = new(
		AsyncConverterShouldReturnWriterDiagnosticId,
		title: "Return the JsonWriter before awaiting or returning",
		messageFormat: "Return the rented JsonWriter to the JsonAsyncWriter (via ReturnWriter) before awaiting, calling another method on the JsonAsyncWriter, or leaving the method",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(AsyncConverterShouldReturnWriterDiagnosticId));

	public static readonly DiagnosticDescriptor AsyncConverterShouldNotReuseWriterDescriptor = new(
		AsyncConverterShouldNotReuseWriterDiagnosticId,
		title: "Do not reuse a JsonWriter after returning it",
		messageFormat: "This JsonWriter was already returned to the JsonAsyncWriter and must not be used again; rent a new one with CreateWriter",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(AsyncConverterShouldNotReuseWriterDiagnosticId));

	public static readonly DiagnosticDescriptor AsyncConverterShouldReturnReaderDescriptor = new(
		AsyncConverterShouldReturnReaderDiagnosticId,
		title: "Return the JsonReader before awaiting or returning",
		messageFormat: "Return the rented JsonReader to the JsonAsyncReader (via ReturnReader) before awaiting, calling another method on the JsonAsyncReader, or leaving the method",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(AsyncConverterShouldReturnReaderDiagnosticId));

	public static readonly DiagnosticDescriptor AsyncConverterShouldNotReuseReaderDescriptor = new(
		AsyncConverterShouldNotReuseReaderDiagnosticId,
		title: "Do not reuse a JsonReader after returning it",
		messageFormat: "This JsonReader was already returned to the JsonAsyncReader and must not be used again; rent a new one with CreateBufferedReader",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(AsyncConverterShouldNotReuseReaderDiagnosticId));

	public static readonly DiagnosticDescriptor AsyncConverterShouldOverridePreferAsyncSerializationDescriptor = new(
		AsyncConverterShouldOverridePreferAsyncSerializationDiagnosticId,
		title: "Override PreferAsyncSerialization on async converters",
		messageFormat: "'{0}' overrides ReadAsync or WriteAsync but does not override PreferAsyncSerialization to return true, so the asynchronous code path is never used",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(AsyncConverterShouldOverridePreferAsyncSerializationDiagnosticId));

	private const string ConverterMetadataName = "Nerdbank.Json.JsonConverter`1";
	private const string AsyncReaderMetadataName = "Nerdbank.Json.JsonAsyncReader";
	private const string AsyncWriterMetadataName = "Nerdbank.Json.JsonAsyncWriter";

	private static readonly RentalAnalysisInputs ReadRentalAnalysis = new()
	{
		RentalMethodNames = new[] { "CreateBufferedReader" }.ToFrozenSet(StringComparer.Ordinal),
		ReturnMethodNames = new[] { "ReturnReader" }.ToFrozenSet(StringComparer.Ordinal),
		ReturnRentalFirst = AsyncConverterShouldReturnReaderDescriptor,
		DoNotReuseRental = AsyncConverterShouldNotReuseReaderDescriptor,
	};

	private static readonly RentalAnalysisInputs WriteRentalAnalysis = new()
	{
		RentalMethodNames = new[] { "CreateWriter" }.ToFrozenSet(StringComparer.Ordinal),
		ReturnMethodNames = new[] { "ReturnWriter" }.ToFrozenSet(StringComparer.Ordinal),
		ReturnRentalFirst = AsyncConverterShouldReturnWriterDescriptor,
		DoNotReuseRental = AsyncConverterShouldNotReuseWriterDescriptor,
	};

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [
		AsyncConverterShouldReturnWriterDescriptor,
		AsyncConverterShouldNotReuseWriterDescriptor,
		AsyncConverterShouldReturnReaderDescriptor,
		AsyncConverterShouldNotReuseReaderDescriptor,
		AsyncConverterShouldOverridePreferAsyncSerializationDescriptor,
	];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new ArgumentNullException(nameof(context));
		}

		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterCompilationStartAction(static context =>
		{
			INamedTypeSymbol? converterBase = context.Compilation.GetTypeByMetadataName(ConverterMetadataName);
			INamedTypeSymbol? asyncReader = context.Compilation.GetTypeByMetadataName(AsyncReaderMetadataName);
			INamedTypeSymbol? asyncWriter = context.Compilation.GetTypeByMetadataName(AsyncWriterMetadataName);
			if (converterBase is null || asyncReader is null || asyncWriter is null)
			{
				return;
			}

			INamedTypeSymbol unboundConverterBase = converterBase.ConstructUnboundGenericType();

			context.RegisterSymbolStartAction(
				context =>
				{
					var target = (INamedTypeSymbol)context.Symbol;
					if (!target.IsOrDerivedFrom(unboundConverterBase))
					{
						return;
					}

					bool isAsyncConverter = target.GetAllMembers().Any(m => m is IMethodSymbol { Name: "ReadAsync" or "WriteAsync", OverriddenMethod: not null });

					context.RegisterOperationBlockAction(context =>
					{
						if (context.OwningSymbol is IMethodSymbol { Name: "ReadAsync" or "WriteAsync", OverriddenMethod: not null } method
							&& method.Parameters.Length > 0)
						{
							bool isReader = method.Name == "ReadAsync";
							RentalAnalysisInputs inputs = isReader ? ReadRentalAnalysis : WriteRentalAnalysis;
							INamedTypeSymbol asyncIoType = isReader ? asyncReader : asyncWriter;
							if (SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, asyncIoType))
							{
								AnalyzeRentalUsage(context, inputs);
							}
						}
					});

					if (isAsyncConverter && !target.IsAbstract && !SymbolEqualityComparer.Default.Equals(target, converterBase))
					{
						context.RegisterSymbolEndAction(context =>
						{
							var symbol = (INamedTypeSymbol)context.Symbol;
							bool overridesPreference = symbol.GetAllMembers().OfType<IPropertySymbol>()
								.Any(p => p is { Name: "PreferAsyncSerialization", OverriddenProperty: not null });
							if (!overridesPreference && symbol.Locations.FirstOrDefault(l => l.IsInSource) is { } location)
							{
								context.ReportDiagnostic(Diagnostic.Create(AsyncConverterShouldOverridePreferAsyncSerializationDescriptor, location, symbol.Name));
							}
						});
					}
				},
				SymbolKind.NamedType);
		});
	}

	private static void AnalyzeRentalUsage(OperationBlockAnalysisContext context, RentalAnalysisInputs inputs)
	{
		var containingMethod = (IMethodSymbol)context.OwningSymbol;
		IParameterSymbol asyncIo = containingMethod.Parameters[0];

		// A rental returned inside a finally clause is guaranteed to be returned on every exit path. Because a ref-struct
		// rental can never be held across an await (that is a compile error), the "must return before await/exit" checks
		// would only produce false positives in that shape, so suppress them. The reuse-after-return checks stay active.
		bool suppressMustReturn = ReturnsRentalInFinally(context, asyncIo, inputs);

		foreach (IOperation block in context.OperationBlocks)
		{
			if (block.Kind != OperationKind.Block)
			{
				continue;
			}

			ControlFlowGraph flow = context.GetControlFlowGraph(block);
			if (flow.Blocks[0].FallThroughSuccessor?.Destination is { } initialBlock)
			{
				VisitBlock(initialBlock, null, ImmutableHashSet.Create<ILocalSymbol>(SymbolEqualityComparer.Default), ImmutableHashSet<BasicBlock>.Empty, null);
			}

			void VisitBlock(BasicBlock basicBlock, ILocalSymbol? rentalHeld, ImmutableHashSet<ILocalSymbol> returnedRentals, ImmutableHashSet<BasicBlock> recursionGuard, ControlFlowBranch? pathToHere)
			{
				if (recursionGuard.Contains(basicBlock))
				{
					return;
				}

				recursionGuard = recursionGuard.Add(basicBlock);

				RentalOperationVisitor visitor = new(context, asyncIo, returnedRentals, rentalHeld, inputs, suppressMustReturn);
				foreach (IOperation op in basicBlock.Operations)
				{
					op.Accept(visitor);
				}

				if (basicBlock.BranchValue is not null)
				{
					basicBlock.BranchValue.Accept(visitor);
				}

				rentalHeld = visitor.CurrentRental;
				returnedRentals = visitor.ReturnedRentals;

				if (basicBlock.FallThroughSuccessor?.Destination is BasicBlock nextBlock)
				{
					VisitBlock(nextBlock, rentalHeld, returnedRentals, recursionGuard, basicBlock.FallThroughSuccessor);
				}

				if (basicBlock.ConditionalSuccessor?.Destination is BasicBlock conditionalBlock)
				{
					VisitBlock(conditionalBlock, rentalHeld, returnedRentals, recursionGuard, basicBlock.ConditionalSuccessor);
				}

				if (!suppressMustReturn
					&& basicBlock.Kind == BasicBlockKind.Exit
					&& pathToHere?.Semantics is ControlFlowBranchSemantics.Return or ControlFlowBranchSemantics.Regular
					&& rentalHeld is not null)
				{
					Location? location =
						pathToHere.Source.BranchValue?.Syntax.FirstAncestorOrSelf<ReturnStatementSyntax>()?.ReturnKeyword.GetLocation()
						?? ((MethodDeclarationSyntax)containingMethod.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken)).Body?.CloseBraceToken.GetLocation()
						?? containingMethod.Locations.FirstOrDefault();
					context.ReportDiagnostic(Diagnostic.Create(inputs.ReturnRentalFirst, location));
				}
			}
		}
	}

	private static bool ReturnsRentalInFinally(OperationBlockAnalysisContext context, IParameterSymbol asyncIo, RentalAnalysisInputs inputs)
	{
		foreach (IOperation block in context.OperationBlocks)
		{
			foreach (IOperation op in block.Descendants())
			{
				if (op is IInvocationOperation invocation
					&& invocation.Instance is IParameterReferenceOperation { Parameter: IParameterSymbol p }
					&& SymbolEqualityComparer.Default.Equals(p, asyncIo)
					&& inputs.ReturnMethodNames.Contains(invocation.TargetMethod.Name)
					&& invocation.Syntax.FirstAncestorOrSelf<FinallyClauseSyntax>() is not null)
				{
					return true;
				}
			}
		}

		return false;
	}

	private struct RentalAnalysisInputs
	{
		public FrozenSet<string> RentalMethodNames { get; init; }

		public FrozenSet<string> ReturnMethodNames { get; init; }

		public DiagnosticDescriptor ReturnRentalFirst { get; init; }

		public DiagnosticDescriptor DoNotReuseRental { get; init; }
	}

	private sealed class RentalOperationVisitor(OperationBlockAnalysisContext context, IParameterSymbol asyncIo, ImmutableHashSet<ILocalSymbol> returnedRentals, ILocalSymbol? currentRental, RentalAnalysisInputs inputs, bool suppressMustReturn) : OperationVisitor
	{
		internal ILocalSymbol? CurrentRental => currentRental;

		internal ImmutableHashSet<ILocalSymbol> ReturnedRentals => returnedRentals;

		public override void DefaultVisit(IOperation operation)
		{
			foreach (IOperation op in operation.ChildOperations)
			{
				op.Accept(this);
			}
		}

		public override void VisitInvocation(IInvocationOperation operation)
		{
			base.VisitInvocation(operation);

			if (operation.Instance is IParameterReferenceOperation { Parameter: IParameterSymbol p } && SymbolEqualityComparer.Default.Equals(p, asyncIo))
			{
				if (inputs.RentalMethodNames.Contains(operation.TargetMethod.Name))
				{
					if (currentRental is not null && !suppressMustReturn)
					{
						context.ReportDiagnostic(Diagnostic.Create(inputs.ReturnRentalFirst, operation.Syntax.GetLocation()));
					}

					if (operation.Parent is IAssignmentOperation { Target: ILocalReferenceOperation { Local: ILocalSymbol local } })
					{
						currentRental = local;
						returnedRentals = returnedRentals.Remove(local);
					}
				}
				else if (inputs.ReturnMethodNames.Contains(operation.TargetMethod.Name))
				{
					if (currentRental is not null)
					{
						returnedRentals = returnedRentals.Add(currentRental);
						currentRental = null;
					}
				}
				else if (currentRental is not null && !suppressMustReturn)
				{
					// Other methods on the async reader/writer are unsafe while a rental is outstanding.
					context.ReportDiagnostic(Diagnostic.Create(inputs.ReturnRentalFirst, operation.Syntax.GetLocation()));
				}
			}
		}

		public override void VisitLocalReference(ILocalReferenceOperation operation)
		{
			bool leftOfAssignment = operation.Parent is IAssignmentOperation assignment && assignment.Target == operation;
			if (!leftOfAssignment && returnedRentals.Contains(operation.Local))
			{
				context.ReportDiagnostic(Diagnostic.Create(inputs.DoNotReuseRental, operation.Syntax.GetLocation()));
			}

			base.VisitLocalReference(operation);
		}

		public override void VisitAwait(IAwaitOperation operation)
		{
			base.VisitAwait(operation);

			if (currentRental is not null && !suppressMustReturn)
			{
				context.ReportDiagnostic(Diagnostic.Create(inputs.ReturnRentalFirst, ((AwaitExpressionSyntax)operation.Syntax).AwaitKeyword.GetLocation()));
			}
		}
	}
}
