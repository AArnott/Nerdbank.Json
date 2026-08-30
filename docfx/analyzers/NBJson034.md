# NBJson034: Do not reuse a JsonWriter after returning it

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson034 |
| **Category** | Usage |
| **Severity** | Error |

## Cause

Inside an overridden `JsonConverter<T>.WriteAsync`, a `JsonWriter` is used after it was already handed back to the
`JsonAsyncWriter` with `ReturnWriter`.

## Rule description

`ReturnWriter(ref writer)` resets the local to its default state and advances the underlying `JsonAsyncWriter`. Any
further use of that variable writes to an invalid writer. Rent a fresh `JsonWriter` with `CreateWriter()` if you need to
write again.

## How to fix violations

```csharp
// ✗ Bad
JsonWriter w = writer.CreateWriter();
writer.ReturnWriter(ref w);
w.WriteNullValue(); // NBJson034

// ✓ Good
JsonWriter w = writer.CreateWriter();
w.WriteNullValue();
writer.ReturnWriter(ref w);

JsonWriter w2 = writer.CreateWriter();
w2.WriteEndObject();
writer.ReturnWriter(ref w2);
```

## When to suppress

Do not suppress this diagnostic; reusing a returned writer is always a bug.
