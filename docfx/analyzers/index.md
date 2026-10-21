# Nerdbank.Json analyzers

The `Nerdbank.Json` package ships C# analyzers that flag invalid or unsafe usage at compile time.
Diagnostics include errors, warnings, and informational guidance.

| Rule ID | Category | Severity | Summary |
|---------|----------|----------|---------|
| [NBJson001](NBJson001.md) | Usage | Warning | Delegate types are not supported; descriptor reserved but analysis is not implemented |
| [NBJson002](NBJson002.md) | Usage | Warning | Unsupported dictionary key types; descriptor reserved but analysis is not implemented |
| [NBJson003](NBJson003.md) | Usage | Warning | `UseComparer` must target a dictionary or set with a compatible comparer or provider property |
| [NBJson020](NBJson020.md) | Usage | Error | Attribute converter must be concrete and compatible with the decorated value type |
| [NBJson021](NBJson021.md) | Usage | Error | Attribute converter requires a public parameterless constructor |
| [NBJson030](NBJson030.md) | Usage | Warning | Do not construct or call the top-level serializer from a converter |
| [NBJson032](NBJson032.md) | Usage | Info | Concrete converters should describe their JSON schema |
| [NBJson033](NBJson033.md) | Usage | Error | Return the `JsonWriter` before awaiting, calling the `JsonAsyncWriter` again, or returning |
| [NBJson034](NBJson034.md) | Usage | Error | Do not reuse a `JsonWriter` after returning it |
| [NBJson035](NBJson035.md) | Usage | Error | Return the `JsonReader` before awaiting, calling the `JsonAsyncReader` again, or returning |
| [NBJson036](NBJson036.md) | Usage | Error | Do not reuse a `JsonReader` after returning it |
| [NBJson037](NBJson037.md) | Usage | Warning | Async converters should override `PreferAsyncSerialization` |
| [NBJson050](NBJson050.md) | Usage | Warning | Pass `JsonReader` and `JsonWriter` parameters by `ref` |
| [NBJson110](NBJson110.md) | Usage | Warning | Describe initialized serializable defaults with `DefaultValueAttribute` |

## Converter and contract guidance

[NBJson020](NBJson020.md) and [NBJson021](NBJson021.md) validate attribute-selected converter types before runtime activation.
[NBJson030](NBJson030.md) directs converter authors to <xref:Nerdbank.Json.SerializationContext.GetConverter*> for nested values.
[NBJson032](NBJson032.md) encourages schema customization without treating an undocumented format as an error.
[NBJson110](NBJson110.md) identifies initialized defaults that shape metadata does not describe.
Its code fix adds a matching <xref:System.ComponentModel.DefaultValueAttribute> for supported constant initializers without changing the initializer.

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
* **Targeted-navigation hooks** (<xref:Nerdbank.Json.JsonConverter.SkipToPropertyValue*> and <xref:Nerdbank.Json.JsonConverter.SkipToIndexValue*>, plus their async counterparts).
  The default implementation navigates plain objects and arrays; custom representations may need overrides.
  There is no statically invalid declaration to flag.
* **Runtime union configuration**. Unions are registered at runtime with explicitly supplied `ITypeShape` instances and
  generic helpers; ambiguity and insufficient evidence are validated at configuration time with precise exceptions. The
  registrations are values computed at runtime, so they are not statically analyzable.

These may be revisited if a precise, low-noise static signal is identified.
