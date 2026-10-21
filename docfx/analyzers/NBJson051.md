# NBJson051: Prefer the modern .NET API

Category: **Usage**. Default severity: **Warning**.

A call uses an API marked by Nerdbank.Json as having a preferred modern .NET alternative, and the target reference assemblies support static virtual interface members (`RuntimeFeature.VirtualStaticsInInterfaces`).
.NET Standard and .NET Framework targets are not diagnosed.

Replace dynamic `AddSourceGenerated` shape resolution with compile-time constrained calls:

[!code-csharp[](../../samples/cs/AnalyzerGuidance.cs#ModernShapeMapping)]

These alternatives verify shape availability at compile time and avoid dynamic resolution.
Add `[GenerateShape]` to the type, or `[GenerateShapeFor<Derived>]` to the witness, as appropriate.
Explicit `ITypeShape<T>` and `ITypeShapeProvider` overloads remain valid, including for reflection and NativeAOT-compatible custom providers; they are not diagnosed.

The internal `Nerdbank.Json.PreferDotNetAlternativeApiAttribute` carries the advice in library metadata.
It is not a public user annotation.
The rule compares the actual marker symbol, not similarly named methods or attributes.

Legacy serializer extensions that are already `[Obsolete(..., error: true)]` on modern .NET remain compiler errors, not an additional NBJson051 warning.
The analyzer also excludes hard-obsolete marked members and containing types.
There is no automatic fix: changing the method name alone is unsafe if the type or witness does not satisfy the new constraints.
Suppress a reviewed intentional dynamic call using `#pragma warning disable NBJson051` or `.editorconfig`.
