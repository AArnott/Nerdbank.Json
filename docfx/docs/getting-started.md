# Getting Started

## Installation

Consume Nerdbank.Json from its NuGet package.

## Usage

Given a mutable type annotated with <xref:PolyType.GenerateShapeAttribute> like this:

[!code-csharp[](../../samples/cs/GettingStarted.cs#SimpleObject)]

> [!IMPORTANT]
> All types attributed with <xref:PolyType.GenerateShapeAttribute> must be declared with the `partial` modifier.
> If these are nested types, all containing types must also have the `partial` modifier.

You can serialize and deserialize it like this:

[!code-csharp[](../../samples/cs/GettingStarted.cs#SimpleObjectRoundtrip)]

Only the top-level type that you directly serialize needs <xref:PolyType.GenerateShapeAttribute>.
Referenced property types in the object graph can be discovered through the generated shape provider.

For built-in scalar values, the same serializer APIs work directly:

```csharp
using Nerdbank.Json;

JsonSerializer serializer = new();

string text = serializer.Serialize(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"));
Guid value = serializer.Deserialize<Guid>(text);
```

## Supported Values

The current serializer surface includes built-in scalar and selected BCL types, mutable object graphs with settable properties, parameterized-constructor object materialization, mutable `ICollection<T>` implementations such as `List<T>`, getter-only mutable collection properties that can be populated in place, and mutable dictionaries whose key type is a supported simple scalar or enum-like type.

Examples include:

* Primitive numbers and booleans
* Strings and chars
* Date and time types
* Guids, URIs, versions, cultures, encodings
* Byte buffers
* Selected drawing primitives such as `Color` and `Point`
* Mutable object graphs annotated with <xref:PolyType.GenerateShapeAttribute>
* Mutable lists and supported-key dictionaries
* Closed unions annotated with <xref:PolyType.DerivedTypeShapeAttribute>

By default, object property names serialize as camelCase. Dictionary keys remain unchanged unless <xref:Nerdbank.Json.JsonSerializer.DictionaryKeyNamingPolicy> is explicitly set.

When deserializing through a parameterized constructor, required constructor parameters must be present in JSON and duplicate assignments to the same constructor parameter are rejected.

When deserializing properties, explicit JSON `null` is rejected for non-nullable reference-type properties. Getter-only mutable collection properties are populated into their existing instances rather than replaced.

## String Escaping

Strings are escaped according to RFC 8259 requirements. Characters that do not require escaping are preserved as-is instead of being over-escaped.

## Current Limitations

The current implementation still does not provide immutable collection breadth, delegate serialization, arbitrary complex-object dictionary keys, read-only scalar property population, or the broader contract customization system planned for the library.
