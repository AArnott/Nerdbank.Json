# NBJson121: Union discriminator must be one get-only member

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson121 |
| **Category** | Usage |
| **Severity** | Error |

## Cause

A type declares more than one `[UnionDiscriminator]`, the member is a field or has a setter, it is bound to a constructor parameter, it uses the name `$type` or `$value`, or a type-level attribute omits its property name or reuses another property's name.
Object shapes do not include constant fields.

## How to fix violations

Keep a single get-only discriminator.
Apply `[UnionDiscriminator("kind", "create")]` to the type when it has no such member.
The type-level name is the JSON name and is not passed through the naming policy.

## When to suppress

Do not suppress this rule. The converter rejects the same declarations.
