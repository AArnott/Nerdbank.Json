# NBJson001: Delegate types are not supported

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson001 |
| **Category** | Usage |
| **Severity** | Warning |

## Cause

A generated serialization contract contains a delegate value without a statically selected custom converter.
This includes delegate array/collection elements, dictionary values, nullable tuples, and reachable nested or declarative union contracts.
Roots are discovered through `[GenerateShape]` and generic, `typeof`, or wildcard witness attributes.
Direct serializer calls with shapes obtained from the default reflection provider are also checked.
Unrelated unshaped classes and unused reflection shapes are not diagnosed.

## How to fix

Exclude callbacks from the serialized contract, represent them as supported data, or explicitly select a converter for their wire representation.
For example:

[!code-csharp[](../../samples/cs/AnalyzerGuidance.cs#SupportedContract)]

## Scope and suppression

The rule uses PolyType's Roslyn data models for member inclusion and follows nullable, collection, dictionary value, tuple, and object contracts.
Ignored and non-opted-in nonpublic members are excluded.
Read-only members are checked when bound to a known constructor or when they expose a recognizable mutable collection.
Explicit type/member converters stop traversal on that edge; a converter on a matching selected constructor parameter conservatively excludes the member too.
Recognizable closed marshalers are analyzed through their surrogate contract rather than the original members.

Unknown generic arguments, customized shape kinds, unrecognized marshalers, ambiguous constructor selection, runtime providers, helper-returned reflection shapes, converter factories, and converter collections are not inferred.
Runtime registrations may make an otherwise unsupported generated contract valid; use `.editorconfig` or `#pragma warning disable NBJson001` for a reviewed intentional registration.
See the [contract-analysis scope](index.md#shape-contract-analysis) for details.
There is no automatic code fix.
