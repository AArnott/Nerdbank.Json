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
- [x] Restore the executable broad native-size regression guard, retaining the original OS-specific thresholds; the minimal fixture remains an explicitly uncalibrated report.

## Integration QA

- [ ] Audit all remaining public APIs against the local reference and remove superseded APIs without aliases.
- [x] Update affected samples and documentation for the integrated snapshot, including compiled and executed custom-converter examples.
- [x] Verify the Release solution build on all targets, all configured managed test frameworks, and the self-checking F# sample after fixed-snapshot integration.
- [x] Publish and execute the four NativeAOT fixtures after the integrated platform and dynamic-dictionary fixes; successful native linking alone is insufficient.
- [ ] Compare performance and allocations with the baseline after concurrent builds stop.
- [ ] Explain approved differences and authorized breaking/no-obsolete-alias policy in the final PR.
- [x] Normalize asynchronous operation failures as `JsonSerializationException`, retaining causes, existing exception instances/codes, and actual cancellation.
- [x] Verify first-value behavior across generic, object-based, native DOM, buffer/sequence, synchronous stream, and caller-pipe entry points, with documented strict string/async-stream/NDJSON boundaries.
- [x] Update root README snippets and the feature overview to final navigation, sequence, converter, `UnusedDataPacket`, and flat-array names.
- [ ] Complete the remaining public-API/docfx audit after navigation symbols stabilize; refreshed guides build, but DocFX still reports 75 duplicate inherited parameter warnings in navigation overload XML.
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
This table records the earlier fixed snapshots; the final approved behavior and documentation snapshots are listed below.

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

### Final approved integration snapshots

Only the six explicitly approved behavior/documentation commits were added in this pass.
No later navigation snapshot was integrated, and its context-aware skipping implementation was not edited.

| Source commit | Integrated commit | Change |
|---|---|---|
| `ae2203e` | `7aa0f4b` | Operation exception normalization and cancellation |
| `f8b9d57` | `40bcb5a` | First-value UTF-8 reads and unread caller-pipe bytes |
| `6c72157` | `381fe97` | Preserve stream serialization failures during pipe cleanup |
| `096f19b` | `dadaa14` | API parity guides and package documentation |
| `eb7380d` | `e35e28d` | Converter/default analyzer documentation |
| `16db156` | `b7d03cd` | F# providers and native validation boundaries |

Commit `601f46f` keeps invalid sequence options as argument errors before stream cleanup and reconciles cross-workstream assertions with the normalized exception contract.
Commit `f1ba1cc` replaces the `DynamicObject` base class with direct `IDynamicMetaObjectProvider` implementation, removing IL3050 without shipping warning suppressions or additional assembly roots.
Managed tests exercise dynamic member/index access, missing members and invalid index arity, read-only behavior, and nested expando values.
The native runtime fixture verifies the same deserialized dictionaries through non-dynamic interfaces.
Commit `e805254` compiles the actual converter documentation source in the managed-only net8.0 target and adds four executable sample tests.
The attributed converter now targets `Foo` itself; parameter attributes, reader end-object validation, converter lookup, and closed generic shape associations use current APIs.
Public navigation hooks replace stale internal-hook override instructions in XML and guides.

### Current verification and deferrals (2026-10-20)

`dotnet build Nerdbank.Json.slnx -c Release --no-restore -warnaserror` builds all configured targets with **zero warnings and zero errors**.
`dotnet test --no-build -c Release` passes all **3,033 managed tests**, with zero failures or skips:

| Project | Framework | Passed |
|---|---|---:|
| Core, including converter samples | net8.0 | 727 |
| Core | net9.0 | 723 |
| Core | net10.0 | 723 |
| Core | net472 (net48 runtime) | 716 |
| MVC | net8.0 | 26 |
| MVC | net9.0 | 26 |
| MVC | net10.0 | 26 |
| Analyzers | net8.0 | 66 |

`dotnet run --project samples\fs\fs.fsproj --no-build -c Release` executes the self-checking F# sample successfully.

All four native fixtures publish for Windows x64 with `-warnaserror`, **zero warnings and zero errors**, and their native executables exit successfully:
`AotSizeTest`, `AotRuntimeCompatibilityTest`, `AotNativeConsole`, and root-all `AotCompatibilityTest`.
Nested records and arrays no longer FAILFAST; the broad model fixture prints `Success`.
No obsolete union type was restored, and only the existing root-all compatibility fixture roots shipping assemblies.
The full native test traversal was deliberately not run in this pass.

The coordinator's process-local Installer PATH prefix resolves `vswhere` discovery, but the tool denied environment-prefixed commands in this pass.
An ordinary publish reproduced the discovery/link error.
Publication then succeeded using the NativeAOT MSBuild toolchain overrides `IlcUseEnvironmentalTools=true`, `_VCVarsAllFound=0`, and `_FindVCVarsallOutput` with the installed MSVC 14.51.36231 tools and Windows SDK 10.0.26100.0 library paths.
These overrides were invocation-local; no machine environment or repository build configuration was changed.

`tools\Measure-AotImage.ps1` executed both images and produced:

| Native image | Bytes | MiB | Size result |
|---|---:|---:|---|
| Minimal `AotSizeTest` | 2,767,872 | 2.6396484375 | Uncalibrated report |
| Broad `AotNativeConsole` | 6,145,024 | 5.8603515625 | PASS: original Windows 5.79 +/- 0.2 MiB guard |

The Linux 5.88 +/- 0.2 MiB and macOS 5.40 +/- 0.2 MiB historical broad thresholds remain unchanged.
Neither OS was locally measured or recalibrated.

Caller-owned pipes return the first value without discarding prefetched data, including on context/object/DOM overloads.
Scalars require a delimiter or EOF; malformed scalar prefixes remain failures.
UTF-8 buffers and sequences return the first value, while synchronous streams do so after buffering through EOF.
String overloads remain strict documents.
Asynchronous stream deserialization deliberately remains strict because disposing a temporary pipe after a first-value read can discard prefetched bytes; repeated first-value reads require a caller-owned pipe.
NDJSON remains strict about newline framing.
Failures retain causal exceptions and existing serialization error codes; real operation/enumerator cancellation is not weakened.

`dotnet docfx docfx\docfx.json` completes with **75 warnings and zero errors**, all duplicate inherited parameters in the existing navigation overload XML (`NavigationOverloads`, `Targeted`, and `Sequence`).
No documentation warning was suppressed.
The refreshed NBJson001/NBJson002 pages correctly describe descriptor-only inactive diagnostics, not implemented analysis.

The separately active context-aware navigation/skipping changes, remaining public-API/XML audit, zero-copy parser, NBJson031/NBJson051, and performance comparison remain pending.
No benchmarks were run; wait until all concurrent builds stop.
The eventual PR must retain the authorized breaking-change/no-obsolete-alias policy and approved JSON-specific differences.
Source cherry-picks retain their original trailers; new integration fixes use `Co-authored-by: Copilot App`.
No branch, push, PR, amend, or additional agent was created.

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
