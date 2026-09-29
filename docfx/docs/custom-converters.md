# Custom converters

While using <xref:PolyType.GenerateShapeAttribute> is the simplest way to make an object graph serializable, some types need custom handling.
In such cases, you can define and register your own <xref:Nerdbank.Json.JsonConverter`1>.

## Define your own converter

Consider class `Foo` that cannot be serialized automatically.

Declare a class that derives from <xref:Nerdbank.Json.JsonConverter`1>:

[!code-csharp[](../../samples/cs/CustomConverters.cs#YourOwnConverter)]

> [!CAUTION]
> It is imperative that each `Write` and `Read` method write and read exactly one JSON value.

If you have more than one field to serialize, wrap them in one JSON object or one JSON array.
If the value may be `null`, handle that explicitly before reading or writing the surrounding structure.

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

Overloads that accept a <xref:Nerdbank.Json.SerializationContext> are available for `Serialize`, `Deserialize`, `SerializeObject`, and `DeserializeObject` on <xref:Nerdbank.Json.JsonWriter> and <xref:Nerdbank.Json.JsonReader>, and for `SerializeAsync` and `DeserializeAsync` on <xref:System.IO.Pipelines.PipeWriter> and <xref:System.IO.Pipelines.PipeReader>. Keep these points in mind:

- The supplied context is used *instead of* <xref:Nerdbank.Json.JsonSerializer.StartingContext?displayProperty=nameWithType>. Its state is not merged with the serializer's, so start from `serializer.StartingContext` (as the sample does) when you want to keep the serializer's settings and state.
- The operation is canceled by the context's <xref:Nerdbank.Json.SerializationContext.CancellationToken?displayProperty=nameWithType>, so these overloads take no separate `CancellationToken` parameter.
- Never pass the context a converter receives to a top-level serializer method. Doing so throws an <xref:System.ArgumentException>. Use <xref:Nerdbank.Json.SerializationContext.GetConverter*> to (de)serialize nested values instead.

## Register your custom converter

There are three main ways to get Nerdbank.Json to use your converter.

### Attribute approach

To use your converter wherever a type appears, apply <xref:Nerdbank.Json.JsonConverterAttribute> to the type itself:

[!code-csharp[](../../samples/cs/CustomConverters.cs#CustomConverterByAttribute)]

You may also apply <xref:Nerdbank.Json.JsonConverterAttribute> to a property or constructor parameter to affect only that serialized slot:

[!code-csharp[](../../samples/cs/CustomConverters.cs#CustomConverterByAttributeOnMember)]

### Runtime registration

For precise runtime control, register a converter instance with <xref:Nerdbank.Json.JsonSerializer.Converters?displayProperty=nameWithType>:

[!code-csharp[](../../samples/cs/CustomConverters.cs#CustomConverterRegisteredAtRuntime)]

Open generic converter types may be registered with <xref:Nerdbank.Json.JsonSerializer.ConverterTypes?displayProperty=nameWithType>:

[!code-csharp[](../../samples/cs/CustomConverters.cs#CustomOpenGenericConverterRegisteredAtRuntime)]

### Converter factories

When one converter pattern applies to many types, implement <xref:Nerdbank.Json.IJsonConverterFactory> and register it with <xref:Nerdbank.Json.JsonSerializer.ConverterFactories?displayProperty=nameWithType>.

[!code-csharp[](../../samples/cs/CustomConverters.cs#CustomConverterFactory)]

While a factory is creating a converter, it can use <xref:Nerdbank.Json.JsonConverterFactoryContext> to resolve supporting converters.

## Version compatibility

Consider forward and backward compatibility in your converter.
When reading JSON objects, continue reading every property even if some are unrecognized.
When reading arrays, consume every element even if the current version only understands a prefix of them.

The simplest compatible pattern for object-like data is to switch on property names and skip unknown values.

## Union types

When a custom converter is applied to the base type of a union, the built-in union wrapper still handles discriminator selection when the converter is attached to the concrete case type or base type itself.
But when a converter is attached directly to a union-typed member or constructor parameter, that converter takes over the full representation for that slot, including any discriminator logic.

For more about the built-in union envelope, see [unions](unions.md).
