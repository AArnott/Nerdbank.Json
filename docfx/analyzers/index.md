# Nerdbank.Json analyzers

The `Nerdbank.Json` package ships C# analyzers that flag invalid or unsafe usage at compile time. Diagnostics are
reported as warnings or errors depending on severity.

| Rule ID | Category | Severity | Summary |
|---------|----------|----------|---------|
| NBJson001 | Usage | Warning | Delegate-typed serializable members are not supported |
| NBJson002 | Usage | Warning | Unsupported dictionary key types |
| [NBJson003](../docs/collection-comparers.md) | Usage | Warning | `JsonCollectionComparer` must target a dictionary or set with a compatible comparer |
| [NBJson033](NBJson033.md) | Usage | Error | Return the `JsonWriter` before awaiting, calling the `JsonAsyncWriter` again, or returning |
| [NBJson034](NBJson034.md) | Usage | Error | Do not reuse a `JsonWriter` after returning it |
| [NBJson035](NBJson035.md) | Usage | Error | Return the `JsonReader` before awaiting, calling the `JsonAsyncReader` again, or returning |
| [NBJson036](NBJson036.md) | Usage | Error | Do not reuse a `JsonReader` after returning it |
| [NBJson037](NBJson037.md) | Usage | Warning | Async converters should override `PreferAsyncSerialization` |
| NBJson050 | Usage | Warning | Pass `JsonWriter`/ref structs by `ref` |

## Async converter safety (NBJson033–NBJson037)

Custom converters that override `ReadAsync`/`WriteAsync` interact with the `JsonAsyncReader`/`JsonAsyncWriter` rental
APIs. A rented `JsonReader` (from `CreateBufferedReader`) or `JsonWriter` (from `CreateWriter`) is a `ref struct` that
must be handed back with `ReturnReader`/`ReturnWriter` before the owning async reader/writer is used again, before the
next `await`, and before the method returns. Using a rental after returning it, or holding one across an `await`, leads
to corruption or a runtime failure. These analyzers catch those mistakes statically.

Because a `ref struct` can never survive an `await`, the "must return before await/exit" rules (NBJson033/NBJson035) are
intentionally suppressed for methods that return the rental inside a `finally` clause: that shape is always safe and
would otherwise produce false positives.

## Explicit non-rules

The following behaviors were assessed and intentionally have **no** analyzer rule, because they are runtime concerns
that cannot be diagnosed precisely and statically without excessive false positives:

* **Lifecycle callbacks** (serialization callback interfaces/hooks). Whether a callback runs, its ordering relative to
  reference registration, and reentrancy are runtime behaviors. There is no statically invalid declaration to flag
  beyond what the compiler already enforces.
* **Custom schema hook** (`JsonConverter.GetJsonSchema`). A converter that does not override it produces a permissive
  ("any") schema, which is a valid, conservative default rather than an error. Requiring an override on every converter
  would be high-noise, so schema completeness is validated by the JSON Schema export tests instead of an analyzer.
* **Targeted-navigation hook** (`JsonConverter.TryNavigate*`). Not overriding it simply means expression/path navigation
  cannot traverse *through* that converter, which is reported clearly at runtime. There is no statically invalid usage.
* **Runtime union configuration**. Unions are registered at runtime with explicitly supplied `ITypeShape` instances and
  generic helpers; ambiguity and insufficient evidence are validated at configuration time with precise exceptions. The
  registrations are values computed at runtime, so they are not statically analyzable.

These may be revisited if a precise, low-noise static signal is identified.
