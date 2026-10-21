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
* It uses **required** properties only; optional properties are not reliable evidence and are ignored
  for case selection.
* It **rejects ambiguous input** (a JSON object whose properties satisfy more than one case) and
  **insufficient evidence** (no case whose required properties are all present) with a clear
  <xref:System.FormatException>.
* Each case must declare at least one required property, or configuration fails.
* Property names respect the serializer's naming policy.
* The buffered re-scan honors the serializer's `AllowTrailingCommas` and `ReadCommentHandling`
  settings, so comments and trailing commas inside a duck-typed object are handled exactly as they
  are for the outer document.

## Discriminator objects

Set <xref:Nerdbank.Json.JsonSerializer.UseDiscriminatorObjects> to write and read single-property envelopes such as `{"circle":{"radius":2.5}}` instead of `["circle",{"radius":2.5}]`.
The two-element envelope remains the default.
Integer discriminators use invariant-culture property text: discriminator `7` becomes property `"7"`.
The reserved property `$base` identifies a base-contract fallback.
Aliases that collide after conversion to property names, or use `$base`, are rejected.
JSON Schema generation and expression-based targeted navigation honor this setting.
Reference preservation still uses the idiomatic `$id`, `$ref`, and `$value` JSON metadata objects.
