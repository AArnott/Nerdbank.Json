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

Call <xref:Nerdbank.Json.JsonSerializer.WithUntypedConverters*> to register converters for
`object`, `ExpandoObject`, and `JsonValue` members:

* A value typed as `object` **deserializes to a boxed <xref:Nerdbank.Json.JsonValue>**, giving
  full JSON fidelity with no runtime type discovery.
* Serializing an `object` accepts a `JsonValue` or a boxed JSON primitive (`bool`, `string`, or
  any built-in numeric type). Any other CLR type throws a
  <xref:System.NotSupportedException> instead of silently walking an arbitrary object graph via
  reflection. Reflection-based serialization of arbitrary runtime types is intentionally **not**
  provided; serialize such values through their own generated type shape.
* `ExpandoObject` round-trips as a JSON object whose member values are `JsonValue` instances.
  Because `ExpandoObject` stores members in a way that requires a linear scan per insertion,
  reconstructing one from JSON is **O(n²)**; prefer `JsonObject` for large payloads. The
  <xref:Nerdbank.Json.SecuritySettings.MaxObjectMemberCount> limit bounds the worst case.

## System.Text.Json interop

Call <xref:Nerdbank.Json.JsonSerializer.WithSystemTextJsonConverters*> to register converters
for <xref:System.Text.Json.JsonElement>, <xref:System.Text.Json.Nodes.JsonNode>, and
<xref:System.Text.Json.JsonDocument>. They bridge by round-tripping raw JSON text, so they add
no reflection — but calling the method roots the `System.Text.Json` assembly. Because the
converters are registered only when you ask for them, the default serializer keeps
`System.Text.Json` out of the trimmed/NativeAOT closure.

## NativeAOT

The native `JsonValue` DOM and the `object`/`ExpandoObject` converters are fully NativeAOT- and
trimming-safe. The `System.Text.Json` interop opt-in is safe to use under NativeAOT as well; it
simply brings `System.Text.Json` into the app when enabled.
