# JSON Schema export

The `JsonSerializer.GetJsonSchema` method produces a
standards-based **JSON Schema (draft 2020-12)** document that describes how the serializer represents a type.
The schema is generated from the effective, source-generated shape and converter graph, so it is trimming-safe
and NativeAOT-safe, and it reflects the serializer's configuration.

[!code-csharp[](../../samples/cs/JsonSchemaExport.cs#JsonSchemaExport)]

## What the schema describes

* **Naming policy** — property names use the configured <xref:Nerdbank.Json.JsonSerializer.PropertyNamingPolicy>.
* **Required and nullable members** — constructor-required parameters and `required` members appear in `required`;
  nullable members allow `null` via a `["type","null"]` union or an `anyOf` with `{"type":"null"}`.
* **Scalars** — integers include `minimum`/`maximum` for their range; strings carry `format` (for example `uuid`,
  `date-time`, `date`, `time`) or `contentEncoding` (base64) where reasonable.
* **Enums** — serialized by name when <xref:Nerdbank.Json.JsonSerializer.SerializeEnumValuesByName> is enabled
  (a string `enum` honoring the naming policy), otherwise an integer.
* **Objects** — `properties` and `additionalProperties: false`. A type with a `JsonExtensionData` member instead
  reports `additionalProperties: true`.
* **Arrays** — `type: array` with `items`; rectangular multidimensional arrays nest `items` per rank.
* **Dictionaries** — `type: object` with `additionalProperties` describing the value type.
* **Unions** — generated and runtime envelope unions produce a `oneOf` of two-element `[discriminator, payload]`
  tuples (using `const` for each discriminator); duck-typing unions produce a `oneOf` of the bare case objects.
* **Reference preservation** — when enabled, a reference-typed schema becomes a `oneOf` of the `{"$ref": N}` and
  `{"$id": N, "$value": ...}` envelope forms.
* **Recursive types** — described once under `$defs` and referenced with `$ref` for a compact, deterministic document.

## Custom converters

A custom converter describes its representation by overriding
`JsonConverter.GetJsonSchema`.
Use the <xref:Nerdbank.Json.JsonSchemaContext> to resolve nested schemas so shared definitions and recursion are
handled consistently.

[!code-csharp[](../../samples/cs/JsonSchemaExport.cs#JsonSchemaCustomConverter)]

A custom converter that does **not** override the hook produces a permissive schema annotated with a conspicuous
`$comment` rather than false precision.

## Caveats

* Output is deterministic: keyword order is stable and `$defs` are sorted by name.
* The root schema describes the non-null value; member schemas express their own nullability.
* Duck-typing union schemas are permissive by nature; a JSON object may satisfy more than one `oneOf` branch.
