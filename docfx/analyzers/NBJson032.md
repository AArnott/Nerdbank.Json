# NBJson032: Describe the converter's JSON schema

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson032 |
| **Category** | Usage |
| **Severity** | Info |

## Cause

A concrete <xref:Nerdbank.Json.JsonConverter`1> has no non-abstract override of <xref:Nerdbank.Json.JsonConverter.GetJsonSchema*> in its inheritance hierarchy.
An inherited implementation satisfies this rule.

## Rule description

An undocumented converter produces a permissive schema annotated with a `$comment`, not a precise description of its wire format.
This is valid output, so the diagnostic is informational rather than an error.
The rule checks for an override, not whether its schema is complete or correct.

## How to fix violations

Override the schema hook and return a <xref:System.Text.Json.Nodes.JsonObject> describing the representation.
Use <xref:Nerdbank.Json.JsonSchemaContext> to obtain nested schemas and respect the requested dialect.
See [JSON Schema export](../docs/json-schema.md#custom-converters) for an existing sample.

There is no automatic code fix for this rule.

## When to suppress

Suppress when deliberately leaving the representation undocumented, for example when the converter's format cannot be described accurately.
