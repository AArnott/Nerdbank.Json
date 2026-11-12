# NBJson004: Serialization callbacks are supported only on reference types

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson004 |
| **Category** | Usage |
| **Severity** | Error |

## Cause

A struct or record struct implements <xref:Nerdbank.Json.IJsonSerializationCallbacks>, directly or through another interface.

## Rule description

The serializer calls these methods through the interface.
That call boxes a struct, so the callback mutates a copy and those changes are lost.
The interface is supported only on reference types.

## How to fix violations

Change the type to a class or record class.
If the value must remain a struct, keep the callback logic in the code that reads the struct after deserialization instead of implementing the interface.

There is no automatic code fix for this rule.

## When to suppress

Do not suppress this diagnostic. The runtime cannot preserve mutations made on the boxed copy.
