# Runtime union configuration

Polymorphic types (unions) are normally described by shape-generated metadata and serialized
using a two-element `[discriminator, payload]` envelope. <xref:Nerdbank.Json.JsonSerializer.Unions>
lets a caller override that behavior per serializer using an immutable
<xref:Nerdbank.Json.JsonUnionConfiguration>.

Because every case is supplied as an explicit `ITypeShape<TCase>`, the feature
stays NativeAOT-safe: no runtime reflection is used to discover cases.

## Operations

<xref:Nerdbank.Json.JsonUnion`1> builds a union definition. Combined with the configuration methods,
you can:

* **Add** a union for a base type that has no generated union: `JsonUnion<TBase>.Create().AddCase(...)`.
* **Replace** a generated union: `Create().AddCase(...)` (generated cases are omitted).
* **Extend** a generated union: `Create().IncludeGenerated().AddCase(...)`.
* **Disable** union handling: `JsonUnionConfiguration.Default.WithoutUnion<TBase>()`.

Cases may be identified by an integer tag or a string name, matching the default representation.

[!code-csharp[](../../samples/cs/RuntimeUnions.cs#RuntimeUnions)]

The default two-element representation and behavior are preserved: without any configuration, unions
serialize exactly as before, and a runtime-configured union that mirrors the generated cases produces
the same JSON.

## Cache identity

The configuration is immutable, and every method returns a new instance. Reusing the same
<xref:Nerdbank.Json.JsonUnionConfiguration> instance across serializers preserves converter-cache
identity; assigning a different instance rebuilds the cache.

## Duck typing (experimental)

For legacy JSON that lacks a discriminator, `UseDuckTyping()` selects a case during deserialization
from the unique set of **required** serialized property names present in a JSON object. Values are
written as the bare case object rather than the envelope.

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

A conventional single-property object discriminator is intentionally not introduced, because the
two-element envelope remains the compatible default.
