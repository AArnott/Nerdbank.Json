# Nerdbank.Json

[![Nerdbank.Json NuGet package](https://img.shields.io/nuget/v/Nerdbank.Json.svg?label=Nerdbank.Json)](https://www.nuget.org/packages/Nerdbank.Json)<br />
[![Nerdbank.Json.AspNetCoreMvcFormatter NuGet package](https://img.shields.io/nuget/v/Nerdbank.Json.AspNetCoreMvcFormatter.svg?label=Nerdbank.Json.AspNetCoreMvcFormatter)](https://www.nuget.org/packages/Nerdbank.Json.AspNetCoreMvcFormatter)<br />

Nerdbank.Json is a UTF-8 JSON serializer being built in the same general shape as Nerdbank.MessagePack, but without layering on top of System.Text.Json, Newtonsoft.Json, or any other serializer.

The implementation includes low-level UTF-8 readers and writers, built-in converters for common .NET types, and PolyType-backed serialization for mutable and constructor-bound object graphs.
It supports incremental asynchronous streaming, reference preservation, unions, targeted deserialization, and JSON Schema generation.
It is allocation-conscious in the hot path and multi-targets `net8.0`, `net9.0`, `net472`, and `netstandard2.0`.

## Features

* Direct UTF-8 JSON writing to `IBufferWriter<byte>`.
* Borrowed segmented UTF-8 parsing, decoded UTF-8 string views, token inspection, and stateless scalar primitives.
* Caller-owned scratch for split numeric/property tokens and incremental string decoding, with pooled fallbacks for large tokens rather than fixed token-size limits.
* `RawJson` values with explicit borrowed/owned lifetimes and automatic owned deserialization.
* RFC 8259 string escaping, limited to the characters that must be escaped.
* Built-in serialization and deserialization for common .NET primitive and BCL types.
* PolyType-driven serialization for mutable objects, records, and immutable objects with mapped constructor parameters.
* Mutable and parameterized collections and dictionaries, including read-only and immutable types when their PolyType shapes supply construction support.
* Dictionaries with supported simple scalar or enum keys, getter-only mutable collection population, and rectangular multidimensional arrays with nested or flat representations.
* camelCase property naming by default, configurable with `JsonNamingPolicy`.
* Optional indented output for human-readable JSON.
* Optional case-insensitive property-name matching during deserialization.
* Optional enum-name serialization with numeric fallback for unnamed values.
* Runtime custom converter registration with immutable converter instance/type collections or converter factories using `ConverterContext`.
* Attribute-driven custom converter registration on types, properties, and constructor parameters, with automatic generated constructor associations for ordinary and open-generic converters.
* Optional unknown-member retention through a member of type `UnusedDataPacket`.
* Built-in byte buffer handling using Base64 JSON strings.
* Optional trailing-comma and comment skipping support during deserialization.
* Configurable deserialization policy for missing required values and non-nullable reference validation.
* Optional reference preservation for repeated references, with either cycle rejection or support for cycles through mutable objects and collections.
* Synchronous stream APIs and incremental asynchronous stream/pipe serialization with cancellation and backpressure.
* Asynchronous sequence reading and writing through `DeserializeEnumerableAsync` and `SerializeEnumerableAsync`, with `SequenceOptions` selecting `JsonSequenceFormat.NDJson` or `JsonSequenceFormat.JsonArray`.
* Targeted `DeserializePath` by JSON path or member-access options, and `DeserializePathEnumerableAsync` for arrays selected inside an envelope.
* Shape-declared unions and runtime `DerivedTypeUnions` mappings, with array or single-property discriminator envelopes.
* JSON Schema draft 4, draft 7, and draft 2020-12 export as `System.Text.Json.Nodes.JsonObject`, separate from the native JSON DOM.
* Serialization callbacks, operation-scoped string interning, collection comparers, and configurable security limits.
* Opt-in native JSON DOM, untyped object, `ExpandoObject`, and System.Text.Json DOM converters.
* A separate ASP.NET Core MVC formatter package using explicitly supplied type shapes.
* A self-checking F# sample covering records, options, and discriminated unions.
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
* `RawJson` for exact UTF-8 token retention
* `System.Drawing.Color`, `System.Drawing.Point`
* On .NET 8 and later: `Half`, `Int128`, `UInt128`, `DateOnly`, `TimeOnly`, `Rune`

## Example

```csharp
using Nerdbank.Json;

JsonSerializer serializer = new();

string json = serializer.Serialize(new DateTimeOffset(2024, 7, 1, 12, 34, 56, TimeSpan.Zero));
DateTimeOffset value = serializer.Deserialize<DateTimeOffset>(json);
```

For object graphs, annotate your root type with `GenerateShape` and use the same serializer APIs:

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

To emit human-readable JSON with line breaks and indentation:

```csharp
JsonSerializer serializer = new()
{
	WriteIndented = true,
};
```

To override built-in or shape-based handling for specific types at runtime:

```csharp
JsonSerializer serializer = new()
{
	Converters = [new MyCustomConverter()],
	ConverterTypes = [typeof(MyOpenGenericConverter<>)],
	ConverterFactories = [new MyFactory()],
};
```

To attach a converter directly to a type or member instead of registering it globally:

```csharp
[JsonConverter(typeof(MyValueConverter))]
public partial class MyValue
{
	public string? Name { get; set; }
}
```

To accept more human-authored JSON during deserialization:

```csharp
JsonSerializer serializer = new()
{
	AllowTrailingCommas = true,
	ReadCommentHandling = JsonCommentHandling.Skip,
};
```

To retain and re-emit JSON properties that your type does not model yet:

```csharp
[GenerateShape]
public partial class Person
{
	public string? Name { get; set; }

	public UnusedDataPacket? UnusedData { get; set; }
}
```

The packet retains raw JSON values, including their original numeric text.
A setter is required to capture unknown members; a getter is required to write a supplied packet.
Private packet fields or properties can be included with `[PropertyShape]`.

To read a selected member without materializing the enclosing object:

```csharp
JsonSerializer.DeserializePathOptions<Person, int> options = new(person => person.Age);
int age = serializer.DeserializePath<Person, int>(json, options);
```

To stream a top-level JSON array instead of the default NDJSON framing:

```csharp
JsonSerializer.SequenceOptions options = new() { Format = JsonSequenceFormat.JsonArray };
await foreach (Person? person in serializer.DeserializeEnumerableAsync<Person>(stream, options))
{
	Console.WriteLine(person?.Name);
}
```

Rectangular multidimensional arrays default to `MultiDimensionalArrayFormat.Nested`.
Set `JsonSerializer.MultiDimensionalArrayFormat` to `MultiDimensionalArrayFormat.Flat` to use the tuple `[[dimension lengths], [flat elements]]`, preserving empty dimensions.

## Design Notes

* The low-level JSON writer does not delegate to another serializer.
* String escaping follows RFC 8259 requirements instead of aggressively escaping extra characters.
* The converter and visitor architecture uses PolyType shapes, including source-generated shapes for NativeAOT-safe contracts.
* The object-graph layer supports settable properties and mapped constructor parameters, uses camelCase property names by default, and can either ignore unknown JSON properties or retain them in an `UnusedDataPacket` member.
* Collection and dictionary construction follows the supplied PolyType shape: mutable collections are populated directly, while parameterized collections are constructed from buffered elements.
* Runtime-registered converters are consulted before built-in and PolyType-generated converters.
* Synchronous custom converters override `Write(ref JsonWriter writer, in T? value, SerializationContext context)`; public sealed object-method overrides support non-generic invocation.
* Low-level scalar output uses `JsonWriter.Write`, `WriteNull`, and `WriteRaw`; typed numeric input uses methods such as `JsonReader.ReadInt32` and `ReadDouble`.
* Custom representations expose logical navigation through `SkipToPropertyValue` and `SkipToIndexValue`, with corresponding asynchronous hooks.
* Unknown-value skipping, raw capture, and targeted navigation preserve the active remaining-depth budget and cancellation token; `JsonReader.Skip(context)` does not consume siblings' budgets.
* `JsonConverterAttribute` can override converter selection for a type, property, or constructor parameter.
* A member of type `UnusedDataPacket` captures unknown JSON members as raw JSON text for forward-compatible round-tripping, without an extension-data attribute.
* Deserialization can optionally skip comments and accept trailing commas.
* Asynchronous stream and pipe APIs process built-in objects and collections incrementally; custom converters using the default asynchronous hooks buffer their individual values.
* UTF-8 buffers, sequences, and caller-owned pipes deserialize the first value; caller pipes retain unread prefetched bytes. Synchronous streams return the first value after buffering through EOF.
* String and asynchronous stream deserialization remain strict single-document operations. Use a caller-owned pipe for repeated first-value reads; temporary async-stream pipes cannot safely preserve read-ahead for the next operation.
* Operation failures use `JsonSerializationException` and retain their causal exception or existing error code; argument validation and actual cancellation remain distinct.
* Cyclic back-references require early registration of mutable instances; immutable or constructor-bound objects cannot be back-referenced while under construction.
* Targeted deserialization and sequence streaming do not support reference preservation.

## Status

The implemented feature surface is described in the [feature guide](docfx/docs/features.md), with dedicated guides for streaming, unions, schema generation, reference preservation, and MVC integration.
The public-API audit is complete for this checkpoint, with remaining low-level breadth explicitly deferred; this is not a claim of perfect binary parity with Nerdbank.MessagePack.
Dictionary keys are limited to supported simple types, collections without construction support are serialization-only, and getter-only scalar or immutable properties require constructor binding to be populated.

Full platform validation of source `7ea67c8` (2026-10-21) passes on Windows and WSL Ubuntu 26.04: **4,129 Windows and 3,191 Linux managed tests**, with zero failures or skips, including all 163 analyzer tests on each OS.
On each OS, NativeAOT core tests on .NET 9 and 10 total **1,892 passes, zero failures, and four known platform-input skips**; all four native fixtures publish with warnings as errors and execute, the F# self-check passes, and native image guards pass without recalibrating budgets.
Only NativeAOT creation of non-zero-lower-bound array inputs skips; managed normalization assertions remain intact.
The final source also passed DocFX with zero warnings/errors and formatting verification.
MVC retains strict single-JSON-body validation.
String tokens enforce JSON lexical grammar rather than Unicode well-formedness: CLR reads use UTF-8 replacement fallback and preserve `\uXXXX` code units, including lone surrogates.
Raw JSON and unescaped UTF-8 views preserve source bytes; UTF-8 string writes escape quotes, backslashes, and controls without validating non-ASCII payloads, while CLR writes escape lone surrogates.

Matched Medium performance evidence, causal fixes, exact native sizes, and artifact locations are recorded in [TODO.md](TODO.md) and the [benchmark guide](docfx/docs/benchmarking.md).
Original object allocation parity is preserved, but full speed restoration is **not** claimed: final Windows small/wide reads remain about 12%/18% slower than the accepted pre-low-level reader.
macOS has **not** been executed; the [platform validation guide](docfx/docs/platform-validation.md) provides the ready-to-run script and standalone Copilot handoff.
In a dedicated checkout of this branch on the Mac, run `pwsh -NoProfile -File ./tools/Validate-Platform.ps1 -ArtifactDirectory ./obj/platform-macos` and return the complete evidence directory without changing size budgets.

Segmented parsing, owned raw values, UTF-8 string/token APIs, scalar `JsonPrimitives`, and NBJson001/NBJson002/NBJson031/NBJson051 are implemented.
Stateless decoded strings, raw/container framing, dedicated narrow-number overloads, and writer `GetSpan`/`Advance` remain deliberately outside the implemented contract.
Experimental duck typing validates candidates against object contract metadata but remains an experimental selection strategy.
Async-stream first-value reads cannot safely retain prefetch; use caller-owned pipes.
See [TODO.md](TODO.md) for final evidence, constraints, and superseded checkpoints.
