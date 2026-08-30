# Targeted deserialization

Targeted deserialization reads only a single value selected by a path from a JSON document,
skipping everything else. This avoids materializing an entire object graph when you need just
one property, array element, or dictionary entry.

[!code-csharp[](../../samples/cs/TargetedDeserialization.cs#TargetedDeserialization)]

## Two ways to specify a path

* **Expression overloads** (`w => w.Location.City`) are ergonomic. The expression is *parsed but
  never compiled*, so they remain NativeAOT-safe. CLR member names are translated to serialized
  names using the configured <xref:Nerdbank.Json.JsonSerializer.PropertyNamingPolicy> and
  <xref:Nerdbank.Json.JsonSerializer.DictionaryKeyNamingPolicy>. Array and dictionary indices must
  be compile-time constants; unsupported expressions (method calls, dynamic indices) are rejected
  before any JSON is read.
* **Pre-parsed <xref:Nerdbank.Json.JsonPath>** is a low-level, immutable path built from the
  *serialized* (wire) names. Build it once and reuse it across many calls to avoid repeated
  analysis on hot code paths.

## Behavior

* The selected value is deserialized through the full converter graph, so custom converters, union
  envelopes, and naming policies remain correct for the target subtree.
* Navigation honors the serializer's case-insensitivity, comment handling, and trailing-comma
  settings, and works over multi-segment (`ReadOnlySequence<byte>`) input.
* A missing path is controlled by <xref:Nerdbank.Json.MissingPathBehavior>: throw a
  <xref:Nerdbank.Json.JsonPathNotFoundException> (the default) or return the target's default value.
  An intermediate `null` is treated as a missing path.

## Limitations

* Path segments navigate plain JSON objects and arrays. Navigating *through* a value with a
  non-standard representation (a union envelope, or a custom converter that does not emit an object
  or array) is not supported; the target of the path may still be any type.
* Targeted deserialization is not supported when
  <xref:Nerdbank.Json.JsonSerializer.PreserveReferences> is enabled, because reference metadata
  wraps every value; a clear <xref:System.NotSupportedException> is thrown.
