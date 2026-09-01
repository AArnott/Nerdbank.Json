# Reference preservation and cycles

By default the serializer emits each reference-typed value inline, so a graph where the
same object appears more than once produces duplicate JSON and loses reference identity
on the way back. <xref:Nerdbank.Json.JsonSerializer.PreserveReferences> opts into
`$id`/`$ref` metadata that preserves reference identity.

## Modes

<xref:Nerdbank.Json.ReferencePreservationMode> has three values:

* `Off` (default) — no metadata is emitted.
* `RejectCycles` — repeated references are preserved, and a reference cycle throws during
  serialization.
* `AllowCycles` — repeated references are preserved and reference cycles are permitted.

`RejectCycles` and `AllowCycles` share the same wire format, so an acyclic document written
by one mode reads identically under the other. Only cyclic documents require `AllowCycles`
to read.

## Wire format

Each preserved value is wrapped in a metadata object: the first occurrence is written as
`{"$id":N,"$value":<value>}`, and later occurrences (including cycle back-edges) are written
as `{"$ref":N}`. The reserved property names `$id`, `$ref`, and `$value` do not collide with
serialized members because members never begin with `$` in the generated shape model.

[!code-csharp[](../../samples/cs/ReferenceCycles.cs#ReferenceCycles)]

## Deserializing cycles

To resolve a back-reference, the target object must already exist. Under `AllowCycles`, the
deserializer registers mutable objects, collections, and dictionaries **before** their
members are populated, so a member that references the object under construction resolves to
that same instance.

Immutable or constructor-bound objects cannot be registered before construction because their
members are supplied to the constructor. Such an object can still *hold* a reference to an
already-registered object, and it can still be referenced repeatedly once fully constructed,
but a back-reference to it **while it is still being constructed** fails with a clear
<xref:System.FormatException>.

## Validation

Deserialization rejects malformed metadata: a `$ref` to an id that was never defined, a
duplicated `$id`, a `$ref` object carrying extra properties, and non-positive or non-numeric
reference ids.

## Callbacks

<xref:Nerdbank.Json.IJsonSerializationCallbacks.OnAfterDeserialize> runs once per object,
after its members are populated. In a cycle, an inner object's callback therefore runs while
an outer object is still being populated, because the inner object completes first.
