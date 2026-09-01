# Nerdbank.Json.AspNetCoreMvcFormatter

ASP.NET Core MVC input and output formatters backed by [Nerdbank.Json](https://github.com/AArnott/Nerdbank.Json),
enabling AOT-safe, source-generated JSON in MVC and minimal-hosting Web APIs.

Unlike the built-in `System.Text.Json` formatters, these formatters:

* use an **explicitly supplied**, source-generated `ITypeShapeProvider` — no reflection and no runtime type discovery;
* stream request and response bodies incrementally through Nerdbank.Json's `PipeReader`/`PipeWriter` APIs;
* are trimming- and NativeAOT-safe.

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using Nerdbank.Json;
using PolyType;

var serializer = new JsonSerializer();

builder.Services
    .AddControllers()
    .AddNerdbankJsonFormatters(serializer, MyWitness.GeneratedTypeShapeProvider);
```

`MyWitness` is any type (often a witness) that lists your model types via `[GenerateShape]` /
`[GenerateShapeFor<T>]`; its generated `ITypeShapeProvider` describes every type the formatters can read or write.

By default the Nerdbank.Json formatters are inserted **before** the built-in `System.Text.Json` formatters (so they win
content negotiation for `application/json` and `+json` media types) but do not remove them. To fully take over JSON
handling:

```csharp
.AddNerdbankJsonFormatters(serializer, provider, o => o.ReplaceSystemTextJsonFormatters = true);
```

If the provider has no shape for a model type, the formatters throw a `MissingTypeShapeException` rather than silently
binding `null` or falling through, so misconfiguration is obvious.
