# Runtime union configuration

Polymorphic types (unions) are normally described by shape-generated metadata and serialized using a two-element `[discriminator, payload]` envelope.
<xref:Nerdbank.Json.JsonSerializer.DerivedTypeUnions> lets a caller override that behavior per serializer using an immutable <xref:Nerdbank.Json.DerivedTypeUnionCollection>.

Because every case is supplied as an explicit `ITypeShape<TCase>`, the feature
stays NativeAOT-safe: no runtime reflection is used to discover cases.

## Operations

<xref:Nerdbank.Json.DerivedShapeMapping`1> builds a union definition from explicit shapes, source-generated witness types, or a shape provider.
<xref:Nerdbank.Json.DerivedTypeMapping`1> additionally supports adding types using a provider supplied to its constructor.

Mappings can add a union to a base type without generated union metadata or replace an existing generated union.
To extend a generated union, include its existing cases alongside the additional cases in the mapping.
Use <xref:Nerdbank.Json.DerivedTypeUnion.CreateDisabled(System.Type)> to ignore a generated union and serialize only its base contract.

Cases may be identified by an integer tag or a string name, matching the default representation.

[!code-csharp[](../../samples/cs/RuntimeUnions.cs#RuntimeUnions)]

The default two-element representation and behavior are preserved: without any configuration, unions
serialize exactly as before, and a runtime-configured union that mirrors the generated cases produces
the same JSON.

## Cache identity

Adding a mapping to a <xref:Nerdbank.Json.DerivedTypeUnionCollection> freezes it.
Further mutation throws <xref:System.InvalidOperationException>.
Reusing the same collection across serializers preserves configuration identity; assigning a different collection rebuilds the cache.

## Duck typing (experimental)

For legacy JSON that lacks a discriminator, <xref:Nerdbank.Json.DerivedTypeDuckTyping> selects a case during deserialization from the unique set of **required** serialized property names present in a JSON object.
Values are written as the bare case object rather than the envelope.

[!code-csharp[](../../samples/cs/RuntimeUnions.cs#DuckTypingUnions)]

Duck typing has important constraints:

* It is **slower** than the default strategy because each object is buffered and re-scanned.
* It uses **required** properties only; optional properties are not reliable evidence and are ignored for case selection.
  Constructor requirements, C# `required` members, and `PropertyShapeAttribute.IsRequired` follow the same precedence as ordinary object deserialization.
  Computed getter-only scalar properties do not participate.
* It checks **every case** and rejects ambiguous input (a JSON object whose properties satisfy more than one case) and insufficient evidence (no case whose required properties are all present) with <xref:Nerdbank.Json.JsonSerializationException> retaining a causal <xref:System.FormatException>.
  Neither registration order nor property order breaks a tie.
* The constructor requires at least two distinct concrete, assignable cases.
  An abstract or interface base is not a case; a concrete base is automatically included and must satisfy the same requirements as every derived case.
  Every case must have at least one required object property; missing required-property metadata is rejected immediately with <xref:System.ArgumentException>, before a serializer is used.
* A concrete base is **not a fallback** for an unrecognized or ambiguous object.
  In particular, if a derived case requires all of the base case's required names, an object satisfying the derived case also satisfies the base and is rejected as ambiguous.
  Use discriminator-based unions when the contracts overlap this way.
* Property names respect explicit shape names, the serializer's naming policy, and its case-insensitive matching setting.
  Serialized names are resolved when the converter is created, not in the union constructor, because one union configuration can be reused with different serializers.
  Naming-policy or case-folding collisions can therefore make otherwise distinct cases ambiguous when reading input.
* Custom case converters are honored, but their bare JSON objects must retain the required serialized property names described by their shapes.
  Duck typing does not infer a new selection contract from converter output or a JSON Schema.
  JSON Schema describes the bare case objects with `oneOf`, regardless of `UseDiscriminatorObjects`.
* The buffered re-scan honors the serializer's `AllowTrailingCommas` and `ReadCommentHandling`
  settings, so comments and trailing commas inside a duck-typed object are handled exactly as they
  are for the outer document.

These ambiguity and evidence checks deliberately differ from Nerdbank.MessagePack's experimental filter-and-base-fallback behavior.
JSON does not select an unchecked remaining candidate or fall back to a base object after evidence fails.
The `DuckTyping` experimental diagnostic remains in effect.

## Discriminator objects

Set <xref:Nerdbank.Json.JsonSerializer.UseDiscriminatorObjects> to write and read single-property envelopes such as `{"circle":{"radius":2.5}}` instead of `["circle",{"radius":2.5}]`.
The two-element envelope remains the default.
Integer discriminators use invariant-culture property text: discriminator `7` becomes property `"7"`.
The reserved property `$base` identifies a base-contract fallback.
Aliases that collide after conversion to property names, or use `$base`, are rejected.
JSON Schema generation and expression-based targeted navigation honor this setting.
Reference preservation still uses the idiomatic `$id`, `$ref`, and `$value` JSON metadata objects.
