# JSON Schema export

The `JsonSerializer.GetJsonSchema` method returns a mutable <xref:System.Text.Json.Nodes.JsonObject> containing a standards-based **JSON Schema (draft 2020-12 by default)** document that describes how the serializer represents a type.
Call `ToJsonString()` on the result to obtain JSON text.
The schema DOM is distinct from the optional native JSON DOM; ordinary serialization does not execute through System.Text.Json.
The schema is generated from the effective, source-generated shape and converter graph, so it is trimming-safe
and NativeAOT-safe, and it reflects the serializer's configuration.

[!code-csharp[](../../samples/cs/JsonSchemaExport.cs#JsonSchemaExport)]

Pass <xref:Nerdbank.Json.JsonSchemaOptions> to select JSON Schema Draft 7 for use in OpenRPC documents, or Draft 4 for older validators.
The dialect applies to the entire document, including recursive definitions, union discriminators, and tuple arrays.
The default remains Draft 2020-12.

[!code-csharp[](../../samples/cs/JsonSchemaExport.cs#JsonSchemaDraft7)]

Use <xref:Nerdbank.Json.JsonSchemaDialect.Draft4> instead to select Draft 4.
Draft 4 and Draft 7 use an array-valued `items` keyword for tuples and `additionalItems` for any trailing item schema; Draft 2020-12 uses `prefixItems` and `items`, respectively.

## What the schema describes

* **Naming policy** — property names use the configured <xref:Nerdbank.Json.JsonSerializer.PropertyNamingPolicy>.
* **Required and nullable members** — constructor-required parameters and `required` members appear in `required`
  when <xref:Nerdbank.Json.JsonSerializer.SerializeDefaultValues> includes `Required`;
  nullable members allow `null` via a `["type","null"]` union or an `anyOf` with `{"type":"null"}`.
* **Defaults and descriptions** — constructor parameter defaults (or `DefaultValueAttribute` when no constructor default
  is available) appear as `default`; `DescriptionAttribute` annotates objects and properties.
* **Scalars** — integers include `minimum`/`maximum` for their range; strings carry `format` (for example `uuid`,
  `date-time`, `date`, `time`) or `contentEncoding` (base64) where reasonable.
* **Enums** — serialized by name when <xref:Nerdbank.Json.JsonSerializer.SerializeEnumValuesByName> is enabled
  (a string `enum` honoring the naming policy), otherwise an integer.
* **Objects** — `properties` and `additionalProperties: false`. A type with a `JsonExtensionData` member instead
  reports `additionalProperties: true`.
* **Arrays** — `type: array` with `items`; rectangular multidimensional arrays nest `items` per rank.
* **Dictionaries** — `type: object` with `additionalProperties` describing the value type.
* **Unions** — generated and runtime envelope unions produce a `oneOf` of two-element `[discriminator, payload]`
  tuples (using `const` in Draft 7 and Draft 2020-12 and single-value `enum` in Draft 4).
  With <xref:Nerdbank.Json.JsonSerializer.UseDiscriminatorObjects>, branches describe single-property discriminator objects instead.
  Runtime mappings replace or disable generated cases in the schema just as they do during serialization;
  duck-typing unions produce a `oneOf` of the bare case objects.
* **Reference preservation** — when enabled, a reference-typed schema becomes a `oneOf` of the `{"$ref": N}` and
  `{"$id": N, "$value": ...}` envelope forms. Union case payloads do not introduce a second identity envelope.
* **Recursive types** — described once under `$defs` (Draft 2020-12) or `definitions` (Draft 4 and Draft 7) and referenced with
  `$ref` for a compact, deterministic document.

## Custom converters

A custom converter describes its representation by overriding
`JsonConverter.GetJsonSchema`.
Use <xref:Nerdbank.Json.JsonSchemaContext.GetJsonSchema(PolyType.ITypeShape)> to resolve nested schemas so shared definitions and recursion are handled consistently.
The context exposes the selected <xref:Nerdbank.Json.JsonSchemaContext.Dialect> and provides `CreateTupleSchema` and `ApplyTupleSchema` helpers that accept a <xref:System.Text.Json.Nodes.JsonArray> of positional schemas.
Ownership of this array transfers to the result.
Custom converters can use the protected `ApplyJsonSchemaNullability`, `CreateUndocumentedSchema`, and `CreateJsonValue` helpers on <xref:Nerdbank.Json.JsonConverter`1>.
Custom fragments are not automatically translated between dialects; converters should honor the requested dialect or throw <xref:System.NotSupportedException>.

[!code-csharp[](../../samples/cs/JsonSchemaExport.cs#JsonSchemaCustomConverter)]

A custom converter that does **not** override the hook produces a permissive schema annotated with a conspicuous
`$comment` rather than false precision.
Registered converters override built-in scalar schemas, and property-specific converters affect only that property's schema.
Schemas describe serialization; a converter applied only to a constructor parameter may accept a different input representation.

## Caveats

* Output is deterministic: keyword order is stable and `$defs` are sorted by name.
* Reference-typed and nullable value-type roots allow `null`; member schemas express their own nullability.
* Duck-typing union schemas are permissive by nature; a JSON object may satisfy more than one `oneOf` branch.
