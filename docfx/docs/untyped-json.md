# Untyped JSON and DOM converters

Nerdbank.Json can process JSON without a static model. The centerpiece is a native,
dependency-free document object model — <xref:Nerdbank.Json.JsonValue> — plus a set of
**opt-in** converters for the CLR <xref:System.Object> and
<xref:System.Dynamic.ExpandoObject> types and the `System.Text.Json` DOM types.

Everything here is opt-in. If you never call the extension methods below, the default
serializer roots no additional dependencies, never touches `System.Text.Json`, and uses no
reflection, so it stays trimming- and NativeAOT-safe.

[!code-csharp[](../../samples/cs/UntypedJson.cs#UntypedJson)]

## The native `JsonValue` DOM

<xref:Nerdbank.Json.JsonValue> is an immutable hierarchy:

* <xref:Nerdbank.Json.JsonObject> — an ordered, duplicate-checked set of members with O(1)
  name lookup.
* <xref:Nerdbank.Json.JsonArray> — an immutable `IReadOnlyList<JsonValue>`.
* <xref:Nerdbank.Json.JsonString>, <xref:Nerdbank.Json.JsonBoolean>, <xref:Nerdbank.Json.JsonNull>.
* <xref:Nerdbank.Json.JsonNumber> — stores the **exact** JSON number token (`RawToken`) so
  values such as `1.0`, `1e10`, `-0`, and integers beyond `long` round-trip byte-for-byte.
  Convenience accessors (`GetInt64`, `GetDouble`, `GetDecimal`, and their `Try*` forms) parse
  on demand.

Parse and write it with the dedicated methods on <xref:Nerdbank.Json.JsonSerializer>:
`DeserializeJsonValue` / `SerializeJsonValue` (string, UTF-8 bytes, `ReadOnlySequence<byte>`,
or `Stream`) and their async counterparts `DeserializeJsonValueAsync` /
`SerializeJsonValueAsync` (`Stream` or `PipeReader`/`PipeWriter`). These require no generated
shape at all. Object and array reads are incremental on the async path.

Reads enforce the configured <xref:Nerdbank.Json.SerializationContext.MaxDepth>, the new
<xref:Nerdbank.Json.SecuritySettings.MaxObjectMemberCount> property-count limit, duplicate
property rejection, and honor `AllowTrailingCommas` and `ReadCommentHandling`.

## `object` and `ExpandoObject`

Call <xref:Nerdbank.Json.OptionalConverters.WithObjectConverter*> to register the converter for <xref:System.Object>.
It reads JSON into boxed primitives, read-only dictionaries with object keys containing strings, and object arrays.
Non-negative integer tokens become <xref:System.UInt64>, negative integers become <xref:System.Int64>, and fractional or exponent-form numbers become <xref:System.Double>.
Numbers outside those ranges fail explicitly; use the native DOM when exact number-token fidelity is required.

Runtime-shaped objects can be written through an `object` member when their shape is available from <xref:Nerdbank.Json.SerializationContext.TypeShapeProvider>.
No arbitrary reflection-based discovery is performed.
The converter also writes its deserialized dictionary and array representations, native DOM values, and boxed JSON primitives.

Use <xref:Nerdbank.Json.OptionalConverters.WithDynamicObjectConverter*> instead when dictionaries should also support read-only C# dynamic member access.
Both registrations target the same `object` type and cannot be combined on one serializer.

Call <xref:Nerdbank.Json.OptionalConverters.WithExpandoObjectConverter*> separately to enable <xref:System.Dynamic.ExpandoObject>.
Its member values use the same primitives and arrays, with nested objects represented by read-only dynamic dictionaries.
Reconstructing an expando is **O(n²)** because each insertion scans its existing members.
The <xref:Nerdbank.Json.SecuritySettings.MaxObjectMemberCount> limit bounds the property count on both reads and writes.

Call <xref:Nerdbank.Json.OptionalConverters.WithJsonValueConverter*> to register converters for shaped members declared as <xref:Nerdbank.Json.JsonValue> or any of its concrete subclasses.
This preserves the native hierarchy and exact number tokens independently of the object and expando opt-ins.
Each registration returns an immutable serializer copy and rejects duplicate converter registrations.

## System.Text.Json interop

Call <xref:Nerdbank.Json.OptionalConverters.WithSystemTextJsonConverters*> to register converters
for <xref:System.Text.Json.JsonElement>, <xref:System.Text.Json.Nodes.JsonNode>, and
<xref:System.Text.Json.JsonDocument>. They bridge by round-tripping raw JSON text, so they add
no reflection — but calling the method roots the `System.Text.Json` assembly. Because the
converters are registered only when you ask for them, the default serializer keeps
`System.Text.Json` out of the trimmed/NativeAOT closure.

## NativeAOT

The native `JsonValue` DOM and the `object`/`ExpandoObject` converters are fully NativeAOT- and
trimming-safe. The `System.Text.Json` interop opt-in is safe to use under NativeAOT as well; it
simply brings `System.Text.Json` into the app when enabled.
