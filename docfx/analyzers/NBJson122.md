# NBJson122: Unsupported union discriminator type

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson122 |
| **Category** | Usage |
| **Severity** | Error |

## Cause

The discriminator constant is not a `string`, `bool`, integer primitive (`byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, or `ulong`), or enum. Null is not a discriminator value.

## How to fix violations

Use one of those constant types.

## When to suppress

Do not suppress this rule. Other types are rejected when the converter is built.
