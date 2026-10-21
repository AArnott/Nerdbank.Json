# API parity work

Mirror the APIs in local `G:\src\nerdbank.messagepack` (read-only), except for the approved differences below.
Breaking API cleanup is authorized: remove superseded APIs without obsolete aliases.
Commit each logical step; the eventual PR must explain this breaking-change/no-obsolete-alias policy.

## Approved differences and exclusions

* Keep the secure-comparer cross-library dependency.
* Keep camelCase as the JSON property-naming default.
* Keep JSON-specific comments, indentation, NDJSON, native JSON DOM, and reference metadata.
* Keep string-returning default `Serialize`, alongside direct UTF-8 buffer and stream APIs.
* Do not add MessagePack extensions, indexed object contracts, Godot, or SignalR.

## Bootstrap

- [x] Inspect the implemented source surface and establish this tracker.
- [x] Update README and feature documentation to describe current functionality.

## Converter infrastructure

- [x] Align converter and serialization contexts.
- [x] Add collection-expression builder support and verify converter collection APIs.
- [x] Align object-based async serialization and deserialization.
- [x] Align early object registration for reference preservation.
- [x] Align public non-generic converter APIs.
- [x] Align converter lookup helpers.
- [x] Align optional converter placement and selection.

## Contract semantics

- [x] Align declared-default handling.
- [x] Align comparer attributes and precedence.
- [x] Align security settings and enforcement, retaining the approved comparer dependency.
- [x] Align async-enumerable type shapes.
- [x] Integrate the fixed contracts followup for `UnusedDataPacket` retention and flat-array contracts.

## Unions and schema

- [x] Align MessagePack-style union configuration and contracts.
- [x] Align schema public types and helpers while preserving JSON schema behavior.

## Navigation and streaming

- [x] Align targeted deserialization as `DeserializePath` with nested `DeserializePathOptions`.
- [x] Align targeted sequence streaming as `DeserializePathEnumerableAsync` with `StreamingEnumerationOptions` and explicit pipe ownership.
- [x] Align sequence streaming as `DeserializeEnumerableAsync` with `SequenceOptions` and `JsonSequenceFormat.NDJson` / `JsonArray`.
- [x] Align public converter navigation hooks as `SkipToPropertyValue` / `SkipToIndexValue` and their asynchronous counterparts.
- [x] Align low-level typed numeric reader methods and scalar writer `Write` / `WriteNull` / `WriteRaw` APIs.

## Platform

- [x] Align MVC integration APIs.
- [x] Add a NativeAOT code-size reporting project and workflow.
- [x] Add an F# sample.
- [x] Integrate non-format-specific converter/default/ref-parameter analyzers and default-initializer code fixes.
- [ ] Restore an equivalent executable native-size regression guard after measuring/calibrating image sizes; report-only minimal fixtures do not replace the removed existing broad-image threshold check.

## Integration QA

- [ ] Audit all remaining public APIs against the local reference and remove superseded APIs without aliases.
- [ ] Update affected samples and documentation after each API change.
- [x] Verify the Release solution build on all targets, all configured managed test frameworks, and the self-checking F# sample after fixed-snapshot integration.
- [ ] Verify NativeAOT runtime compatibility after the separate platform fix; successful native linking alone is insufficient.
- [ ] Compare performance and allocations with the baseline after concurrent builds stop.
- [ ] Explain approved differences and authorized breaking/no-obsolete-alias policy in the final PR.
- [ ] Audit asynchronous error wrapping against synchronous `JsonSerializationException` behavior; current async converter format failures remain unwrapped.
- [ ] Audit trailing-data rejection consistently across generic, object-based, native DOM, stream, and pipe entry points.
- [x] Update root README snippets and the feature overview to final navigation, sequence, converter, `UnusedDataPacket`, and flat-array names.
- [ ] Complete the separate docfx/API audit after symbols stabilize; detailed guides outside the overview have not been audited.
- [ ] Implement the deferred zero-copy parser work.
- [ ] Address deferred analyzer rules NBJson031 and NBJson051.

## Fixed-snapshot integration checkpoint

The coordinator branch remains `aarnott-miniature-engine`; no branch was created or renamed, and nothing was pushed.
Contracts through `6014701`, platform through original `135e9bf` (integrated `f744a3f`), the seven foundations commits in `9cd49c2..05ec767` (integrated through `04df7ba`), and the five unions/schema commits in `9cd49c2..78c1a8f` (integrated through `477c28d`) are integrated as individual logical commits.
These earlier integrations were verified at `89e70ae` with 2,137 passing managed tests.

Integration fixes in `1664183` migrate new converter overrides, registrations, analyzer fixtures, and native fixture usage to the combined API, and dispose the custom-converter test reader before completing its pipe.
Conflict resolutions retain `UseComparer` NBJson003 and every added analyzer rule, both expando limits and duplicate detection, dynamic nested-object semantics, early custom reference registration, and JSON-specific reference envelopes.
Boundary coverage includes 128/129 expando properties and a lower general member limit on synchronous/asynchronous reads and writes.

Repeated integration testing exposed concurrent lazy cache replacement through the new converter lookup API.
Commit `e8140f6` atomically publishes serializer and shape caches and adds repeated parallel converter-identity coverage, retaining the initialized allocation-free lookup path.

### Additional fixed snapshots

Only the explicitly supplied snapshots below were integrated, preserving each source commit as a separate logical cherry-pick.
No foundations followup after `d48a85e`, additional platform commits, or separately active behavior QA changes were integrated.

| Source snapshot | Integrated commit | Change |
|---|---|---|
| `d48a85e` | `0720fef` | Runtime-object reference envelopes and dictionary/Expando backreferences |
| `4add4b9` | `b126b81` | `DeserializePath` names and options |
| `31faf52` | `3b8a2fd` | Typed streaming paths and pipe ownership |
| `478e3cf` | `ef4b037` | Generic sequence APIs and framing options |
| `2cb5814` | `3033aae` | Public converter property/index navigation hooks |
| `991ae8c` | `0b0a17d` | Typed numeric reads, scalar writes, sequence positions |
| `cc2b601` | `80bf3fe` | Complete generated/witness/legacy path overload matrix |
| `63c7d8e` | `6898762` | BOM/startup stall correction |
| `7dcef03` | `79de1bd` | JSON-local `UnusedDataPacket` member-type retention |
| `7f3a04a` | `b0e190c` | Flat multidimensional converter and schemas |

Conflict resolutions retain both union envelope formats and all workstream tests, `in T?` converter writes, public sealed object-method overrides, the STJ `JsonObject` schema API, and the atomic cache publication fix.
Flat schemas describe exactly `[[rank-sized dimension lengths], [flat elements]]`, with nonnegative Int32 dimensions, in all three supported dialects.
Packet tests retain unknown raw numeric spelling and opaque JSON reference text, private fields/properties exposed through `PropertyShape`, and getter-only packet semantics: supplied packets write, unknown input is skipped.
No superseded extension-data attribute references remain in compile sources, tests, or native fixtures; no obsolete aliases were added.

Focused reconciliation commit `1738627` migrates cross-workstream scalar writer callers, flat converter signatures, registrations, and schema tests.
Object-envelope union navigation now delegates to the public hooks with the active context, matching array envelopes.
Additional regression coverage exposed generated-union case construction bypassing registered custom converters; it now uses the existing custom-converter selector without changing native property dispatch or adding roots.
Both generated and runtime union cases are covered with custom wire names, synchronous serialization/deserialization/path selection, and incrementally read sequences for both envelopes.

### Current verification and deferrals

The Release solution builds all configured targets with **zero warnings and zero errors**.
All **2,721 managed tests pass**, with zero failures or skips:

| Project | Framework | Passed |
|---|---|---:|
| Core | net8.0 | 646 |
| Core | net9.0 | 646 |
| Core | net10.0 | 646 |
| Core | net472 (net48 runtime) | 639 |
| MVC | net8.0 | 26 |
| MVC | net9.0 | 26 |
| MVC | net10.0 | 26 |
| Analyzers | net8.0 | 66 |

The self-checking F# sample executes successfully.
Managed builds of the native fixtures are not native runtime validation.
The coordinator reports that a process-local PATH prefix of `C:\Program Files (x86)\Microsoft Visual Studio\Installer` unblocks native linking without `vcvars`.
However, the minimal native fixture still FAILFASTs in `JsonStandardVisitor.VisitProperty<AddressBook,?>` for `record AddressBook(Person[] People)`; a separate Native QA agent owns that fix.
No native execution or native-size calibration was performed in this integration pass, and the size regression guard remains unresolved.
No whole-library rooting or changes to the size fixture/workflow were introduced to hide the runtime failure.

Behavior QA for exception wrapping and complete input consumption remains separately active and unintegrated.
NativeAOT runtime/size guard work, the zero-copy parser, NBJson031/NBJson051, the full docfx audit, and performance comparison remain explicitly deferred.
No benchmarks were run while concurrent work may be building.
The two contract cherry-picks retain their source `Co-authored-by: Copilot` trailers (same account); other new integration commits use `Co-authored-by: Copilot App`. No existing commits were amended.

## Coordinator-provided baseline

Release solution build: 0 warnings/errors; 1,680 tests pass across all configured frameworks.
BenchmarkDotNet ShortRun `ObjectBenchmarks`, .NET 9.0.21:

| Operation | Mean | Allocated |
|---|---:|---:|
| Small deserialize | 298.4 ns | 200 B |
| Wide deserialize | 1051.8 ns | 664 B |
| Small serialize | 175.0 ns | 0 B |
| Wide serialize | 514.7 ns | 0 B |

Reports are retained outside the repository by the coordinator.
