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
- [ ] Update README and feature documentation to describe current functionality.

## Converter infrastructure

- [ ] Align converter and serialization contexts.
- [ ] Add collection-expression builder support and verify converter collection APIs.
- [ ] Align object-based async serialization and deserialization.
- [ ] Align early object registration for reference preservation.
- [ ] Align public non-generic converter APIs.
- [ ] Align converter lookup helpers.
- [ ] Align optional converter placement and selection.

## Contract semantics

- [ ] Align declared-default handling.
- [ ] Align comparer attributes and precedence.
- [ ] Align security settings and enforcement, retaining the approved comparer dependency.
- [ ] Align async-enumerable type shapes.

## Unions and schema

- [ ] Align MessagePack-style union configuration and contracts.
- [ ] Align schema public types and helpers while preserving JSON schema behavior.

## Navigation and streaming

- [ ] Align targeted deserialization as `DeserializePath`.
- [ ] Align targeted sequence streaming as `DeserializePathEnumerableAsync`.
- [ ] Align sequence streaming as `DeserializeEnumerableAsync` with an options struct/enum including `NDJson`, not a hardcoded NDJSON method name.
- [ ] Align converter navigation hooks.
- [ ] Align low-level reader/writer and streaming APIs.

## Platform

- [ ] Align MVC integration APIs.
- [ ] Add a NativeAOT code-size testing project and workflow.
- [ ] Add an F# sample.
- [ ] Add equivalent non-format-specific analyzers and code fixes.

## Integration QA

- [ ] Audit all remaining public APIs against the local reference and remove superseded APIs without aliases.
- [ ] Update affected samples and documentation after each API change.
- [ ] Verify Release build, all configured test frameworks, and NativeAOT compatibility.
- [ ] Compare performance and allocations with the baseline.
- [ ] Explain approved differences and authorized breaking/no-obsolete-alias policy in the final PR.

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
