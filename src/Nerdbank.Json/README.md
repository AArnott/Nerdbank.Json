# Nerdbank.Json

Nerdbank.Json is a UTF-8 JSON serializer being built in the same general shape as Nerdbank.MessagePack, but without layering on top of System.Text.Json, Newtonsoft.Json, or any other serializer.

The implementation includes low-level UTF-8 readers and writers, built-in converters for common .NET types, and PolyType-backed serialization for mutable and constructor-bound object graphs.
It is allocation-conscious in the hot path and multi-targets `net8.0`, `net9.0`, `net472`, and `netstandard2.0`.

## Features

* Direct UTF-8 JSON writing to `IBufferWriter<byte>`.
* RFC 8259 string escaping, limited to the characters that must be escaped.
* Built-in serialization and deserialization for common .NET primitive and BCL types.
* PolyType-driven serialization for mutable objects, records, and immutable constructor-bound contracts.
* Mutable, read-only, and immutable collections when their shapes supply a construction strategy.
* Dictionaries with supported scalar or enum keys, and member-specific comparers through `UseComparerAttribute`.
* Rectangular multidimensional arrays with nested or flat dimensions/data representations.
* camelCase property naming by default, configurable with `JsonNamingPolicy`.
* Optional indented output for human-readable JSON.
* Optional case-insensitive property-name matching during deserialization.
* Optional enum-name serialization with numeric fallback for unnamed values.
* Runtime custom converter registration with converter instances, converter types, or converter factories.
* Attribute-driven custom converter registration on types, properties, and constructor parameters.
* Optional unknown-member retention through a type-recognized `UnusedDataPacket` field or property.
* Built-in byte buffer handling using Base64 JSON strings.
* Optional trailing-comma and comment skipping support during deserialization.
* Configurable deserialization policy for missing required values and non-nullable reference validation.
* Reference preservation for shared instances, with optional cycles through mutable objects and collections, using JSON `$id`, `$ref`, and `$value` metadata.
* Synchronous stream and incremental asynchronous stream and pipe overloads.
* Sequence streaming through `DeserializeEnumerableAsync` and `SerializeEnumerableAsync`, with NDJSON or JSON-array framing selected by `JsonSerializer.SequenceOptions`.
* Typed and raw JSON paths through `DeserializePath` and `DeserializePathEnumerableAsync`.
* Generated and runtime unions configured through `DerivedTypeUnions` and `DerivedShapeMapping<T>`.
* JSON Schema export as a mutable `System.Text.Json.Nodes.JsonObject`.
* Opt-in `JsonValue`, object, dynamic, expando, and System.Text.Json DOM converters through `OptionalConverters` extensions.
* Multi-targeting across modern .NET and .NET Framework.
* Test-driven development with focused round-trip coverage for the current built-in type surface.

## Supported Built-In Types

The current built-in serializer supports:

* `string`, `char`, `bool`
* Numeric primitives: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`
* `System.Numerics.BigInteger`
* `DateTime`, `DateTimeOffset`, `TimeSpan`
* `Guid`, `Version`, `Uri`, `CultureInfo`, `Encoding`
* `byte[]`, `Memory<byte>`, `ReadOnlyMemory<byte>`
* `System.Drawing.Color`, `System.Drawing.Point`
* On .NET 8 and later: `Half`, `Int128`, `UInt128`, `DateOnly`, `TimeOnly`, `Rune`

## Usage

Annotate a partial root contract with PolyType's `GenerateShape` attribute and use `JsonSerializer.Serialize` and `Deserialize` with its generated shape.
`Serialize` returns a string by default; buffer-writer, stream, and pipe overloads support direct UTF-8 output.
See the [getting-started sample](https://github.com/AArnott/Nerdbank.Json/blob/main/samples/cs/GettingStarted.cs) and [usage guide](https://aarnott.github.io/Nerdbank.Json/docs/getting-started.html).

Object property names default to camelCase.
Set `PropertyNamingPolicy` to `null` to preserve declared names, or choose another `JsonNamingPolicy`.
Dictionary keys remain unchanged unless `DictionaryKeyNamingPolicy` is set separately.
`AllowTrailingCommas` and `ReadCommentHandling` optionally accept human-authored JSON.

Register converter instances, types, or factories through `Converters`, `ConverterTypes`, and `ConverterFactories`.
`ConverterTypeCollection` supports collection expressions and an explicit `Create` builder.
Factories resolve supporting converters through `ConverterContext`; converters processing nested values use `SerializationContext.GetConverter` instead of calling the top-level serializer.
See the [custom-converter samples](https://github.com/AArnott/Nerdbank.Json/blob/main/samples/cs/CustomConverters.cs).

To retain unknown members, declare a field or property of type `UnusedDataPacket`.
For a private member, include it in the shape with PolyType's `PropertyShape` attribute.
Retained payloads are re-emitted as raw JSON without introducing a property for the packet itself.
See [retaining unknown members](https://aarnott.github.io/Nerdbank.Json/docs/features.html#polytype-object-graphs).

`SerializeDefaultValues` honors optional constructor defaults and `DefaultValueAttribute` when deciding whether to omit a property.
The [feature guide](https://aarnott.github.io/Nerdbank.Json/docs/features.html) describes supported contracts and limitations.
