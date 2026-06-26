# Nerdbank.Json

[![NuGet package](https://img.shields.io/nuget/v/Nerdbank.Json.svg)](https://nuget.org/packages/Nerdbank.Json)

Nerdbank.Json is a UTF-8 JSON serializer being built in the same general shape as Nerdbank.MessagePack, but without layering on top of System.Text.Json, Newtonsoft.Json, or any other serializer.

The current implementation includes a low-level writer/reader pair, a built-in converter table for common .NET types, and an initial PolyType-backed object serializer for mutable object graphs. It is allocation-conscious in the hot path and multi-targets `net8.0`, `net9.0`, `net472`, and `netstandard2.0`.

## Features

* Direct UTF-8 JSON writing to `IBufferWriter<byte>`.
* RFC 8259 string escaping, limited to the characters that must be escaped.
* Built-in serialization and deserialization for common .NET primitive and BCL types.
* PolyType-driven serialization for object graphs with settable properties.
* Mutable `ICollection<T>` implementations with public parameterless constructors, including `List<T>`.
* Mutable `IDictionary<string, TValue>` implementations with public parameterless constructors, including `Dictionary<string, TValue>`.
* camelCase property naming by default, configurable with `JsonNamingPolicy`.
* Optional case-insensitive property-name matching during deserialization.
* Optional enum-name serialization with numeric fallback for unnamed values.
* Built-in byte buffer handling using Base64 JSON strings.
* Configurable deserialization policy for missing required values and non-nullable reference validation.
* Synchronous stream and async stream overloads.
* Multi-targeting across modern .NET and .NET Framework.
* Test-driven development with focused round-trip coverage for the current built-in type surface.

## Supported Built-In Types

The current built-in serializer supports:

* `string`, `char`, `bool`
* Numeric primitives: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`
* `System.Numerics.BigInteger`
* `DateTime`, `DateTimeOffset`, `TimeSpan`
* `Guid`, `Version`, `Uri`, `CultureInfo`, `Encoding`
* `byte[]`, `Memory<byte>`, `ReadOnlyMemory<byte>`
* `System.Drawing.Color`, `System.Drawing.Point`
* On .NET 8 and later: `Half`, `Int128`, `UInt128`, `DateOnly`, `TimeOnly`, `Rune`

## Example

```csharp
using Nerdbank.Json;

JsonSerializer serializer = new();

string json = serializer.Serialize(new DateTimeOffset(2024, 7, 1, 12, 34, 56, TimeSpan.Zero));
DateTimeOffset value = serializer.Deserialize<DateTimeOffset>(json);
```

For mutable object graphs, annotate your root type with `GenerateShape` and use the same serializer APIs:

```csharp
using Nerdbank.Json;
using PolyType;

[GenerateShape]
public partial class Person
{
	public string? Name { get; set; }
	public int Age { get; set; }
}

JsonSerializer serializer = new();
string json = serializer.Serialize(new Person { Name = "Ada", Age = 37 });
Person person = serializer.Deserialize<Person>(json);
```

By default, object property names serialize as camelCase. To preserve declared member names or apply a different built-in transform:

```csharp
JsonSerializer serializer = new()
{
	PropertyNamingPolicy = JsonNamingPolicy.SnakeLowerCase,
};
```

This naming policy applies to object property names. Dictionary keys remain unchanged by default. To opt into dictionary key transformation separately:

```csharp
JsonSerializer serializer = new()
{
	DictionaryKeyNamingPolicy = JsonNamingPolicy.CamelCase,
};
```

## Design Notes

* The low-level JSON writer does not delegate to another serializer.
* String escaping follows RFC 8259 requirements instead of aggressively escaping extra characters.
* This repository is intended to grow toward the richer converter and visitor architecture used by Nerdbank.MessagePack.
* The current object-graph layer supports mutable types with settable properties, uses camelCase property names by default, and ignores unknown JSON properties during deserialization.
* Mutable `ICollection<T>` and `IDictionary<string, TValue>` implementations with public parameterless constructors are supported through the same converter cache.
* Current stream overloads buffer the full payload before reading or writing; incremental streaming APIs will come later.

## Status

The built-in type surface listed above is working and covered by focused serializer tests. PolyType-backed mutable object graphs and stream/async overloads are now available in an initial form. Broader contract features, constructor-based materialization, and incremental streaming remain under active development.
