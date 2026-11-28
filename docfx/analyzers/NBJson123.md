# NBJson123: Union discriminator cases disagree

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson123 |
| **Category** | Usage |
| **Severity** | Error |

## Cause

A C# union or a `[DerivedTypeShape]` hierarchy has cases whose discriminator names differ as written, or whose discriminator values are the same.
Cases with no discriminator are allowed.

## How to fix violations

Use the same property name, or the same type-level JSON name, on every case that declares one.
Give each of those cases a distinct value.

The analyzer compares names as declared, including a `[PropertyShape(Name = ...)]` override. It does not apply a property naming policy.

## When to suppress

Suppress when the names differ only by a naming policy and the runtime serializer is known to accept them.
The converter still checks the serialized names when it is built.
