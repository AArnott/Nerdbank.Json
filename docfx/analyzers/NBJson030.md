# NBJson030: Do not call the top-level serializer from a converter

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson030 |
| **Category** | Usage |
| **Severity** | Warning |

## Cause

A type derived from <xref:Nerdbank.Json.JsonConverter`1> constructs a <xref:Nerdbank.Json.JsonSerializer> or invokes a method declared on that serializer type.
The rule also recognizes serializer-derived types.

## Rule description

A top-level serializer call starts a separate operation rather than continuing the converter's current operation.
That can lose shared state, depth accounting, and reference tracking.
Passing a converter's active context to a top-level serializer is rejected at runtime.

## How to fix violations

Obtain nested converters through <xref:Nerdbank.Json.SerializationContext.GetConverter*> and invoke their read or write methods with the current reader or writer and context.
During factory construction, use <xref:Nerdbank.Json.ConverterContext> to resolve supporting converters.
See [delegating to sub-values](../docs/custom-converters.md#delegating-to-sub-values).

There is no automatic code fix for this rule.

## When to suppress

Suppress only after confirming that a deliberately independent serialization operation is required and does not replace nested processing in the current operation.
