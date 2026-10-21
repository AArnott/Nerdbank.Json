# Custom converters

While using <xref:PolyType.GenerateShapeAttribute> is the simplest way to make an object graph serializable, some types need custom handling.
In such cases, you can define and register your own <xref:Nerdbank.Json.JsonConverter`1>.

## Define your own converter

Consider class `Foo` that cannot be serialized automatically.

Declare a class that derives from <xref:Nerdbank.Json.JsonConverter`1>:

[!code-csharp[](../../samples/cs/CustomConverters.cs#YourOwnConverter)]

Use the scalar <xref:Nerdbank.Json.JsonWriter.Write*> overloads to write strings, numbers, and booleans, and <xref:Nerdbank.Json.JsonWriter.WriteRaw*> for already encoded JSON.
JSON container methods such as <xref:Nerdbank.Json.JsonWriter.WriteStartObject> remain explicit.
<xref:Nerdbank.Json.JsonReader> exposes typed scalar methods such as <xref:Nerdbank.Json.JsonReader.ReadInt32> as well as structural and raw-token methods.

> [!CAUTION]
> It is imperative that each `Write` and `Read` method write and read exactly one JSON value.

If you have more than one field to serialize, wrap them in one JSON object or one JSON array.
If the value may be `null`, handle that explicitly before reading or writing the surrounding structure.

## Borrowing and retaining raw JSON

Use <xref:Nerdbank.Json.RawJson> for a model member whose JSON should be decoded separately from its envelope.
The built-in converter is automatic, preserves exact numeric and string token spelling, and always returns owned storage.
No DOM registration is necessary.

[!code-csharp[](../../samples/cs/CustomConverters.cs#DeferredRawJson)]

<xref:Nerdbank.Json.JsonReader.ReadRaw(Nerdbank.Json.SerializationContext)?displayProperty=nameWithType> validates and skips one value using the active depth and cancellation limits.
Sequence-backed readers return a zero-copy borrowed slice; span-backed readers copy only the captured value into owned storage.
Borrowed slices are valid only while their input buffers remain alive and unchanged.
Call <xref:Nerdbank.Json.RawJson.ToOwned?displayProperty=nameWithType> before returning a pipe buffer or recycling application-owned memory.
The method retains its owned copy when called on a mutable instance, so subsequent calls do not copy again.

<xref:Nerdbank.Json.JsonWriter.Write(Nerdbank.Json.RawJson)?displayProperty=nameWithType> and the built-in converter validate exactly one strict JSON value before writing its UTF-8 bytes directly.
This does not normalize numbers, decode and re-escape strings, or apply indentation within the raw payload.
Comments, trailing commas, multiple values, and empty or default payloads are rejected.
For explicitly unvalidated output, use <xref:Nerdbank.Json.JsonWriter.WriteRaw*> only with already trusted JSON.

Sequence-backed <xref:Nerdbank.Json.JsonReader> instances parse the original segments rather than flattening the document.
Only individual scalar tokens that cross segment boundaries may need temporary GC-owned storage.
Peeking copies reader position, not input ownership; advancing a peek reader does not advance the original.

## UTF-8 strings and token inspection

<xref:Nerdbank.Json.JsonReader.NextJsonType?displayProperty=nameWithType> uses the same <xref:Nerdbank.Json.JsonValueKind> vocabulary as the DOM.
<xref:Nerdbank.Json.JsonReader.NextCode?displayProperty=nameWithType> exposes the next significant UTF-8 byte, while <xref:Nerdbank.Json.JsonReader.IsNull?displayProperty=nameWithType> and <xref:Nerdbank.Json.JsonReader.ReadNull?displayProperty=nameWithType> support explicit null handling.
Inspection skips permitted insignificant input but does not validate an entire value.

<xref:Nerdbank.Json.JsonReader.TryReadStringSpan*> returns a borrowed, decoded UTF-8 view only for an unescaped, contiguous string.
It returns false without advancing for null, escaped, or segmented strings; malformed strings still throw.
Use <xref:Nerdbank.Json.JsonReader.ReadStringSequence?displayProperty=nameWithType> to handle all nullable strings, or <xref:Nerdbank.Json.JsonReader.ReadStringSpan?displayProperty=nameWithType> when null is not allowed.
Both return decoded content, never JSON escape text, and allocate token-scoped storage only when necessary.
Unescaped sequence slices retain the input's borrowed lifetime.

<xref:Nerdbank.Json.JsonWriter.WriteString*> accepts decoded UTF-8 bytes, validates them, and applies JSON escaping directly without an intermediate UTF-16 string.
It accepts sequences even when a multi-byte character crosses a segment boundary.
Do not pass an already-quoted or already-escaped JSON token to this method; use raw-value APIs for that contract.

Reader and writer `Clone` methods preserve syntax or formatting options but start at the root of their new input or destination.
They do not inherit container state.
There is intentionally no public writer `GetSpan`/`Advance` pair: arbitrary fragments cannot safely maintain JSON container and pending-property state.
Use <xref:Nerdbank.Json.JsonWriter.WriteRaw*> for trusted, complete JSON values.

## Security considerations

Any custom converter that reads or writes nested JSON structures should call <xref:Nerdbank.Json.SerializationContext.DepthStep?displayProperty=nameWithType> on the provided <xref:Nerdbank.Json.SerializationContext> before processing the structure.

This is important to prevent maliciously crafted payloads from causing a stack overflow or other denial-of-service issue.
It also ensures the converter honors <xref:Nerdbank.Json.SerializationContext.CancellationToken?displayProperty=nameWithType>, because `DepthStep()` performs the cancellation check.

Applications that have a legitimate need to exceed the default nesting limit can adjust <xref:Nerdbank.Json.SerializationContext.MaxDepth?displayProperty=nameWithType>.

## Delegating to sub-values

The <xref:Nerdbank.Json.SerializationContext.GetConverter*> methods may be used to obtain converters for members of the type your converter is serializing or deserializing.

[!code-csharp[](../../samples/cs/CustomConverters.cs#DelegateSubValues)]

If the nested type does not have a directly generated shape, you can use a witness type decorated with <xref:PolyType.GenerateShapeForAttribute`1> and request the converter using that shape.

## Sharing state with converters

The indexer on <xref:Nerdbank.Json.SerializationContext?displayProperty=nameWithType> lets applications pass custom state to converters. Configure it on the serializer's starting context:

```csharp
private static readonly object TenantKey = new();

SerializationContext context = serializer.StartingContext;
context[TenantKey] = tenant;
serializer = serializer with { StartingContext = context };
```

A converter can retrieve the value with `context[TenantKey]`. Use a unique `static readonly object` key to avoid collisions with other state; assigning `null` removes the key. The context stores this state immutably, so changing a copied context does not mutate the original.

### Per-call state

When the state varies from one call to the next (for example, per request or per tenant), pass a <xref:Nerdbank.Json.SerializationContext> directly to an overload that accepts one instead of creating a new serializer for each call:

[!code-csharp[](../../samples/cs/PerCallState.cs#PerCallState)]

The converter reads the state from the context it is given:

[!code-csharp[](../../samples/cs/PerCallState.cs#PerCallStateConverter)]

Overloads that accept a <xref:Nerdbank.Json.SerializationContext> are available for `Serialize`, `Deserialize`, `SerializeObject`, and `DeserializeObject` on <xref:Nerdbank.Json.JsonWriter> and <xref:Nerdbank.Json.JsonReader>, and for their asynchronous counterparts on pipes and streams.
Keep these points in mind:

- The supplied context is used *instead of* <xref:Nerdbank.Json.JsonSerializer.StartingContext?displayProperty=nameWithType>. Its state is not merged with the serializer's, so start from `serializer.StartingContext` (as the sample does) when you want to keep the serializer's settings and state.
- The operation is canceled by the context's <xref:Nerdbank.Json.SerializationContext.CancellationToken?displayProperty=nameWithType>, so these overloads take no separate `CancellationToken` parameter.
- Never pass the context a converter receives to a top-level serializer method. Doing so throws an <xref:System.ArgumentException>. Use <xref:Nerdbank.Json.SerializationContext.GetConverter*> to (de)serialize nested values instead.

## Register your custom converter

There are three main ways to get Nerdbank.Json to use your converter.

### Attribute approach

To use your converter wherever a type appears, apply <xref:Nerdbank.Json.JsonConverterAttribute> to the type itself:

[!code-csharp[](../../samples/cs/CustomConverters.cs#CustomConverterByAttribute)]

The converter's generic argument must match the attributed type; this example applies `FooConverter : JsonConverter<Foo>` to `Foo` itself.
The attribute automatically asks PolyType to generate the associated converter constructor, so no separate <xref:PolyType.AssociatedTypeShapeAttribute> is required for attributed converters.
Open generic converter types are supported on generic model types; generate shapes for the closed models through a witness type so their closed converter constructors are available under NativeAOT.

You may also apply <xref:Nerdbank.Json.JsonConverterAttribute> to a property or constructor parameter to affect only that serialized slot:

[!code-csharp[](../../samples/cs/CustomConverters.cs#CustomConverterByAttributeOnMember)]

### Runtime registration

For precise runtime control, register a converter instance with <xref:Nerdbank.Json.JsonSerializer.Converters?displayProperty=nameWithType>:

[!code-csharp[](../../samples/cs/CustomConverters.cs#CustomConverterRegisteredAtRuntime)]

Open generic converter types may be registered with <xref:Nerdbank.Json.JsonSerializer.ConverterTypes?displayProperty=nameWithType>:

[!code-csharp[](../../samples/cs/CustomConverters.cs#CustomOpenGenericConverterRegisteredAtRuntime)]

<xref:Nerdbank.Json.ConverterTypeCollection> is immutable and supports collection expressions through its collection builder, as well as explicit calls to <xref:Nerdbank.Json.ConverterTypeCollection.Create*>.
For runtime registration without a converter attribute, associate an open generic converter with its model using <xref:PolyType.AssociatedTypeShapeAttribute>, and generate a shape for the closed model through a witness type.
The `Envelope<T>` sample uses this pattern; applying `GenerateShapeAttribute` directly to an open generic model is not supported by PolyType.

### Converter factories

When one converter pattern applies to many types, implement <xref:Nerdbank.Json.IJsonConverterFactory> and register it with <xref:Nerdbank.Json.JsonSerializer.ConverterFactories?displayProperty=nameWithType>.

[!code-csharp[](../../samples/cs/CustomConverters.cs#CustomConverterFactory)]

While a factory is creating a converter, it can use <xref:Nerdbank.Json.ConverterContext> to resolve supporting converters.
Both converter contexts expose the active shape provider and support typed shapes, untyped shapes, witness types, and runtime-type lookup.
Factories implementing <xref:PolyType.Abstractions.ITypeShapeFunc> can use <xref:Nerdbank.Json.JsonConverterFactoryExtensions.Invoke*> to enter a generic context without reflection-based generic activation.
The `EnvelopeConverterFactory` sample uses reflective activation and is exercised only by the managed sample tests; prefer shape-based generic dispatch for NativeAOT.

## Reference cycles in custom converters

Call <xref:Nerdbank.Json.SerializationContext.ReportObjectConstructed*> after constructing an instance and before deserializing its members.
When cycle-preserving deserialization is enabled, nested references can then resolve to that instance before the converter returns.
Return the same instance that was reported.
The call has no effect when cycle preservation is not active.

## Targeted navigation in custom representations

Override <xref:Nerdbank.Json.JsonConverter.SkipToPropertyValue*> and <xref:Nerdbank.Json.JsonConverter.SkipToIndexValue*> when a custom representation remaps logical members, array indices, or dictionary keys.
The corresponding asynchronous hooks return whether the selected value was found.
Use the asynchronous reader's structural methods when entering containers so the serializer can drain the enclosing envelope correctly.
These are public converter hooks; the internal navigation dispatchers are not extension points.

## Version compatibility

Consider forward and backward compatibility in your converter.
When reading JSON objects, continue reading every property even if some are unrecognized.
When reading arrays, consume every element even if the current version only understands a prefix of them.

The simplest compatible pattern for object-like data is to switch on property names and skip unknown values.

## Union types

When a custom converter is applied to the base type of a union, the built-in union wrapper still handles discriminator selection when the converter is attached to the concrete case type or base type itself.
But when a converter is attached directly to a union-typed member or constructor parameter, that converter takes over the full representation for that slot, including any discriminator logic.

For more about the built-in union envelope, see [unions](unions.md).
