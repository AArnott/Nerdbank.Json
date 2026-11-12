# NBJson037: Async converters should override PreferAsyncSerialization

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson037 |
| **Category** | Usage |
| **Severity** | Warning |

## Cause

A concrete `JsonConverter<T>` overrides `ReadAsync` and/or `WriteAsync` but does not override `PreferAsyncSerialization`
to return `true`.

## Rule description

`PreferAsyncSerialization` returns `false` by default. Synchronous `Read` and `Write` are always required. When the
property is `false`, a nested value is buffered and read or written synchronously, so a custom `ReadAsync`/`WriteAsync`
override is not used for that nested value. A value already complete within `MaxAsyncBuffer` is also read with `Read`.
Overriding `PreferAsyncSerialization` to return `true` selects the asynchronous override for a nested value while an
ancestor is reading asynchronously.

## How to fix violations

```csharp
// ✗ Bad: WriteAsync is overridden but never used because PreferAsyncSerialization is false.
class MyConverter : JsonConverter<string>
{
    public override string? Read(ref JsonReader reader, SerializationContext context) => null;
    public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
    public override async ValueTask WriteAsync(JsonAsyncWriter writer, string? value, SerializationContext context)
        => await Task.Yield();
}

// ✓ Good
class MyConverter : JsonConverter<string>
{
    public override bool PreferAsyncSerialization => true;
    public override string? Read(ref JsonReader reader, SerializationContext context) => null;
    public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
    public override async ValueTask WriteAsync(JsonAsyncWriter writer, string? value, SerializationContext context)
        => await Task.Yield();
}
```

## When to suppress

Suppress this diagnostic only if you intentionally provide an async override that should remain dormant (for example, a
base class that concrete converters override further). In most cases you should override `PreferAsyncSerialization`.
