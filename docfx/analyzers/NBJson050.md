# NBJson050: Pass JSON readers and writers by ref

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson050 |
| **Category** | Usage |
| **Severity** | Warning |

## Cause

A method declares a <xref:Nerdbank.Json.JsonReader> or <xref:Nerdbank.Json.JsonWriter> parameter by value.
The rule applies to these two types, not to all ref structs.

## Rule description

Readers and writers carry mutable cursor and buffer state.
Passing one by value copies that state, so changes made by the callee are not reflected in the caller's local.

## How to fix violations

Pass the parameter by `ref` and update its call sites accordingly.
If an independent read cursor is intentional, use <xref:Nerdbank.Json.JsonReader.CreatePeekReader> to make that copy explicit.

There is no automatic code fix for this rule.

## When to suppress

Suppress only when an intentional copy is required and its independent state is understood.
