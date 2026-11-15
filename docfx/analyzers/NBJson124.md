# NBJson124: Key the union discriminator at 0

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson124 |
| **Category** | Usage |
| **Severity** | Warning |

## Cause

The type uses `Nerdbank.MessagePack.KeyAttribute`, and its `[UnionDiscriminator]` member is missing or is not `Key(0)`.

## How to fix violations

Put `[Key(0)]` on the discriminator member when the same type is also keyed for another serializer.
JSON objects are name-keyed, so the index does not change the JSON property order. The discriminator is still written ahead of the type's other properties.

Types that do not use `Key` are not diagnosed.

## When to suppress

Suppress when the keyed layout intentionally puts another member at index 0.
