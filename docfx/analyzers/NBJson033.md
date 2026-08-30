# NBJson033: Return the JsonWriter before awaiting or returning

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson033 |
| **Category** | Usage |
| **Severity** | Error |

## Cause

Inside an overridden `JsonConverter<T>.WriteAsync`, a `JsonWriter` rented from `JsonAsyncWriter.CreateWriter()` is still
outstanding when the method awaits, calls another method on the `JsonAsyncWriter`, or returns.

## Rule description

A rented `JsonWriter` is a `ref struct` that shares buffer state with the `JsonAsyncWriter`. You must hand it back with
`ReturnWriter(ref writer)` before doing anything else with the `JsonAsyncWriter` (including awaiting a flush) or leaving
the method. Failing to do so corrupts writer state or throws at runtime.

## How to fix violations

Return the writer as soon as you finish the synchronous write, then await or continue.

```csharp
// ✗ Bad: the writer is never returned before the method exits.
public override ValueTask WriteAsync(JsonAsyncWriter writer, string? value, SerializationContext context)
{
    JsonWriter w = writer.CreateWriter();
    w.WriteStringValue(value);
    return default; // NBJson033
}

// ✓ Good
public override async ValueTask WriteAsync(JsonAsyncWriter writer, string? value, SerializationContext context)
{
    JsonWriter w = writer.CreateWriter();
    w.WriteStringValue(value);
    writer.ReturnWriter(ref w);
    await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
}
```

## When to suppress

Do not suppress this diagnostic. Returning the rental is required for correct behavior. Note that the analyzer does not
report this rule for methods that return the rental inside a `finally` clause, since that is always safe.
