# ASP.NET Core MVC formatters

The `Nerdbank.Json.AspNetCoreMvcFormatter` package provides MVC input and output formatters backed by
Nerdbank.Json, so controller-based Web APIs can read and write JSON with an explicitly supplied,
source-generated `ITypeShapeProvider` — no reflection, and without rooting `System.Text.Json`.

This is useful when you want Nerdbank.Json semantics (its converters, naming policies, security limits,
reference handling, unions, and so on) in an MVC app, and when you want a trimming- and NativeAOT-friendly
JSON pipeline.

## Registration

[!code-csharp[](../../samples/AspNetMvc/Program.cs#Registration)]

<xref:Microsoft.Extensions.DependencyInjection.NerdbankJsonMvcBuilderExtensions.AddNerdbankJsonFormatters*>
is available on both `IMvcBuilder` (from `AddControllers`) and `IMvcCoreBuilder` (from `AddMvcCore`). You
supply the <xref:Nerdbank.Json.JsonSerializer> and the source-generated `ITypeShapeProvider` that describes
every model type the API reads or writes.

## Coexistence with the built-in formatters

By default the Nerdbank.Json formatters are **inserted first** so they win content negotiation for
`application/json`, `text/json`, and `application/*+json`, but the built-in `System.Text.Json` formatters are
**left in place**. The formatters only claim types their provider can describe (via `CanReadType` /
`CanWriteType`), so framework types such as `ProblemDetails` continue to be handled by the built-in
formatters. This is important because the automatic `400` validation response produced by
`[ApiController]` serializes a `ValidationProblemDetails` that is typically not in your shape provider.

To make Nerdbank.Json the *only* JSON formatter, set
<xref:Nerdbank.Json.AspNetCore.NerdbankJsonFormatterOptions.ReplaceSystemTextJsonFormatters> to `true`. Do
this only when every serialized type — including any error/response types you return — is in your provider.
Use <xref:Nerdbank.Json.AspNetCore.NerdbankJsonFormatterOptions.InsertFirst> to control ordering.

## Behavior

* **Media types.** Both formatters register `application/json`, `text/json`, and the structured-suffix
  wildcard `application/*+json`.
* **Charsets.** UTF-8 and UTF-16 are supported. UTF-8 bodies stream directly through the request
  `PipeReader` / response `PipeWriter`; other encodings are transcoded to and from UTF-8.
* **Streaming.** Bodies are read and written incrementally with backpressure through Nerdbank.Json's async
  APIs — large payloads are not buffered in full.
* **Cancellation.** Both formatters honor <xref:Microsoft.AspNetCore.Http.HttpContext.RequestAborted>.
* **Empty input.** An empty request body yields no value (or the model's default when
  `TreatEmptyInputAsDefaultValue` is set).
* **Malformed input.** Parse failures are surfaced as model-state errors, producing a `400` response rather
  than a `500`.
* **Null output.** A `null` model is written as the JSON `null` literal.
* **Provider misses.** If the formatter is ever invoked for a type the provider cannot describe, it throws a
  <xref:Nerdbank.Json.AspNetCore.MissingTypeShapeException> that explains how to add the type to the
  provider — misconfiguration fails loudly instead of silently falling back to reflection.

## NativeAOT and trimming

The formatters use PolyType's `ITypeShape` visitor to bridge from a runtime `Type` into strongly typed
serialization without reflection or expression compilation, so they are trimming- and NativeAOT-safe when
paired with a source-generated provider.
