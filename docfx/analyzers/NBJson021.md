# NBJson021: Converter requires a public parameterless constructor

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson021 |
| **Category** | Usage |
| **Severity** | Error |

## Cause

An otherwise compatible converter selected by <xref:Nerdbank.Json.JsonConverterAttribute> has no public parameterless constructor.
Attribute-selected converters require that constructor for activation.

## How to fix violations

Add a public parameterless constructor if the converter can be initialized without arguments.
If it requires application state or dependencies, remove the attribute and register an initialized instance through <xref:Nerdbank.Json.JsonSerializer.Converters>, or use a converter factory.
See [custom converters](../docs/custom-converters.md).

There is no automatic code fix for this rule.

## When to suppress

Do not suppress this diagnostic for an attribute-selected converter that cannot be activated.
