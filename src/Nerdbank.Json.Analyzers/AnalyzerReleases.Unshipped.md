; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NBJson001 | Usage | Warning | Delegate-typed serializable members are not supported
NBJson002 | Usage | Warning | Unsupported dictionary key types are not supported
NBJson003 | Usage | Warning | JsonCollectionComparer attribute must target a dictionary or set with a compatible comparer type
NBJson033 | Usage | Error | Async converters should return the JsonWriter before awaiting, calling the JsonAsyncWriter again, or returning
NBJson034 | Usage | Error | Async converters should not reuse a JsonWriter after returning it
NBJson035 | Usage | Error | Async converters should return the JsonReader before awaiting, calling the JsonAsyncReader again, or returning
NBJson036 | Usage | Error | Async converters should not reuse a JsonReader after returning it
NBJson037 | Usage | Warning | Async converters should override PreferAsyncSerialization
NBJson050 | Usage | Warning | JsonWriter parameters should be passed by ref
