// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Nerdbank.Json.Analyzers;

/// <summary>
/// Finds provable violations of the single-value synchronous converter contract.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConverterStructureAnalyzer : DiagnosticAnalyzer
{
	/// <summary>The diagnostic ID for an incorrect JSON structure count.</summary>
	public const string NotExactlyOneStructureDiagnosticId = "NBJson031";

	private static readonly DiagnosticDescriptor NotExactlyOneStructure = new(
		NotExactlyOneStructureDiagnosticId,
		"Consume or produce exactly one JSON value",
		"This converter has a normal return path that does not consume or produce exactly one complete JSON value (roots: {0}, open containers: {1}, invalid structure: {2})",
		"Usage",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: AnalyzerUtilities.GetHelpLink(NotExactlyOneStructureDiagnosticId));

	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [NotExactlyOneStructure];

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
			INamedTypeSymbol? converter = context.Compilation.GetTypeByMetadataName(KnownNames.GenericConverter);
			INamedTypeSymbol? reader = context.Compilation.GetTypeByMetadataName(KnownNames.Reader);
			INamedTypeSymbol? writer = context.Compilation.GetTypeByMetadataName(KnownNames.Writer);
			if (converter is null || reader is null || writer is null)
			{
				return;
			}

			context.RegisterOperationBlockAction(context =>
			{
				if (context.OwningSymbol is not IMethodSymbol { Name: "Read" or "Write", IsOverride: true } method ||
					!IsConverterMethod(method, converter) || method.Parameters.Length == 0)
				{
					return;
				}

				foreach (IOperation operation in context.OperationBlocks)
				{
					if (operation is not IBlockOperation)
					{
						continue;
					}

					ControlFlowGraph graph = context.GetControlFlowGraph(operation);

					// Exception-region transitions can execute implicit finally blocks.
					if (HasExceptionRegions(graph.Root) || HasExhaustiveEnumSwitch(operation))
					{
						continue;
					}

					HashSet<int> reportedExits = new();
					int budget = 512;
					Visit(graph.Blocks[0], new State(method.Parameters[0], method.Name == "Write"), ImmutableHashSet<int>.Empty);

					void Visit(BasicBlock block, State state, ImmutableHashSet<int> visited)
					{
						context.CancellationToken.ThrowIfCancellationRequested();
						if (--budget < 0 || !block.IsReachable || visited.Contains(block.Ordinal))
						{
							return;
						}

						visited = visited.Add(block.Ordinal);
						if (block.Kind == BasicBlockKind.Exit)
						{
							if ((state.Invalid || state.Roots != 1 || state.Containers.Count != 0) && reportedExits.Add(block.Ordinal))
							{
								context.ReportDiagnostic(Diagnostic.Create(NotExactlyOneStructure, method.Locations.FirstOrDefault(), state.Roots, state.Containers.Count, state.Invalid));
							}

							return;
						}

						foreach (IOperation item in block.Operations)
						{
							if (!Apply(item, state))
							{
								return;
							}
						}

						IOperation? condition = block.BranchValue;
						bool negated = false;
						while (condition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unary)
						{
							condition = unary.Operand;
							negated = !negated;
						}

						if (block.ConditionKind != ControlFlowConditionKind.None && block.BranchValue?.ConstantValue is { HasValue: true, Value: bool constant })
						{
							FollowBranch(
								constant == (block.ConditionKind == ControlFlowConditionKind.WhenTrue) ? block.ConditionalSuccessor : block.FallThroughSuccessor,
								state);
						}
						else if (block.ConditionKind != ControlFlowConditionKind.None &&
							condition is IInvocationOperation conditional && IsOriginal(conditional.Instance, state) &&
							SymbolEqualityComparer.Default.Equals(conditional.TargetMethod.ContainingType, reader) &&
							conditional.TargetMethod.Name is "TryReadNull" or "TryReadEndObject" or "TryReadEndArray")
						{
							Follow(block.FallThroughSuccessor, block.ConditionKind == ControlFlowConditionKind.WhenFalse);
							Follow(block.ConditionalSuccessor, block.ConditionKind == ControlFlowConditionKind.WhenTrue);

							void Follow(ControlFlowBranch? branch, bool result)
							{
								State next = state.Clone();
								if (result != negated)
								{
									ApplyToken(conditional.TargetMethod.Name, next);
								}

								FollowBranch(branch, next);
							}
						}
						else if (block.BranchValue is null || Apply(block.BranchValue, state))
						{
							FollowBranch(block.FallThroughSuccessor, state.Clone());
							FollowBranch(block.ConditionalSuccessor, state.Clone());
						}

						void FollowBranch(ControlFlowBranch? branch, State next)
						{
							if (branch?.Destination is { } destination &&
								branch.Semantics is ControlFlowBranchSemantics.Regular or ControlFlowBranchSemantics.Return)
							{
								Visit(destination, next, visited);
							}
						}
					}

					bool Apply(IOperation item, State state)
					{
						if (item is IAnonymousFunctionOperation or ILocalFunctionOperation or IFlowAnonymousFunctionOperation)
						{
							return true;
						}

						foreach (IOperation child in item.ChildOperations)
						{
							if (!Apply(child, state))
							{
								return false;
							}
						}

						if (item is ISimpleAssignmentOperation assignment)
						{
							ISymbol? target = Reference(assignment.Target);
							if (assignment.IsRef && target is ILocalSymbol)
							{
								if (IsOriginal(assignment.Value, state))
								{
									state.Aliases.Add(target);
								}
								else
								{
									state.Aliases.Remove(target);
								}
							}
							else if (IsOriginal(assignment.Target, state))
							{
								return false;
							}
						}

						if (item is not IInvocationOperation invocation)
						{
							return item is not IInvalidOperation &&
								!(item is IObjectCreationOperation creation && creation.Arguments.Any(a => IsOriginal(a.Value, state)));
						}

						if (IsOriginal(invocation.Instance, state))
						{
							IMethodSymbol called = invocation.TargetMethod;
							if (SymbolEqualityComparer.Default.Equals(called.ContainingType, reader) ||
								SymbolEqualityComparer.Default.Equals(called.ContainingType, writer))
							{
								return called.Name is not ("TryReadNull" or "TryReadEndObject" or "TryReadEndArray") &&
									ApplyToken(called.Name, state);
							}

							return false;
						}

						bool passesOriginal = invocation.Arguments.Any(a => IsOriginal(a.Value, state));
						if (passesOriginal)
						{
							if (IsConverterMethod(invocation.TargetMethod, converter))
							{
								state.Value();
								return true;
							}

							return false;
						}

						return true;
					}
				}
			});
		});
	}

	private static bool IsConverterMethod(IMethodSymbol method, INamedTypeSymbol converter)
	{
		while (method.OverriddenMethod is { } overridden)
		{
			method = overridden;
		}

		return method.Name is "Read" or "Write" or "ReadObject" or "WriteObject" &&
			(SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, converter) ||
				SymbolEqualityComparer.Default.Equals(method.ContainingType, converter.BaseType));
	}

	private static bool HasExceptionRegions(ControlFlowRegion region)
		=> region.Kind is ControlFlowRegionKind.Try or ControlFlowRegionKind.Catch or ControlFlowRegionKind.Finally ||
			region.NestedRegions.Any(HasExceptionRegions);

	private static bool HasExhaustiveEnumSwitch(IOperation operation)
	{
		foreach (ISwitchOperation @switch in operation.DescendantsAndSelf().OfType<ISwitchOperation>())
		{
			if (@switch.Value.Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType ||
				@switch.Cases.SelectMany(c => c.Clauses).Any(c => c is IDefaultCaseClauseOperation))
			{
				continue;
			}

			HashSet<object> cases = new(@switch.Cases.SelectMany(c => c.Clauses)
				.OfType<ISingleValueCaseClauseOperation>()
				.Select(c => c.Value.ConstantValue)
				.Where(c => c.HasValue && c.Value is not null)
				.Select(c => c.Value!));
			if (enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).All(f => cases.Contains(f.ConstantValue!)))
			{
				return true;
			}
		}

		return false;
	}

	private static ISymbol? Reference(IOperation? operation)
		=> operation switch
		{
			IParameterReferenceOperation parameter => parameter.Parameter,
			ILocalReferenceOperation local => local.Local,
			IConversionOperation conversion => Reference(conversion.Operand),
			_ => null,
		};

	private static bool IsOriginal(IOperation? operation, State state)
		=> Reference(operation) is { } symbol && state.Aliases.Contains(symbol);

	private static bool ApplyToken(string name, State state)
	{
		switch (name)
		{
			case "WriteStartObject":
			case "ReadStartObject":
				state.Value();
				state.Containers.Add('o');
				break;
			case "WriteStartArray":
			case "ReadStartArray":
				state.Value();
				state.Containers.Add('a');
				break;
			case "WriteEndObject":
			case "TryReadEndObject":
				state.End('o');
				break;
			case "WriteEndArray":
			case "ReadEndArray":
			case "TryReadEndArray":
				state.End('a');
				break;
			case "WritePropertyName":
				if (state.Containers.Count == 0 || state.Containers[state.Containers.Count - 1] != 'o')
				{
					state.Invalid = true;
				}
				else
				{
					state.Containers[state.Containers.Count - 1] = 'p';
				}

				break;
			case "ReadString":
			case "ReadRequiredString":
			case "ReadBoolean":
			case "ReadChar":
			case "ReadByte":
			case "ReadSByte":
			case "ReadInt16":
			case "ReadUInt16":
			case "ReadInt32":
			case "ReadUInt32":
			case "ReadInt64":
			case "ReadUInt64":
			case "ReadSingle":
			case "ReadDouble":
			case "ReadDecimal":
			case "ReadNumberToken":
			case "ReadBase64Bytes":
			case "ReadRequiredBase64Bytes":
			case "ReadRawValue":
			case "Skip":
			case "Write":
			case "WriteNull":
			case "WriteBase64StringValue":
			case "TryReadNull":
				state.Value();
				break;
			case "CreatePeekReader":
			case "EnsureFullyConsumed":
			case "ReadNameSeparator":
			case "ReadValueSeparator":
			case "WriteValueSeparator":
			case "Flush":
				break;
			default:
				return false;
		}

		return true;
	}

	private sealed class State
	{
		/// <summary>Initializes a new instance of the <see cref="State"/> class.</summary>
		/// <param name="parameter">The original reader or writer.</param>
		/// <param name="writing">Whether object property/value ordering can be checked.</param>
		internal State(IParameterSymbol parameter, bool writing)
		{
			this.Aliases.Add(parameter);
			this.Writing = writing;
		}

		private State()
		{
		}

		/// <summary>Gets the known reference aliases of the original reader or writer.</summary>
		internal HashSet<ISymbol> Aliases { get; } = new(SymbolEqualityComparer.Default);

		/// <summary>Gets the container stack; p denotes a pending object property value.</summary>
		internal List<char> Containers { get; } = new();

		/// <summary>Gets the root value count.</summary>
		internal int Roots { get; private set; }

		/// <summary>Gets or sets a value indicating whether the structure is provably invalid.</summary>
		internal bool Invalid { get; set; }

		private bool Writing { get; set; }

		/// <summary>Records one value in the current container or at the root.</summary>
		internal void Value()
		{
			if (this.Containers.Count == 0)
			{
				this.Roots++;
			}
			else if (this.Containers[this.Containers.Count - 1] == 'p')
			{
				this.Containers[this.Containers.Count - 1] = 'o';
			}
			else if (this.Writing && this.Containers[this.Containers.Count - 1] == 'o')
			{
				this.Invalid = true;
			}
		}

		/// <summary>Closes a matching container.</summary>
		/// <param name="kind">The expected container kind.</param>
		internal void End(char kind)
		{
			if (this.Containers.Count == 0 || this.Containers[this.Containers.Count - 1] != kind)
			{
				this.Invalid = true;
			}
			else
			{
				this.Containers.RemoveAt(this.Containers.Count - 1);
			}
		}

		/// <summary>Copies the state for an independent successor path.</summary>
		/// <returns>The copied state.</returns>
		internal State Clone()
		{
			State clone = new() { Roots = this.Roots, Invalid = this.Invalid, Writing = this.Writing };
			clone.Aliases.UnionWith(this.Aliases);
			clone.Containers.AddRange(this.Containers);
			return clone;
		}
	}
}
