# Polymorphic serialization

Serialization of polymorphic types requires special consideration.

For instance, suppose you want to serialize a `Farm`:

[!code-csharp[](../../samples/cs/Unions.cs#LossyFarm)]

Notice that your animals on the farm are kept in a collection typed with the base type `Animal`.
At runtime, we expect most or all animals to be of a derived type rather than the `Animal` base type.

By default, serializing the `Farm` will only serialize animals with the properties that are directly on the `Animal` class.

```json
{
  "animals": [
    { "name": "Bessie" },
    { "name": "Lightning" },
    { "name": "Rover" }
  ]
}
```

Note the lack of any type information or properties defined on the derived types.
Deserializing this `Farm` will produce a bunch of `Animal` objects.
If `Animal` were an `abstract` class, this would not be deserializable at all.

You can preserve polymorphic type metadata across serialization using <xref:PolyType.DerivedTypeShapeAttribute>, which you apply to the declared base type:

[!code-csharp[](../../samples/cs/Unions.cs#RoundtrippingFarmAnimal)]

This changes the serialized form to include a discriminator and the payload for each union case.

Now the farm serializes like this:

```json
{
  "animals": [
    ["Cow", { "name": "Bessie", "weight": 1400 }],
    ["Horse", { "name": "Lightning", "speed": 45 }],
    ["Dog", { "name": "Rover", "color": "Brown" }]
  ]
}
```

Deserializing this will recreate each object with its original derived type and full set of properties.

## Unions

A type that stands in for itself and a closed set of derived types is called a union.
Nerdbank.Json serializes unions as 2-element arrays where the first element is the discriminator and the second element is the object payload:

```json
["TypeName", { "property": "value" }]
```

This wrapper is only used when the declared type is the union type.
If a property is declared as `Horse`, then a `Horse` value is serialized directly without the union wrapper.

## Union case identifiers

Each type in a union is a union case and has a discriminator by which it is recognized during deserialization.

When no explicit tag is supplied, Nerdbank.Json uses the case type name for derived types and `null` for the base type itself.
This is the behavior shown in the earlier example.

You can explicitly choose string discriminators:

[!code-csharp[](../../samples/cs/Unions.cs#StringAliasTypes)]

Or integer discriminators:

[!code-csharp[](../../samples/cs/Unions.cs#IntAliasTypes)]

Integer discriminators reduce payload size and avoid coupling the wire format to CLR type names.

## Unknown derived types

Union support is closed-world.
Only the base type and the derived types explicitly listed with <xref:PolyType.DerivedTypeShapeAttribute> are preserved polymorphically.

If a runtime object is more derived than any registered union case, Nerdbank.Json will serialize it as the nearest registered base case.
On deserialization, the object is rehydrated as that registered case, not as the original more-derived runtime type.

## Runtime configuration

Attribute-driven metadata is the default, but <xref:Nerdbank.Json.JsonSerializer.DerivedTypeUnions> can add, replace, extend, or disable unions per serializer.
See [runtime union configuration](runtime-unions.md) for explicit shape mappings, JSON object envelopes, and experimental discriminator-free duck typing.

## C# union types

C# 15 introduces [union types](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md), which hold one of a closed set of payload types without requiring a common base type.
Nerdbank.Json supports these unions with <xref:PolyType.GenerateShapeAttribute>:

[!code-csharp[](../../samples/cs/CSharpUnions.cs#CSharpUnion)]

Serialize and deserialize a union like any other shaped type:

[!code-csharp[](../../samples/cs/CSharpUnions.cs#CSharpUnionUsage)]

By default, cases use their payload type names as identifiers, such as `Int32` and `String`.
Renaming a payload type changes its default identifier.
A null payload selects the first nullable case, so the example union's default value serializes as `["String",null]`.
A default value that matches no case cannot be serialized.
Recursive unions and hand-authored types implementing the C# union member pattern are also supported.
C# unions have no base-type fallback.

The default two-element array envelope and <xref:Nerdbank.Json.JsonSerializer.UseDiscriminatorObjects?displayProperty=nameWithType> both apply to C# unions.
Payload converters, reference preservation, asynchronous serialization, and generated JSON schemas work with these unions as well.
Asynchronous operations forward to the payload converters, including asynchronous-only converters and payloads containing <xref:System.Collections.Generic.IAsyncEnumerable`1>.
Generated schemas allow null payloads for nullable cases in either envelope format.
The `union` keyword examples require C# 15 and .NET 11, or union compiler polyfills when targeting earlier frameworks.

### Custom C# union case identifiers

Set <xref:Nerdbank.Json.JsonSerializer.UnionTypeAliasProvider?displayProperty=nameWithType> to assign stable string or integer identifiers.
This does not change the union's payload types or their converters, and does not affect type hierarchies, F# unions, or runtime derived type mappings.

Use <xref:Nerdbank.Json.UnionCaseMapping> for explicit per-union registrations:

[!code-csharp[](../../samples/cs/CSharpUnions.cs#CSharpUnionAliases)]

No type shape provider or inheritance relationship is needed for registration.
Integer aliases can be used in place of strings.
The same payload type may have different aliases in different unions.
<xref:Nerdbank.Json.UnionCaseMappingCollection> implements <xref:Nerdbank.Json.IUnionTypeAliasProvider> and freezes each mapping when it is added.
Further mutation throws <xref:System.InvalidOperationException>.
Duplicate union registrations, payload types, and identifiers within a mapping are rejected.
Registered payload types are checked against the union when its converter is constructed.

Use <xref:Nerdbank.Json.DelegateUnionTypeAliasProvider> to consult an existing registry or resolve identifiers from the containing union and payload types:

[!code-csharp[](../../samples/cs/CSharpUnions.cs#CSharpUnionAliasDelegate)]

For access to the full shapes, implement <xref:Nerdbank.Json.IUnionTypeAliasProvider> directly.
Providers must be thread-safe and return consistent results for as long as they are used.
Aliases are resolved when constructing converters, not for every serialized value.
Returning `null`, or leaving a case unregistered, retains the normal identifier for that case.

Custom aliases apply to serialization, deserialization, and JSON schemas in both envelope formats.
An overridden case accepts only its custom identifier, not its original identifier.
Uninitialized identifiers and identifiers that collide with another case's custom or default identifier are rejected.

## Custom converters

A custom converter applied directly to a union-typed member takes over that member's entire serialization shape, including any discriminator handling.
By contrast, a converter applied to a concrete union case type only controls that concrete payload and still participates inside the built-in union envelope.

For general guidance on authoring converters, see [custom converters](custom-converters.md).
