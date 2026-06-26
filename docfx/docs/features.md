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
* Types with parameterized constructors can be materialized when constructor parameters map to serializable properties.
* Nested objects compose through the same converter cache.
* Mutable `ICollection<T>` implementations with public parameterless constructors can be serialized and deserialized.
* Mutable `IDictionary<TKey, TValue>` implementations with public parameterless constructors can be serialized and deserialized when `TKey` is a supported simple key type.
* Getter-only mutable collection and dictionary properties are populated into their existing instances during deserialization.
* Unknown JSON properties are ignored during deserialization.
* Property names default to camelCase.
* `JsonSerializer.PropertyNamingPolicy` can be set to `null` or another `JsonNamingPolicy` built-in.
* `PropertyShapeAttribute.Name` overrides the naming policy when explicitly set.
* Dictionary keys are not transformed by the property naming policy by default.
* `JsonSerializer.DictionaryKeyNamingPolicy` can opt string-key dictionaries into key transformation.
* `JsonSerializer.SerializeDefaultValues` can omit default-valued properties during serialization.
* `JsonSerializer.SerializeEnumValuesByName` can serialize enums as strings when simple names exist.
* `JsonSerializer.DeserializeDefaultValues` can relax required-member and non-nullable reference enforcement during deserialization.
* `JsonSerializer.PreserveReferences` can preserve repeated references in acyclic object graphs.
* Closed unions declared with `DerivedTypeShapeAttribute` serialize as two-element arrays containing a discriminator and payload.

Property validation notes:

* Non-nullable reference-type properties reject explicit JSON `null` values during deserialization.
* Getter-only mutable collection properties ignore JSON `null` instead of replacing their existing collection instances.

Constructor deserialization notes:

* Required constructor parameters must appear in the JSON payload.
* Duplicate JSON assignments to the same constructor parameter are rejected.
* Optional constructor parameters can continue to rely on their declared default values.
* `JsonSerializer.DeserializeDefaultValues` can allow missing required values or `null` assignments to non-nullable reference members when compatibility is more important than strict contract validation.

Enum notes:

* `JsonSerializer.SerializeEnumValuesByName` writes enum names as JSON strings when a simple declared name exists for the value.
* Unnamed enum values continue to serialize numerically.
* String enum names are deserialized case-insensitively unless the enum defines distinct names that differ only by case.

Reference preservation notes:

* When `JsonSerializer.PreserveReferences` is enabled, reference-typed values are wrapped in JSON metadata objects using `$id`, `$ref`, and `$value`.
* Reference cycles are rejected.

Current limitations:

* Read-only scalar and immutable properties are not populated unless a future converter adds explicit support.
* Immutable or constructor-only collections are not implemented yet.
* Dictionary keys are intentionally limited to simple scalar and enum-like types rather than arbitrary object graphs.
* Delegate types are not supported.

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

This phase does not yet include immutable collection breadth, a richer contract-customization system, or an incremental streaming pipeline.
