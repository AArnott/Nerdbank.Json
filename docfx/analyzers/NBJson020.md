# NBJson020: Converter type must be compatible

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson020 |
| **Category** | Usage |
| **Severity** | Error |

## Cause

<xref:Nerdbank.Json.JsonConverterAttribute> selects a converter that is abstract, cannot be closed for the decorated value type, or does not derive from <xref:Nerdbank.Json.JsonConverter`1> for that exact type.
The rule checks attributes on types, fields, properties, and parameters.
An open generic converter is checked after substituting the decorated constructed type's arguments when their arities match.

## How to fix violations

Select a concrete converter whose generic value type matches the decorated value.
A member-level attribute must match the member's type, not its containing type.
See [custom converters](../docs/custom-converters.md) for registration examples.

There is no automatic code fix for this rule.

## When to suppress

Do not suppress an incompatible converter registration; it cannot be activated as the required converter.
