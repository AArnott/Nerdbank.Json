# Nerdbank.Json.AspNetCoreMvcFormatter

ASP.NET Core MVC input and output formatters backed by [Nerdbank.Json](https://github.com/AArnott/Nerdbank.Json),
enabling source-generated JSON in controller-based Web APIs, including apps using the minimal hosting model.
This package supplies MVC formatters, not Minimal API JSON integration.

Unlike the built-in `System.Text.Json` formatters, these formatters:

* use an **explicitly supplied**, source-generated `ITypeShapeProvider` — no reflection and no runtime type discovery;
* stream request and response bodies incrementally through Nerdbank.Json's `PipeReader`/`PipeWriter` APIs;
* are trimming- and NativeAOT-safe.

## Usage

Call `AddNerdbankJsonFormatters(serializer, shapeProvider, configure)` on the builder returned by `AddControllers` or `AddMvcCore`.
The extension methods are in `Microsoft.Extensions.DependencyInjection`.
Supply a source-generated `ITypeShapeProvider` that describes every type the formatters can read or write.
See the [compilable MVC sample](https://github.com/AArnott/Nerdbank.Json/blob/main/samples/AspNetMvc/Program.cs) and the [formatter guide](https://aarnott.github.io/Nerdbank.Json/docs/aspnetcore-mvc.html).

For direct registration, `JsonInputFormatter` and `JsonOutputFormatter` are in the `Nerdbank.Json.AspNetCoreMvcFormatter` namespace.
Their constructors accept the provider first and the serializer second.
Provider-only constructors use a default serializer.

By default the Nerdbank.Json formatters are inserted **before** the built-in `System.Text.Json` formatters (so they win
content negotiation for `application/json` and `+json` media types) but do not remove them.
Set `ReplaceSystemTextJsonFormatters` in the registration callback to remove the built-in JSON formatters.
Set `InsertFirst` to `false` to append rather than prepend the Nerdbank.Json formatters.
Both options are properties of `NerdbankJsonFormatterOptions`.

During content negotiation, the formatters decline model types the provider cannot describe, allowing other registered formatters to handle types such as `ProblemDetails`.
If a formatter is invoked directly for an unsupported type, it throws `MissingTypeShapeException` rather than silently binding `null` or using reflection.
When replacing the built-in JSON formatters, include all response and error types in the provider.
