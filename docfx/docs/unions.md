# Polymorphic serialization

A value whose runtime type varies needs a union so it round-trips.
This page covers every kind of union Nerdbank.Json supports and the options that change how each is written.

## Choosing a union approach

| You have | Use | Default shape |
|--|--|--|
| A base type with derived types | [Derived type hierarchies](#derived-type-hierarchies) | `{"$type":"Cow", ...}` |
| A C# 15 `union` | [C# unions](#c-unions) | Bare when the case can be recognized; otherwise `{"$type": ...}` |
| JSON with no discriminator | [Duck typing](#duck-typing) | The bare object |
| Types you can't annotate, or cases known only at run time | [Runtime configuration](#runtime-configuration) | As configured |

F# discriminated unions work without any of this. See [F# contracts](fsharp.md).

## Derived type hierarchies

A union stands in for a base type and a closed set of derived types.
Only types you list are preserved. A more-derived runtime value is written as the nearest listed case.

### Declaring derived types

A property typed as the base class keeps only the base properties unless you opt in.

[!code-csharp[](../../samples/cs/Unions.cs#LossyFarm)]

[!code-json[](../../samples/cs/Output/Unions.LossyFarm.json)]

Deserializing that produces `Animal` objects.
If `Animal` were abstract, it would not deserialize.

Apply <xref:PolyType.DerivedTypeShapeAttribute> on the base type to keep each derived type:

[!code-csharp[](../../samples/cs/Unions.cs#RoundtrippingFarmAnimal)]

[!code-json[](../../samples/cs/Output/Unions.RoundtrippingFarmAnimal.json)]

The `$type` wrapper is used only when the declared type is the union.
A property declared as `UnionHorse` is written as that object, with no `$type`.

### Case identifiers

With no explicit tag, a derived case uses its type name and the base case uses `"$type":"$base"`.

Choose string names:

[!code-csharp[](../../samples/cs/Unions.cs#StringAliasTypes)]

Or integers, which are smaller and do not depend on CLR names:

[!code-csharp[](../../samples/cs/Unions.cs#IntAliasTypes)]

An unrecognized discriminator throws <xref:Nerdbank.Json.JsonSerializationException>.

### Unknown derived types

Union support is closed-world.
Only the base type and the derived types listed with <xref:PolyType.DerivedTypeShapeAttribute> participate.
A runtime value more derived than any listed case is serialized as the nearest listed case and comes back as that case.

### Wire format

Tagged cases are JSON objects.
Object properties sit beside `$type`.
Scalars and custom converters use `$value`.
The base contract uses `"$type":"$base"`.
Integer tags are decimal strings, so tag `7` is `"$type":"7"`.
`$type` is the first property unless [out-of-order discriminators](#security) are enabled.

## C# unions

C# 15 [union types](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) hold exactly one of a closed set of case types.
They need no attributes beyond <xref:PolyType.GenerateShapeAttribute>.

### Tagging modes

| Mode | Writes | Reads |
|--|--|--|
| `Auto` (default) | Bare when the case can be recognized: by its JSON kind, a discriminator, its properties, or the first-declared rule. Otherwise `{"$type": ..., ...}` | Bare values, discriminators, properties, and `$type` |
| `Untagged` | Never adds `$type`, except for an alias, a custom converter, or a recursive union case. An object case that cannot be recognized is rejected | Same as `Auto` |
| `Tagged` | Every object uses `$type`. Other values are bare only when no other case shares their JSON kind | `$type`, and a bare value when its kind is unambiguous. A bare value that needs duck typing throws |

Keep `Auto` for most JSON APIs.
Use `Tagged` when every object should carry its type.
<xref:Nerdbank.Json.JsonSerializer.DefaultUnionTagging> set to `Tagged` writes object cases with `$type` and does not duck-type.

In `Auto` and `Untagged`, several number, string, or collection cases are written bare.
The reader uses range, element types, and declaration order.
`Tagged` still wraps those collisions.
A union case that is itself a non-recursive C# union is written as its inner cases.
A recursive union case stays wrapped, as does a custom converter.

### Options

| Option | Purpose |
|--|--|
| <xref:Nerdbank.Json.JsonSerializer.DefaultUnionTagging> | Mode for every C# union. Default: `Auto`. |
| <xref:Nerdbank.Json.UnionTaggingAttribute> | Mode for one union. Overrides the serializer property. |
| <xref:Nerdbank.Json.JsonSerializer.UnionTaggingProvider> | Mode for unions you can't annotate. Overrides both. |
| <xref:Nerdbank.Json.JsonSerializer.UnionTypeAliasProvider> | Custom `$type` values. A case with an alias is always tagged. |
| <xref:Nerdbank.Json.JsonSerializer.AllowOutOfOrderDiscriminator> | Allow `$type` after other properties. See [Security](#security). |

The provider wins, then the attribute, then the serializer property.
Derived type hierarchies and F# unions ignore these settings.

### Example

[!code-csharp[](../../samples/cs/CSharpUnions.cs#Value)]

`Auto` (default), then `Tagged`:

[!code-json[](../../samples/cs/Output/CSharpUnions.Value.Auto.json)]

[!code-json[](../../samples/cs/Output/CSharpUnions.Value.Tagged.json)]

[!code-csharp[](../../samples/cs/CSharpUnions.cs#CSharpUnion)]

[!code-csharp[](../../samples/cs/CSharpUnions.cs#CSharpUnionUsage)]

`new Result(42)` serializes as `42`, and `new Result("hi")` as `"hi"`.
A null payload selects the first nullable bare case.

### Custom case identifiers

Set <xref:Nerdbank.Json.JsonSerializer.UnionTypeAliasProvider?displayProperty=nameWithType> to assign stable string or integer identifiers.
This does not change payload converters, and it does not affect type hierarchies, F# unions, or runtime derived-type mappings.

[!code-csharp[](../../samples/cs/CSharpUnions.cs#CSharpUnionAliases)]

An alias forces a `$type` wrapper in every tagging mode, including `Untagged`, even when the case's JSON kind would otherwise be enough.
That case then accepts only the alias, not its inferred name.
Providers must be thread-safe and return the same result for as long as they are used.
Aliases are resolved when the converter is built.
For a callback, use <xref:Nerdbank.Json.DelegateUnionTypeAliasProvider>, or implement <xref:Nerdbank.Json.IUnionTypeAliasProvider>.

## Duck typing

Duck typing reads a bare JSON value by its shape.
It applies to C# unions in `Auto` and `Untagged`, and to derived types listed with <xref:Nerdbank.Json.DerivedTypeDuckTyping>.
Derived-type duck typing is experimental.

### How a case is chosen

For a C# union in `Auto` or `Untagged`:

1. The JSON kind (number, string, true/false, array, object), when only one case accepts it.
1. `$type`, or a <xref:Nerdbank.Json.UnionDiscriminatorAttribute>, as the first property. <xref:Nerdbank.Json.JsonSerializer.AllowOutOfOrderDiscriminator> also accepts either one later in the object.
1. A <xref:Nerdbank.Json.UnionClassifierAttribute>, when the union has one. `Tagged` does not call it.
1. Required properties, among the cases still possible. If several match, the one whose required names are a strict superset of the others wins. An object case with no required properties and no discriminator is the catch-all, and it is used only when nothing else matches.
1. Otherwise the first declared case that can represent the value, for numbers and collections. `5` read as `union(int, long)` is an `int`; `5000000000` is a `long`. Objects that match more than one case throw.

Derived-type duck typing uses required properties the same way: the strict superset wins, one catch-all is allowed, and there is no base-type fallback.

A discriminator is optional on read. When it is present it selects the case, and a wrong value is an error. When it is absent, those cases still compete on their other required properties, so `{"uri":"..."}` matches both `CreateFile` and `DeleteFile` and throws.
An unrecognized discriminator value matches none of those cases and excludes the catch-all.
Required properties follow object deserialization: constructor parameters, C# `required`, and members flagged by <xref:PolyType.PropertyShapeAttribute>.
Names honor <xref:PolyType.PropertyShapeAttribute.Name>, the naming policy, and case-insensitive matching.
Two object cases that still cannot be told apart are wrapped in `Auto` and rejected in `Untagged`.

### Discriminator members

<xref:Nerdbank.Json.UnionDiscriminatorAttribute> names the constant written with an object, whether or not that object is used as a union case.
The member is written ahead of the type's other properties.
On read it is optional. A value that is present must match, or deserialization throws <xref:Nerdbank.Json.JsonSerializationException> with <xref:Nerdbank.Json.JsonSerializationException.ErrorCode.DiscriminatorMismatch>.

[!code-csharp[](../../samples/cs/CSharpUnions.cs#DocumentChange)]

[!code-json[](../../samples/cs/Output/CSharpUnions.DocumentChange.json)]

`kind` is part of `CreateFile` and `DeleteFile`. `TextDocumentEdit` has no discriminator.
In `Auto` the cases are written bare. `kind` selects the first two, and `textDocument` plus `edits` selects the third.

### Duck typing derived types

[!code-csharp[](../../samples/cs/RuntimeUnions.cs#DuckTypingUnions)]

[!code-json[](../../samples/cs/Output/RuntimeUnions.DuckTypingUnions.json)]

Hierarchies stay tagged unless listed with <xref:Nerdbank.Json.DerivedTypeDuckTyping>.
`Drawing` has no required properties, so it is the catch-all.
`{"radius":2.5}` is a `Circle`. An object with neither `radius` nor `side` is a `Drawing`.

### Same shapes as Nerdbank.MessagePack

Nerdbank.MessagePack uses these same rules for types listed with its `DerivedTypeDuckTyping`.

## Runtime configuration

Shape attributes are the default.
<xref:Nerdbank.Json.JsonSerializer.DerivedTypeUnions> replaces them per serializer with an immutable <xref:Nerdbank.Json.DerivedTypeUnionCollection>.
Cases are explicit `ITypeShape` values, so nothing is discovered by reflection.

### Registering derived types at runtime

<xref:Nerdbank.Json.DerivedShapeMapping`1> builds a union from shapes you supply.
<xref:Nerdbank.Json.DerivedTypeMapping`1> can also take types from a provider passed to its constructor.
A mapping can add a union that was not generated, or replace one that was.
To extend a generated union, include its current cases and the new ones.

[!code-csharp[](../../samples/cs/RuntimeUnions.cs#RuntimeUnions)]

[!code-json[](../../samples/cs/Output/RuntimeUnions.RuntimeUnions.json)]

A case is identified by an integer tag or a string name. Both are strings in `$type`.
Object properties are written beside `$type`. Scalars and custom converters use `$value`.
The base contract uses `"$type":"$base"`.

### Removing a union at runtime

<xref:Nerdbank.Json.DerivedTypeUnion.CreateDisabled(System.Type)> ignores a generated union and writes only the base contract.

### Cache identity

Adding a mapping freezes the collection.
Further changes throw <xref:System.InvalidOperationException>.
Reusing one collection keeps the same configuration. Assigning another rebuilds the converter cache.

### Runtime tagging and aliases

<xref:Nerdbank.Json.JsonSerializer.UnionTaggingProvider> and <xref:Nerdbank.Json.JsonSerializer.UnionTypeAliasProvider> set the mode and `$type` values for C# unions you can't annotate.
See [Custom case identifiers](#custom-case-identifiers).

## Security

`$type` or a discriminator must be the first property.
<xref:Nerdbank.Json.JsonSerializer.AllowOutOfOrderDiscriminator> accepts it later, but then the whole union value is buffered and nested unions can take quadratic time.
Enable it only for trusted input.
See [Security](security.md).

## Custom converters

A converter on a union-typed member replaces the whole shape, including `$type`.
A converter on a concrete case controls only that payload and still sits inside the union envelope.
See [custom converters](custom-converters.md).
