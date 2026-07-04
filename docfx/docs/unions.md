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

## Current scope

Nerdbank.Json currently supports attribute-driven union metadata through PolyType shapes.
It does not currently document or expose the broader runtime union-registration and duck-typing features available in Nerdbank.MessagePack.

## Custom converters

A custom converter applied directly to a union-typed member takes over that member's entire serialization shape, including any discriminator handling.
By contrast, a converter applied to a concrete union case type only controls that concrete payload and still participates inside the built-in union envelope.

For general guidance on authoring converters, see [custom converters](custom-converters.md).
