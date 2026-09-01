# NBJson036: Do not reuse a JsonReader after returning it

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson036 |
| **Category** | Usage |
| **Severity** | Error |

## Cause

Inside an overridden `JsonConverter<T>.ReadAsync`, a `JsonReader` is used after it was already handed back to the
`JsonAsyncReader` with `ReturnReader`.

## Rule description

`ReturnReader(ref reader)` resets the local to its default state and advances the underlying `JsonAsyncReader`. Any
further use of that variable reads from an invalid reader. Rent a fresh `JsonReader` with `CreateBufferedReader()` (after
buffering the next value) if you need to read again.

## How to fix violations

```csharp
// ✗ Bad
JsonReader r = reader.CreateBufferedReader();
reader.ReturnReader(ref r);
string token = r.ReadNumberToken(); // NBJson036

// ✓ Good
JsonReader r = reader.CreateBufferedReader();
string token = r.ReadNumberToken();
reader.ReturnReader(ref r);
```

## When to suppress

Do not suppress this diagnostic; reusing a returned reader is always a bug.
