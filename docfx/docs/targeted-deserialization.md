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

## Navigating through unions and custom representations

Expression overloads can traverse *through* intermediate values whose JSON representation is not a
plain object or array, because the parser records the converter for each segment's container and the
navigator asks that converter how to descend. This works out of the box for:

* **Generated unions** and **runtime-configured unions**, whose `[discriminator, payload]` envelope
  is unwrapped so navigation continues into the selected case's payload (base members declared on the
  union's base type are addressable this way).
* **Duck-typing unions**, whose payload is already a plain object.

Custom converters that use a non-standard representation participate by overriding
`JsonConverter.TryNavigate`. The default implementation navigates a self-describing object or array,
so most converters need do nothing. A converter that remaps logical members (for example, one that
writes a value as a positional array) overrides the hook to translate a
<xref:Nerdbank.Json.JsonNavigationSegment> into a navigation over its own representation, honoring the
supplied <xref:Nerdbank.Json.JsonNavigationOptions> name comparer:

[!code-csharp[](../../samples/cs/TargetedDeserialization.cs#NavigationHook)]

Pre-parsed <xref:Nerdbank.Json.JsonPath> navigation uses raw JSON tokens only. It still targets any
type, and it traverses plain objects and arrays, but it cannot descend *through* a union envelope or
a remapping converter (the wire alone does not carry that metadata). In that case a clear
<xref:System.NotSupportedException> is thrown that points you to the expression-based overload, which
supplies the converter metadata required to traverse the value.

## Limitations

* Accessing a member declared only on a derived union case (via a cast in the expression) is not
  supported; only members visible on the static type of each segment are addressable.
* Targeted deserialization is not supported when
  <xref:Nerdbank.Json.JsonSerializer.PreserveReferences> is enabled, because reference metadata
  wraps every value; a clear <xref:System.NotSupportedException> is thrown.
