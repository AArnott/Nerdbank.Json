# NBJson031: Consume or produce exactly one JSON value

Category: **Usage**. Default severity: **Warning**.

A synchronous `JsonConverter<T>.Read` or `Write` override has a normal return path that demonstrably consumes or produces zero values, multiple root values, or an incomplete or mismatched object/array.
The diagnostic is on the method name.
Throwing paths have no single-value obligation.
Calls marked with <xref:System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute> are treated as non-returning paths too.

Writing two independent root values is incorrect.
Wrap composite values in a complete container, as in this compilable converter example:

[!code-csharp[](../../samples/cs/CustomConverters.cs#YourOwnConverter)]

The analysis follows control-flow branches, conditional `TryReadNull` and `TryReadEndObject`/`TryReadEndArray`, nested converter calls, and known local reference aliases.
Reading a peek reader does not consume the original reader.
Property names are not independent values; writer objects also require values for their property names.

This is deliberately incomplete, not a JSON validator.
Unknown helpers receiving the reader/writer, raw writes, unrecognized reader/writer APIs, ignored conditional reads, exception regions, exhaustive enum switches without a default, repeated loop iterations, and analysis-budget exhaustion stop inference on the affected path.
Loop exit paths can still be checked.
Async overrides are not analyzed by this rule; NBJson033–NBJson037 address rental safety instead.
Token separators and reader object property/value ordering are not validated.
No automatic fix is offered because the intended representation cannot be inferred.

Runtime invariants that correlate separate conditions may not be visible in the control-flow graph.
Suppress a reviewed intentional case with `#pragma warning disable NBJson031` or configure the rule in `.editorconfig`.
