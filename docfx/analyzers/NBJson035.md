# NBJson035: Return the JsonReader before awaiting or returning

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson035 |
| **Category** | Usage |
| **Severity** | Error |

## Cause

Inside an overridden `JsonConverter<T>.ReadAsync`, a `JsonReader` rented from `JsonAsyncReader.CreateBufferedReader()` is
still outstanding when the method awaits, calls another method on the `JsonAsyncReader`, or returns.

## Rule description

A rented `JsonReader` is a `ref struct` that reads from the buffer owned by the `JsonAsyncReader`. You must hand it back
with `ReturnReader(ref reader)` — which advances the async reader past the consumed bytes — before doing anything else
with the `JsonAsyncReader`, before awaiting, or before leaving the method.

## How to fix violations

```csharp
// ✗ Bad: the reader is never returned before the method exits.
public override ValueTask<string?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
{
    JsonReader r = reader.CreateBufferedReader();
    string token = r.ReadNumberToken();
    return new ValueTask<string?>(token); // NBJson035
}

// ✓ Good
public override async ValueTask<string?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
{
    await reader.BufferNextValueAsync(context).ConfigureAwait(false);
    JsonReader r = reader.CreateBufferedReader();
    string token = r.ReadNumberToken();
    reader.ReturnReader(ref r);
    return token;
}
```

## When to suppress

Do not suppress this diagnostic. The analyzer does not report this rule for methods that return the rental inside a
`finally` clause, since that is always safe.
