# Nerdbank.Json feature backlog

This punch list compares the user-visible capabilities of the neighboring
`Nerdbank.MessagePack` repository with Nerdbank.Json as of August 2026. It
includes features that are useful for JSON and are not already implemented
here. Items are ordered by dependency and implementation risk.

Every item must preserve NativeAOT and trimming safety on the default path,
must not use `InternalsVisibleTo`, and must include focused tests, docfx
documentation, and samples where a runnable example adds value.

## Punch list

- [x] **Serialization lifecycle callbacks.**
  Provide a JSON-specific callback interface that notifies an object immediately
  before serialization and immediately after deserialization. This is useful for
  validation, computed state, and restoring invariants. Callbacks must participate
  in generated object converters without reflection, run exactly once per object
  occurrence, and have documented ordering relative to constructors, properties,
  extension data, and reference tracking. Unlike MessagePack, the public contract
  should avoid callbacks that expose partially initialized objects unless a
  compelling scenario requires them.

- [x] **Per-member collection comparer selection.**
  Add an attribute-driven way to select an `IEqualityComparer<T>` or
  `IComparer<T>` for a collection property or constructor parameter. The
  serializer already has a global secure comparer provider, but cannot preserve
  member-specific semantics such as case-insensitive dictionaries when it creates
  the collection. Activation must use source-generated type shapes rather than
  unconstrained runtime reflection. Existing getter-only collection initializers
  remain the preferred zero-configuration alternative.

- [x] **Rectangular multidimensional arrays.**
  Support rank-2 and higher rectangular arrays using nested JSON arrays. JSON has
  no rectangular-array metadata, so deserialization must reject ragged input and
  infer dimensions before allocating the final array. Unlike MessagePack, a
  flattened representation is not appropriate because it would require
  non-standard dimension metadata and reduce interoperability. Enforce depth,
  cancellation, integer-overflow, and allocation limits while measuring the
  additional buffering required to infer dimensions.

- [x] **Cyclical reference preservation.**
  Extend `$id`/`$ref` reference preservation to allow cycles, matching
  MessagePack's opt-in `AllowCycles` mode. The current `RejectCycles` mode remains
  valuable and behavior-compatible. Deserialization must register mutable objects
  before their members are populated; immutable constructor-bound objects cannot
  generally participate in backward references and must fail clearly. Metadata
  names must remain collision-safe and the feature stays opt-in because it changes
  the JSON wire shape.

- [x] **Runtime union configuration and discriminator-free unions.**
  Allow callers to add, replace, disable, or extend generated union definitions
  using explicitly supplied `ITypeShape` instances so the feature remains
  NativeAOT-safe. Also support an opt-in duck-typing strategy for legacy JSON that
  lacks discriminators, selecting a case from unique required property names.
  Duck typing is inherently slower, must reject ambiguous inputs, and cannot use
  optional properties as reliable evidence. JSON's conventional object
  discriminator should be considered alongside the existing two-element envelope
  before freezing the API; compatibility with the current representation is
  required by default.

- [x] **JSON Schema export.**
  Generate a JSON Schema document from the effective converter graph and serializer
  configuration. This is more directly applicable to JSON than MessagePack's
  schema projection and should describe naming policies, required and nullable
  members, enums, collections, dictionaries, unions, extension data, and reference
  metadata. Custom converters need an overridable schema hook; otherwise the
  exporter must emit an explicit warning/annotation rather than pretend to know
  their representation. Schema generation must be shape-driven and NativeAOT-safe.

- [x] **Targeted deserialization.**
  Add strongly typed path-based APIs that skip unrelated JSON and deserialize only
  a requested property, index, or dictionary value. Reuse the converter graph so
  naming policies, custom converters, and union envelopes remain correct.
  Expression-based paths are ergonomic but expression compilation is not
  NativeAOT-safe; parsing expression trees without compiling them is acceptable.
  A lower-level pre-parsed path API should avoid repeated expression analysis and
  allocations. Missing paths need an explicit throw-versus-default policy.

- [ ] **True asynchronous and incremental stream I/O.**
  Replace full-payload buffering in stream overloads with `PipeReader`/`PipeWriter`
  based incremental operation, and expose pipe overloads where useful. Custom
  converters need async hooks with correct default buffering behavior and a way to
  advertise optimized async implementations. JSON tokens can cross arbitrary
  buffer boundaries, so the reader must preserve partial UTF-8 strings, escapes,
  numbers, comments, and delimiters without copying whole documents. Cancellation,
  backpressure, stream ownership, and trailing-data behavior must be explicit.

- [ ] **Asynchronous sequence streaming.**
  Build on incremental I/O to return `IAsyncEnumerable<T>` for elements in a large
  top-level JSON array and, where practical, for newline-delimited JSON values.
  Also support streaming a sequence selected by a typed path inside an object
  envelope. Enumeration must leave the reader in a defined state on completion,
  cancellation, early disposal, malformed input, and absent paths. Unlike
  MessagePack's concatenated-value mode, newline-delimited JSON requires explicit
  framing because adjacent top-level JSON values are otherwise ambiguous.

- [ ] **Optional untyped JSON and DOM converters.**
  Provide opt-in converters for `object`, `ExpandoObject`, and common JSON DOM
  types so callers can process data without a static model. The default library
  must not root `System.Text.Json` or reflection-heavy dynamic support. Put these
  behind explicit extension methods and, where needed, feature switches. The
  native untyped representation should retain JSON number fidelity and raw
  fragments, impose property-count and depth limits, and document that
  `ExpandoObject` construction can exhibit quadratic behavior.

- [ ] **ASP.NET Core MVC formatters.**
  Add a separate NativeAOT-compatible integration package with JSON input and
  output formatters backed by Nerdbank.Json and an explicitly supplied
  source-generated `ITypeShapeProvider`. This is useful when applications want
  Nerdbank.Json semantics rather than ASP.NET Core's built-in System.Text.Json
  formatter. It must not replace framework defaults implicitly, and should
  support media-type selection, cancellation, problem reporting, and streaming
  bodies once incremental I/O is available.

- [ ] **Analyzer coverage for the new contracts.**
  Add diagnostics and, where safe, code fixes for invalid callback declarations,
  incompatible comparer attributes, malformed runtime union registrations,
  async converter lifetime mistakes, and custom converters that omit schema or
  targeted-navigation support. Each diagnostic needs documentation and tests.
  Analyzer rules should only require optional hooks when the corresponding
  capability is actually used.

## Compared features intentionally not copied

- MessagePack extension codes, raw MessagePack values, integer property keys,
  flattened multidimensional arrays, compression choices, and binary GUID/date
  encodings are wire-format-specific and do not belong in an interoperable JSON
  serializer.
- MessagePack's structural equality and collision-resistant comparer APIs are
  format-neutral and are already available through the existing
  Nerdbank.MessagePack dependency used for secure collection construction.
  Duplicating them in another namespace would create two competing public APIs.
- Forward-compatible unknown-property retention, surrogates, constructor-bound
  immutable types, state carried through `SerializationContext`, strict required
  member handling, default-value policies, naming policies, custom converter
  factories, reference preservation without cycles, and generated type-shape
  overloads already exist in Nerdbank.Json.
- A custom SignalR JSON protocol would collide with SignalR's standardized,
  built-in JSON protocol and offer little value. Godot converters are principally
  useful for MessagePack's non-JSON representations; JSON applications can add
  ordinary custom converters without a dedicated package.
- Reflection-based contractless serialization and migration analyzers for other
  serializers are not core defaults. A future reflection provider may be offered
  only through an explicit, trimming-unsafe opt-in if demand justifies it.
