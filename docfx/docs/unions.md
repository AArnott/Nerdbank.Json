# Polymorphic serialization

A value whose runtime type varies needs a union so it round-trips.
This page covers every kind of union Nerdbank.Json supports and the options that change how each is written.

## Choosing a union approach

| You have | Use | Default shape |
|--|--|--|
| A base type with derived types | [Derived type hierarchies](#derived-type-hierarchies) | `{"$type":"Cow", ...}` |
| A C# 15 `union` | [C# unions](#c-unions) | Bare when its JSON kind is unique; otherwise `{"$type": ...}` |
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

C# 15 [union types](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) hold one of a closed set of case types.
Cases need not share a base type.
They need no attributes beyond <xref:PolyType.GenerateShapeAttribute>.
There is no base-type fallback.

### Tagging modes

A case whose JSON kind is unique in the union is written bare, such as `42` or `"hi"`.
Object cases, nested unions, and custom converters are written with `$type`.
JSON cannot tell numeric widths, `float` from `double`, or two string or collection cases apart.
Those cases need [explicit identifiers](#custom-case-identifiers).
Only one untagged array case and one untagged object case are allowed.
An ambiguous union is rejected instead of guessed.

### Options

| Option | Purpose |
|--|--|
| <xref:Nerdbank.Json.JsonSerializer.UnionTypeAliasProvider> | Custom `$type` values. A case with an alias is always tagged. |
| <xref:Nerdbank.Json.JsonSerializer.AllowOutOfOrderDiscriminator> | Allow `$type` after other properties. See [Security](#security). |

### Example

[!code-csharp[](../../samples/cs/CSharpUnions.cs#CSharpUnion)]

[!code-csharp[](../../samples/cs/CSharpUnions.cs#CSharpUnionUsage)]

`new Result(42)` serializes as `42`, and `new Result("hi")` as `"hi"`.
A null payload selects the first nullable bare case.

### Custom case identifiers

Set <xref:Nerdbank.Json.JsonSerializer.UnionTypeAliasProvider?displayProperty=nameWithType> to assign stable string or integer identifiers.
This does not change payload converters, and it does not affect type hierarchies, F# unions, or runtime derived-type mappings.

[!code-csharp[](../../samples/cs/CSharpUnions.cs#CSharpUnionAliases)]

An alias forces `$type` even when the case's JSON kind would otherwise be enough.
That case then accepts only the alias, not its inferred name.
Providers must be thread-safe and return the same result for as long as they are used.
Aliases are resolved when the converter is built.
For a callback, use <xref:Nerdbank.Json.DelegateUnionTypeAliasProvider>, or implement <xref:Nerdbank.Json.IUnionTypeAliasProvider>.

## Duck typing

Duck typing reads a bare JSON object by its required properties.
Opt in per hierarchy with <xref:Nerdbank.Json.DerivedTypeDuckTyping>.
It is experimental.

### How a case is chosen

1. Each case other than the catch-all matches when all of its required properties are present.
1. If several cases match, the one whose required names are a strict superset of each of the others wins.
1. If none match, the catch-all is used: the one object case with no required properties.
1. Otherwise the read throws <xref:Nerdbank.Json.JsonSerializationException>. There is no base-type fallback.

A union has at most one catch-all, and the catch-all does not break a tie.
Required properties follow object deserialization: constructor parameters, C# `required`, and members flagged by <xref:PolyType.PropertyShapeAttribute>.
Names honor <xref:PolyType.PropertyShapeAttribute.Name>, the naming policy, and case-insensitive matching.
Cases that require the same names after that policy are rejected when the converter is created.

### Discriminator members

<xref:Nerdbank.Json.UnionDiscriminatorAttribute> names the constant written with an object, whether or not that object is used as a union case.
The member is written ahead of the type's other properties.
On read it is optional. A value that is present must match, or deserialization throws <xref:Nerdbank.Json.JsonSerializationException> with <xref:Nerdbank.Json.JsonSerializationException.ErrorCode.DiscriminatorMismatch>.

[!code-csharp[](../../samples/cs/CSharpUnions.cs#DocumentChange)]

[!code-json[](../../samples/cs/Output/CSharpUnions.DocumentChange.json)]

`kind` is part of `CreateFile` and `DeleteFile`. `TextDocumentEdit` has no discriminator.
These object cases are still wrapped in `$type`.

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

<xref:Nerdbank.Json.JsonSerializer.UnionTypeAliasProvider> sets `$type` values for C# unions you can't annotate.
See [Custom case identifiers](#custom-case-identifiers).

## Security

`$type` must be the first property.
<xref:Nerdbank.Json.JsonSerializer.AllowOutOfOrderDiscriminator> accepts it later, but then the whole union value is buffered and nested unions can take quadratic time.
Enable it only for trusted input.
See [Security](security.md).

## Custom converters

A converter on a union-typed member replaces the whole shape, including `$type`.
A converter on a concrete case controls only that payload and still sits inside the union envelope.
See [custom converters](custom-converters.md).
