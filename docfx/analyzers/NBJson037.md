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

`PreferAsyncSerialization` returns `false` by default. When it is `false`, the serializer buffers the value and uses the
synchronous `Read`/`Write` path, so a converter's custom `ReadAsync`/`WriteAsync` override is never actually invoked.
Overriding `PreferAsyncSerialization` to return `true` opts the converter into the streaming asynchronous code path.

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
