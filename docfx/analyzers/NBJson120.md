# NBJson120: Union discriminator must be a matching constant

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson120 |
| **Category** | Usage |
| **Severity** | Warning |

## Cause

A `[UnionDiscriminator]` property or field does not return a constant, or its constant differs from the attribute argument.

## How to fix violations

Return a constant from a get-only property, or use a constant field the shape includes.
When the attribute supplies a value, that value must be the same constant.
The **Use the member's constant** code fix replaces the attribute argument with the member's constant expression.

## When to suppress

Suppress only when the member is constant in a way the compiler cannot see.
