# NBJson002: Dictionary key type is not supported

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson002 |
| **Category** | Usage |
| **Severity** | Warning |

## Cause

A dictionary in a generated serialization contract has a key type that Nerdbank.Json cannot encode as a JSON property name.
The rule also checks direct serializer calls using the default reflection provider's shapes.
It does not diagnose dictionaries merely because they appear on an unrelated, unshaped class.

Supported keys exactly match the library's dictionary key formatter: strings, characters, booleans, the built-in integer and floating-point types, decimal, <xref:System.Numerics.BigInteger>, <xref:System.DateTime>, <xref:System.DateTimeOffset>, <xref:System.TimeSpan>, <xref:System.Guid>, and enums.
The modern .NET library asset additionally supports <xref:System.Half>, <xref:System.Int128>, <xref:System.UInt128>, <xref:System.DateOnly>, <xref:System.TimeOnly>, and <xref:System.Text.Rune>.
Nullable keys, `object`, native integers, and complex keys are not supported.

## How to fix

Use a supported key type, or select a converter for the entire dictionary that uses a different representation.
For example, this contract uses GUID keys:

[!code-csharp[](../../samples/cs/AnalyzerGuidance.cs#SupportedContract)]

A custom converter or marshaler on the **key type** does not change the dictionary property-name formatter and does not suppress this rule.
A converter on the **dictionary type or member** does stop inference.

## Scope and suppression

Generated roots, witnesses, nested/collection/optional graphs, member inclusion, constructor selection, and override handling follow the same [contract-analysis scope](index.md#shape-contract-analysis) as NBJson001.
Unknown generic key arguments are not guessed.
Runtime converter registrations and custom providers cannot generally be predicted; suppress a reviewed intentional case with `.editorconfig` or `#pragma warning disable NBJson002`.
There is no automatic code fix.
