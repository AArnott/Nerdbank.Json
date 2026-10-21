# NBJson110: Describe the initialized default value

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson110 |
| **Category** | Usage |
| **Severity** | Warning |

## Cause

A serializable instance field or property in the generated shape graph has an initializer but no <xref:System.ComponentModel.DefaultValueAttribute>.
The rule covers supported attribute-compatible primitive, string, and enum member types, including members reached through generated root contracts and witnesses.
Ignored, static, implicitly declared, and unsupported member types are excluded.
Shape requests with customization arguments are not inferred by this rule.

## Rule description

An initializer alone does not describe the contract's default value to the serializer.
Default omission honors an optional constructor parameter's declared default first, then matching <xref:System.ComponentModel.DefaultValueAttribute> metadata, and otherwise uses the CLR default.
Schema export uses the same contract default.
The attribute describes the default; it does not initialize the member.

## How to fix violations

Add a matching default-value attribute while retaining the initializer.
The **Add [DefaultValue(...)]** code fix is available for constant property initializers and single-variable field declarations.
It preserves the initializer and uses a cast when necessary to preserve the boxed primitive type.
Nonconstant initializers and multi-variable field declarations require manual handling; the analyzer can report them without offering this fix.
Fix All is supported for eligible declarations.

## When to suppress

Suppress when the initializer intentionally differs from the contract default, or when its default cannot be represented by attribute metadata.
