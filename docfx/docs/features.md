# Features

Nerdbank.Json provides UTF-8 JSON serialization through PolyType contracts, custom converters, and low-level readers and writers.
Its implemented features include constructor-bound objects, mutable and parameterized collections, incremental streaming, reference preservation, unions, targeted deserialization, and JSON Schema generation.

## Low-Level UTF-8 Writer

The low-level writer writes UTF-8 directly to `IBufferWriter<byte>`, which keeps the core serializer independent from string-building intermediate layers.

Key traits:

* Direct UTF-8 emission.
* Optional pretty-printed output with <xref:Nerdbank.Json.JsonSerializer.WriteIndented>.
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

The PolyType-based contract layer supports mutable objects as well as records and immutable objects whose constructor parameters map to serializable properties.

Current behavior:

* Mutable object types with settable properties can be serialized and deserialized.
* Types with parameterized constructors can be materialized when constructor parameters map to serializable properties.
* Nested objects compose through the same converter cache.
* Enumerable and dictionary shapes support both mutable and parameterized construction, including read-only and immutable collections when their shapes supply construction support.
* Mutable collections use their shape-provided constructor and appender; parameterized collections buffer elements and then invoke their shape-provided constructor.
* Dictionary keys must be supported simple scalar or enum types.
* Getter-only mutable collection and dictionary properties are populated into their existing instances during deserialization.
* Rectangular multidimensional arrays (rank 2 and higher) use nested JSON arrays by default, or `MultiDimensionalArrayFormat.Flat` for `[[dimension lengths], [flat elements]]` through <xref:Nerdbank.Json.JsonSerializer.MultiDimensionalArrayFormat>.
* Unknown JSON properties are ignored during deserialization unless a settable <xref:Nerdbank.Json.UnusedDataPacket> member captures them.
* Property names default to camelCase.
* `JsonSerializer.PropertyNamingPolicy` can be set to `null` or another `JsonNamingPolicy` built-in.
* `JsonSerializer.WriteIndented` can emit human-readable JSON with line breaks and indentation.
* `JsonSerializer.PropertyNameCaseInsensitive` can relax object-property matching during deserialization.
* `PropertyShapeAttribute.Name` overrides the naming policy when explicitly set.
* Dictionary keys are not transformed by the property naming policy by default.
* `JsonSerializer.DictionaryKeyNamingPolicy` can opt string-key dictionaries into key transformation.
* `JsonSerializer.Converters` can register concrete runtime converters for exact target types.
* `JsonSerializer.ConverterTypes` can register converter classes, including open generic converter types.
* `JsonSerializer.ConverterFactories` can register runtime converter factories for type-driven interception.
* `JsonConverterAttribute` can attach custom converters directly to types, properties, and constructor parameters.
* A field or property of type <xref:Nerdbank.Json.UnusedDataPacket> captures unknown object members as raw JSON for forward-compatible round-tripping; private members can be exposed with `PropertyShapeAttribute`.
* Types implementing `IJsonSerializationCallbacks` can validate or prepare state before serialization and restore invariants after deserialization.
* <xref:Nerdbank.Json.UseComparerAttribute> selects a comparer type or provider property for a dictionary or set member so the deserializer preserves member-specific semantics such as case-insensitive keys.
* <xref:Nerdbank.Json.JsonSerializer.SerializeDefaultValues> can omit default-valued properties, honoring optional constructor defaults and <xref:System.ComponentModel.DefaultValueAttribute> before falling back to the CLR default.
* `JsonSerializer.SerializeEnumValuesByName` can serialize enums as strings when simple names exist.
* `JsonSerializer.DeserializeDefaultValues` can relax required-member and non-nullable reference enforcement during deserialization.
* `JsonSerializer.InternStrings` can reuse equal string instances within one deserialization operation.
* <xref:Nerdbank.Json.JsonSerializer.PreserveReferences> can preserve repeated references and optionally allow cycles through mutable objects and collections.
* `JsonSerializer.AllowTrailingCommas` can accept trailing commas while reading arrays and objects.
* `JsonSerializer.ReadCommentHandling` can skip `//` and `/* */` comments while deserializing.
* Closed unions declared with `DerivedTypeShapeAttribute` serialize as two-element arrays containing a discriminator and payload.
* <xref:Nerdbank.Json.JsonSerializer.DerivedTypeUnions> can add, replace, extend, or disable unions at runtime using explicitly supplied type shapes, and can opt into an experimental discriminator-free duck-typing strategy.
* `JsonSerializer.GetJsonSchema` exports a `System.Text.Json.Nodes.JsonObject` for draft 4, draft 7, or draft 2020-12, describing the effective representation of a type.
* <xref:Nerdbank.Json.JsonSerializer.DeserializePath*> can deserialize only a single value selected by a <xref:Nerdbank.Json.JsonPath> or typed path options, skipping unrelated JSON.

Converter registration notes:

* Runtime converter instances take precedence over runtime converter types.
* Runtime converter types take precedence over runtime converter factories.
* Runtime converter factories take precedence over built-in and PolyType-generated converters.
* Attribute-driven converters on a member or constructor parameter override the default converter selection for that specific serialized slot.
* Attribute-driven converters on a type override built-in and PolyType-generated conversion for that type.
* Open generic converter types are matched against the open generic target type definition and activated for the closed target type being serialized.

Property validation notes:

* Non-nullable reference-type properties reject explicit JSON `null` values during deserialization.
* Getter-only mutable collection properties ignore JSON `null` instead of replacing their existing collection instances.
* Property-name case insensitivity only affects object-property and constructor-parameter matching during deserialization; serialization still uses the configured emitted names.
* <xref:Nerdbank.Json.UnusedDataPacket> retains raw JSON text, so unknown members round-trip without being reinterpreted through another converter.
Getter-only packet members can write supplied packets but do not capture unknown input.

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
* `ReferencePreservationMode.RejectCycles` preserves repeated references and rejects reference cycles.
* `ReferencePreservationMode.AllowCycles` additionally allows reference cycles by registering mutable objects and collections before their members are populated; immutable or constructor-bound objects cannot be back-referenced while under construction.

String interning notes:

* Enable with `new JsonSerializer { InternStrings = true }`; the default is `false`.
* Built-in string conversion decodes into temporary stack or pooled character storage before looking up the value. Repeated values do not allocate candidate strings, even when JSON escape sequences are used.
* Different spellings of the same value, such as `"hello"` and `"h\u0065llo"`, share a string instance within the operation.
* The cache is operation-scoped, not the CLR's global intern pool. Strings are not retained for reuse across separate deserializations, and serialization output is unchanged.

Current limitations:

* Getter-only scalar and immutable properties are not populated unless bound to constructor parameters.
* Collections whose shapes provide no construction strategy can be serialized but not deserialized.
* Parameterized collection construction buffers the collection's elements, even when its JSON input is read incrementally.
* Dictionary keys are intentionally limited to simple scalar and enum-like types rather than arbitrary object graphs.
* Delegate types are not supported.

## Stream APIs

Nerdbank.Json exposes synchronous stream overloads plus incremental asynchronous stream and pipe overloads through <xref:Nerdbank.Json.JsonSerializer.SerializeAsync*> and <xref:Nerdbank.Json.JsonSerializer.DeserializeAsync*>.
These use <xref:System.IO.Pipelines.PipeReader> and <xref:System.IO.Pipelines.PipeWriter>, apply backpressure, honor cancellation, and leave caller-supplied streams and pipes open.
Built-in object and collection converters process members and elements incrementally, including constructor-bound objects.
Custom converters using the default asynchronous hooks buffer their individual values rather than streaming their contents.
See [Asynchronous streaming](async-streaming.md).

## Sequence Streaming and Targeted Deserialization

* <xref:Nerdbank.Json.JsonSerializer.DeserializeArrayAsync*> streams a top-level JSON array as an asynchronous sequence.
* <xref:Nerdbank.Json.JsonSerializer.DeserializeEnumerableAsync*> streams values using <xref:Nerdbank.Json.JsonSerializer.SequenceOptions>, with <xref:Nerdbank.Json.JsonSequenceFormat.NDJson> or <xref:Nerdbank.Json.JsonSequenceFormat.JsonArray> framing.
* <xref:Nerdbank.Json.JsonSerializer.DeserializePathEnumerableAsync*> streams an array selected inside an envelope by JSON path or typed <xref:Nerdbank.Json.JsonSerializer.StreamingEnumerationOptions`2>.
* <xref:Nerdbank.Json.JsonSerializer.SerializeArrayAsync*> and <xref:Nerdbank.Json.JsonSerializer.SerializeEnumerableAsync*> write asynchronous sequences with backpressure.
* <xref:Nerdbank.Json.JsonSerializer.DeserializePath*> reads only a selected value, skipping unrelated JSON.
* Custom converters support logical properties and indices through `SkipToPropertyValue`, `SkipToIndexValue`, and their asynchronous counterparts.

NDJSON requires a line terminator between values.
Typed streaming options control caller-supplied pipe ownership through `LeaveOpen`.
Targeted deserialization and sequence streaming do not support reference preservation.
Completing a sequence validates its remaining framing; disposing the enumerator early does not drain or validate the remaining document.
See [Asynchronous sequence streaming](async-sequences.md) and [Targeted deserialization](targeted-deserialization.md).

## Untyped JSON

Nerdbank.Json includes an opt-in native JSON document object model, <xref:Nerdbank.Json.JsonValue>, plus opt-in converters for `object`, <xref:System.Dynamic.ExpandoObject>, and the System.Text.Json DOM types.
The default serializer does not root these converters; source-generated shapes avoid reflection-based contract discovery.
See [Untyped JSON and DOM converters](untyped-json.md).

## ASP.NET Core MVC

The separate `Nerdbank.Json.AspNetCoreMvcFormatter` package provides MVC input and output formatters backed by Nerdbank.Json and an explicitly supplied, source-generated `ITypeShapeProvider`, for AOT-safe JSON in controller-based Web APIs.
See [ASP.NET Core MVC formatters](aspnetcore-mvc.md).

## Behavioral Notes

* `DateTime` and `DateTimeOffset` use round-trip `O` formatting.
* `TimeSpan` uses constant `c` formatting.
* Byte buffers serialize as Base64 JSON strings.
* `Point` serializes as a two-element JSON array.
* String escaping is intentionally minimal and standards-based.

## Current Scope

The serializer supports mutable and constructor-bound contracts, shape-supported parameterized collections, and incremental asynchronous streaming today.
Collection deserialization depends on the construction strategy supplied by the type shape; arbitrary dictionary object keys and delegates remain unsupported.
API parity with Nerdbank.MessagePack is ongoing, with JSON-specific representation and configuration retained.
