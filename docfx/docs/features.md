# Features

Nerdbank.Json currently focuses on a narrow but working core.

## Low-Level UTF-8 Writer

The low-level writer writes UTF-8 directly to `IBufferWriter<byte>`, which keeps the core serializer independent from string-building intermediate layers.

Key traits:

* Direct UTF-8 emission.
* RFC 8259 escaping for required characters only.
* Numeric formatting using invariant culture.
* Base64 encoding for byte buffers.

## Built-In Type Coverage

The built-in serializer currently supports:

* `string`, `char`, `bool`
* `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`
* `BigInteger`
* `DateTime`, `DateTimeOffset`, `TimeSpan`
* `Guid`, `Version`, `Uri`, `CultureInfo`, `Encoding`
* `byte[]`, `Memory<byte>`, `ReadOnlyMemory<byte>`
* `Color`, `Point`
* On .NET 8 and later: `Half`, `Int128`, `UInt128`, `DateOnly`, `TimeOnly`, `Rune`

## PolyType Object Graphs

The serializer now includes an initial PolyType-based contract layer for object graphs.

Current behavior:

* Mutable object types with settable properties can be serialized and deserialized.
* Nested objects compose through the same converter cache.
* Mutable `ICollection<T>` implementations with public parameterless constructors can be serialized and deserialized.
* Mutable `IDictionary<string, TValue>` implementations with public parameterless constructors can be serialized and deserialized.
* Unknown JSON properties are ignored during deserialization.
* Property names default to camelCase.
* `JsonSerializer.PropertyNamingPolicy` can be set to `null` or another `JsonNamingPolicy` built-in.
* `PropertyShapeAttribute.Name` overrides the naming policy when explicitly set.
* Dictionary keys are not transformed by the property naming policy by default.
* `JsonSerializer.DictionaryKeyNamingPolicy` can opt string-key dictionaries into key transformation.

Current limitations:

* Constructor-parameter materialization is not implemented yet.
* Read-only properties are not populated unless a future converter adds explicit support.
* Immutable or constructor-only collections are not implemented yet.
* Non-string dictionary keys are not implemented yet.
* Polymorphic object shapes are still pending.

## Stream APIs

Nerdbank.Json now exposes synchronous and asynchronous stream overloads for serializer entry points.

The current implementation buffers the full JSON payload in memory before writing to or reading from the stream. This keeps the public surface moving forward while the lower-level incremental streaming model is still being built.

## Behavioral Notes

* `DateTime` and `DateTimeOffset` use round-trip `O` formatting.
* `TimeSpan` uses constant `c` formatting.
* Byte buffers serialize as Base64 JSON strings.
* `Point` serializes as a two-element JSON array.
* String escaping is intentionally minimal and standards-based.

## Current Scope

This phase does not yet include the richer constructor-aware contract system, immutable collection breadth, or incremental streaming pipeline planned for later work.
