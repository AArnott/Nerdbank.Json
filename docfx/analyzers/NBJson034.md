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

Finish all writes through the rental before calling <xref:Nerdbank.Json.JsonAsyncWriter.ReturnWriter*>.
If more synchronous writing is needed later, call <xref:Nerdbank.Json.JsonAsyncWriter.CreateWriter> to obtain a new rental, then return that rental before continuing asynchronous work.

## When to suppress

Do not suppress this diagnostic; reusing a returned writer is always a bug.
