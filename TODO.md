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
- [ ] Integrate the pending contracts followup for `UnusedDataPacket` retention and flat-array contracts.

## Unions and schema

- [x] Align MessagePack-style union configuration and contracts.
- [x] Align schema public types and helpers while preserving JSON schema behavior.

## Navigation and streaming

- [ ] Align targeted deserialization as `DeserializePath`.
- [ ] Align targeted sequence streaming as `DeserializePathEnumerableAsync`.
- [ ] Align sequence streaming as `DeserializeEnumerableAsync` with an options struct/enum including `NDJson`, not a hardcoded NDJSON method name.
- [ ] Align converter navigation hooks.
- [ ] Align low-level reader/writer and streaming APIs.

## Platform

- [x] Align MVC integration APIs.
- [x] Add a NativeAOT code-size reporting project and workflow.
- [x] Add an F# sample.
- [x] Integrate non-format-specific converter/default/ref-parameter analyzers and default-initializer code fixes.
- [ ] Restore an equivalent executable native-size regression guard after measuring/calibrating image sizes; report-only minimal fixtures do not replace the removed existing broad-image threshold check.

## Integration QA

- [ ] Audit all remaining public APIs against the local reference and remove superseded APIs without aliases.
- [ ] Update affected samples and documentation after each API change.
- [ ] Verify Release build, all configured test frameworks, and NativeAOT compatibility.
- [ ] Compare performance and allocations with the baseline.
- [ ] Explain approved differences and authorized breaking/no-obsolete-alias policy in the final PR.
- [ ] Audit asynchronous error wrapping against synchronous `JsonSerializationException` behavior; current async converter format failures remain unwrapped.
- [ ] Audit trailing-data rejection consistently across generic, object-based, native DOM, stream, and pipe entry points.
- [ ] Complete the documentation/API audit after navigation and retention followups are integrated; the root README converter snippets are migrated, but retention snippets await their final API.

## Fixed-snapshot integration checkpoint

The coordinator branch remains `aarnott-miniature-engine`; no branch was created or renamed, and nothing was pushed.
Contracts through `6014701`, platform through original `135e9bf` (integrated `f744a3f`), the seven foundations commits in `9cd49c2..05ec767` (integrated through `04df7ba`), and the five unions/schema commits in `9cd49c2..78c1a8f` (integrated through `477c28d`) are integrated as individual logical commits.
No later foundations commits or active navigation/contracts-followup snapshots have been integrated.

Integration fixes in `1664183` migrate new converter overrides, registrations, analyzer fixtures, and native fixture usage to the combined API, and dispose the custom-converter test reader before completing its pipe.
Conflict resolutions retain `UseComparer` NBJson003 and every added analyzer rule, both expando limits and duplicate detection, dynamic nested-object semantics, early custom reference registration, and JSON-specific reference envelopes.
Boundary coverage includes 128/129 expando properties and a lower general member limit on synchronous/asynchronous reads and writes.

Repeated integration testing exposed concurrent lazy cache replacement through the new converter lookup API.
Commit `e8140f6` atomically publishes serializer and shape caches and adds repeated parallel converter-identity coverage, retaining the initialized allocation-free lookup path.
The Release solution builds all configured targets with zero warnings/errors, and all 2,137 managed tests pass with no skips: core tests on net8.0/net9.0/net10.0/net472 (net48 runtime), MVC tests on net8.0/net9.0/net10.0, and analyzer tests on net8.0.
The self-checking F# sample also executes successfully after integration.

Native publishing and native-size calibration have not been performed at this checkpoint.
For the later native run, the installed `C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe` locates VS 18 Enterprise; import its `vcvars64.bat` in the same `cmd.exe` process as `dotnet publish`, without system-wide installation or PATH changes.
Performance comparison, native validation, async failure/trailing-data QA, navigation/retention integration, and the final docs audit remain open.

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
