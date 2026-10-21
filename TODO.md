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

## Finalization policy (user-directed)

Keep the preferred MessagePack-aligned public API and finalize otherwise-ready work without waiting for complete performance recovery.
Continue tracking benchmark impact and applying measured, correctness-preserving mitigations; unresolved performance findings are non-blocking follow-ups, not API-finalization gates.
Log the affected scenario, measured time/allocations and uncertainty, suspected or confirmed source, attempted mitigation, and any genuine public-contract constraint.
Do not confuse a current hotspot with proof of the change that caused a regression.

The current public-contract review finds no identified regression that requires replacing the preferred API.

| Area | API constraint and optimization latitude |
|---|---|
| Object lookup/dispatch, converter caching, parser scanning, segmented access, scratch storage | Internal implementation choices; no required public API change has been identified. |
| By-value converter context | Preserves each converter's local depth/state semantics and matches the preferred model; internal readonly-reference dispatch already avoids some copies without changing public overrides. Further internal optimization is possible, but eliminating all public-boundary value-copy semantics is not promised. |
| CLR strings, contiguous decoded spans, independently owned raw results | Materializing the requested representation can inherently require decoding, copying, or allocation. Existing UTF-8 span/sequence, typed numeric, raw borrowed, and direct buffer APIs cover important alternatives without removing convenient APIs. |
| Future specialized fast paths | Add caller-buffer/ownership-aware APIs only when evidence warrants them; no known issue currently requires a new public escape hatch. Do not weaken lifetimes, context isolation, grammar, depth/cancellation, or exact-shape identity for speed. |

The remaining contiguous-read gap and segmented scratch latency tradeoffs stay logged in the current checkpoint and benchmark guide.
The rejected hash experiment remains rejected; unchanged public APIs do not prevent another measured implementation experiment later.
Independent user-Mac handoff execution remains separate from the CI evidence now recorded in the current macOS calibration checkpoint below.

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
- [x] Generate attributed converter constructor associations automatically, including open-generic converters, without manual associated-shape annotations.

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
- [x] Finalize MessagePack-style overloaded scalar `JsonPrimitives.TryRead`/`TryWrite`, including all 8- through 64-bit integer widths and single precision, without retaining replaced named readers.
- [x] Finalize direct writer `GetSpan`/`Advance` access for one complete trusted raw value, preserving JSON indentation, pending-property state, cancellation of reservations, clones, and async handoff.
- [x] Integrate context-aware `Skip(SerializationContext)` and raw capture, preserving remaining depth and cancellation through coordinator-only paths without a `SkipValue` alias.

## Platform

- [x] Align MVC integration APIs.
- [x] Add a NativeAOT code-size reporting project and workflow.
- [x] Add an F# sample.
- [x] Integrate non-format-specific converter/default/ref-parameter analyzers and default-initializer code fixes.
- [x] Restore the executable broad native-size regression guard, retaining the original OS-specific thresholds; the minimal fixture remains an explicitly uncalibrated report.

## Integration QA

- [x] Complete the public-API audit for this checkpoint and remove superseded APIs without aliases; remaining low-level breadth is explicitly deferred, not a claim of perfect binary parity.
- [x] Update affected samples and documentation for the integrated snapshot, including compiled and executed custom-converter examples.
- [x] Verify the Release solution build on all targets, all configured managed test frameworks, and the self-checking F# sample after fixed-snapshot integration.
- [x] Publish and execute the four NativeAOT fixtures after the integrated platform and dynamic-dictionary fixes; successful native linking alone is insufficient.
- [x] Compare performance and allocations with the frozen baseline in matched runs; final accepted results and the remaining elapsed-time regression are recorded below.
- [ ] Explain approved differences and authorized breaking/no-obsolete-alias policy in the eventual PR (future handoff, not a blocker for this completed checkpoint; no PR requested this run).
- [x] Normalize asynchronous operation failures as `JsonSerializationException`, retaining causes, existing exception instances/codes, and actual cancellation.
- [x] Verify first-value behavior across generic, object-based, native DOM, buffer/sequence, synchronous stream, and caller-pipe entry points, with documented strict string/async-stream/NDJSON boundaries.
- [x] Update root README snippets and the feature overview to final navigation, sequence, converter, `UnusedDataPacket`, and flat-array names.
- [x] Complete the requested navigation/attribute API and XML reconciliation; warnings-as-errors DocFX reports zero warnings.
- [x] Verify formatting without restoring and execute the complete native net10.0 core suite; explicitly skip only unsupported lower-bound-array input creation, preserving all managed assertions.
- [x] Implement the deferred borrowed segmented parser without whole-document flattening.
- [x] Add the direct `RawJson` wrapper with explicit ownership and an automatic owned converter.
- [x] Address deferred analyzer rules NBJson031 and NBJson051, and activate contract-aware NBJson001/NBJson002.
- [x] Add memory/array readers, clones, token inspection, decoded UTF-8 strings, and the reviewed stateless scalar primitives.
- [x] Document intentional low-level contract limits separately from implementation tasks; the implemented scalar subset does not promise general string/container primitives.
- [x] Publish and execute final Windows NativeAOT core net9.0/net10.0 and all four fixtures; the final full platform run supersedes the historical linker block.
- [x] Investigate and optimize the object elapsed-time regression without sacrificing allocation parity, safety, or correctness; causal boundaries and accepted fixes are recorded below.
- [ ] Further isolate and reduce residual small/wide-object read cost as a non-blocking follow-up, not an API-finalization gate; the accepted fixes do not establish complete original-speed restoration.
- [x] Execute full Linux validation in an isolated WSL Linux-native checkout, preserving existing size budgets.
- [x] Receive actual macOS execution evidence from CI; an independent user-Mac handoff run is still not claimed.

## Renewed throughput investigation

The user authorized continued measured performance investigation without requiring complete original-speed parity.
The first retained change is contiguous lexical string scanning: local-span prefix handling plus an outlined SIMD search, preserving the original scalar handlers, permissive Unicode policy, failures/positions, and segmented/legacy paths.
Matched repeated Medium runs cut the large escaped string-array cases from 576/833 us to 78/344 us without/with interning, with unchanged allocations; ordinary object timings do not establish a speedup and remain logged in the [benchmark report](docfx/docs/benchmarking.md).
All 4,841 managed tests pass, including 432 lexical threshold/boundary cases across four core frameworks; the full Release build is warning-free.
Four earlier variants are preserved on named local branches and not merged because of worse short-string or segmented tradeoffs.
Next measured leads are repeated end traversal in segmented span probing and signed division in property-name bucket selection; no public API change is required for either.
Native/startup impact and final cross-platform validation will be recorded separately rather than inferred from timing.
The next retained fix removes a numeric leading-zero lookback that reset the segmented byte cursor to the entire input's start whenever integer digits crossed segments.
This turns the tiny-segment 1,024-number case from approximately 7.3 ms into 0.218 ms (about 33 times faster), with the same 104 B allocation; scaling is now approximately linear and the 32-number control is about twice as fast.
The digit loop, cancellation checks, grammar, failure outputs, token spellings, and public APIs remain unchanged; all 5,013 managed tests pass.
The old constant-time span access became expensive with borrowed segmentation (`3737b21`) and was later copied into the shared lexer (`088ae4f`); warmed profiles and repeated original-source controls corroborate this cause.
The [benchmark report](docfx/docs/benchmarking.md) includes scaling/error/allocation tables and an anomalous unchanged escaped-string control, not just the wins.
Remaining profiled targets include split-token copy setup, redundant span-probe traversal, per-byte segment access, and property lookup division.

## Current macOS CI calibration (operator-approved)

GitHub Actions run `39676067406` at `78a9fc9` executes the macOS build, tests, and native fixtures successfully; its broad native image is 5,931,376 bytes (5.6566009521484375 MiB).
The live operator explicitly accepted growth from the historical 5.40 MiB baseline and authorized recalibration.
The new macOS arm64 center is **5.65 MiB**, with the same **+/- 0.2 MiB** tolerance; Windows/Linux centers stay **5.79/5.88 MiB**.
Both CI checks and the standalone platform validator use the reviewed value; no fixture coverage, runtime feature, assertion, or required check is removed or bypassed.
This evidence supersedes older macOS-unverified statements below; the next CI run still has to confirm the recalibrated guard.
The low-level source is unchanged: the experimental runtime-union navigation size reduction is preserved only on local branch `perf/deferred-union-navigation` at `315b3d8`, not merged or pushed.
Actual arm64 compiler reports attribute 68,440 code bytes to duplicated generic async navigation state machines and their MoveNext wrappers; that remains a measured optimization lead, not a proven fix or an API-finalization gate.
The candidate was not validated: its latest build stopped on a whitespace analyzer error before tests ran; Medium baseline reports and compiler evidence are retained in session artifacts for a later investigation.

## Current API finalization checkpoint (source `a28b0ef`)

The remaining common scalar-overload and direct writer-buffer API gaps are implemented, rather than held for complete throughput recovery.
Accepted commits are `31dbb33` (overloaded scalar APIs and caller migration), `7db40f0` (complete-value buffer reservations and compiled converter example), `d2ab8e0` (portable buffer tests), `81f3fbd` (legacy floating precision/signed zero), and `a28b0ef` (focused benchmarks).
Replaced scalar reader names are removed without obsolete aliases; the eventual PR must retain the authorized breaking-change explanation.
`GetSpan` reserves without consuming a value; positive `Advance` commits one complete trusted value, and zero cancels.
Raw fragments, property names, or multiple values per reservation are deliberately not promised: JSON container and pending-property state must remain coherent.
Remaining stateless string/container operations require JSON-specific decoding/framing contracts, not MessagePack's encoded-length headers; stateful reader/writer and borrowed raw APIs remain available.

| Evidence at `a28b0ef` | Windows x64 | WSL Ubuntu 26.04 Linux x64 |
|---|---:|---:|
| Managed tests passed / failed / skipped | 4409 / 0 / 0 | 3405 / 0 / 0 |
| Native core .NET 9 passed / skipped | 1017 / 2 | 1017 / 2 |
| Native core .NET 10 passed / skipped | 1017 / 2 | 1017 / 2 |
| Native failures | 0 | 0 |
| All four native fixtures / F# self-check | PASS / PASS | PASS / PASS |
| Broad image bytes / MiB | 6110720 / 5.82763671875 | 6323240 / 6.030311584472656 |
| Unchanged broad budget, MiB | 5.79 +/- 0.2 | 5.88 +/- 0.2 |
| Minimal image bytes / MiB (uncalibrated) | 2786816 / 2.65771484375 | 3010480 / 2.8710174560546875 |

Full Release builds and native publishes pass with warnings as errors; both size guards pass without recalibration.
The native skips remain only unsupported nonzero-lower-bound input-array creation, with managed assertions intact.
DocFX builds with zero warnings/errors and formatting verification passes.
Coordinator runtime validation supersedes the workers' permission-blocked test execution; no unexecuted worker checks are counted as passes.
Full reports are `obj\final-api-windows-a28b0ef\validation.json` and the isolated Linux checkout's `obj/final-api-linux-a28b0ef/validation.json`.

The [benchmark guide](docfx/docs/benchmarking.md) records fresh serial Medium object controls and the new scalar fixtures, including absolute means, errors, medians, allocations, and evidence paths.
Object allocations remain 200/664/0/0 B for small/wide reads/writes; overlapping timing intervals do not establish a new causal regression or speedup.
Exact-token primitive Int32/Single reads measured 25.73/47.61 ns versus 44.26/66.09 ns through a reader, all allocation-free on .NET 9; this is specialization, not full-serializer speed restoration.
Direct-buffer Single writing measured 104.32 ns versus 101.42 ns through normal writing, so no universal fast-writer claim is made.

### Non-blocking follow-ups and limits

| Scenario | Evidence and suspected source | Disposition |
|---|---|---|
| Residual contiguous object reads | Warm profiles implicate object population including lookup/dispatch; hashing alone is unproven and its unrolling experiment is rejected. | Keep measuring implementation hypotheses later; do not block preferred API finalization. |
| Segmented scratch latency | Allocation gains are proven, with number/name cases approximately 13% slower against the repeated policy-only baseline. | Retain documented tradeoff; investigate internal copy/cursor costs without changing lifetime contracts. |
| Direct scalar writer sample | Transactional primitive formatting uses scratch then copies; reservation accounting adds work. These are plausible sources, not measured causal attribution. | Consider destination-size-specialized formatting after profiling; no new public API is required. |
| Legacy numeric behavior | Full-precision E8/E16 formatting fixes lossy legacy defaults and preserves signed zero; parsing follows the same target-runtime rounding limitations as JsonReader, including the documented near-halfway Single case. | Correctness cases are tested on net472 and modern .NET; further parser improvements are internal follow-ups, not a rounding-equivalence claim across runtimes. |
| macOS execution | CI build/tests/native execution evidence is now available, with operator-approved 5.65 +/- 0.2 MiB calibration documented above. | Confirm the recalibrated guard in CI; do not claim an independent user-Mac handoff run. |

## HISTORICAL Unicode, scratch, and performance checkpoint (source `ef6e8f9`)

This supersedes the historical checkpoint below for current semantics, test counts, and native sizes.
Commit `7e2e3cf` implements the user-directed permissive Unicode policy; commit `ef6e8f9` implements caller-owned scratch for split tokens.
JSON lexical grammar, framing, depth/cancellation, raw ownership, and exact-shape cache semantics remain intact.
No original-speed-restoration claim is made.

- [x] Remove Unicode prevalidation while preserving raw bytes and replacement-based CLR decoding.
- [x] Audit split-token consumers: typed numbers, object property lookup, targeted property matching, and segmented strings now use borrowed/caller-owned/pooled storage as appropriate.
- [x] Cover long tokens, pool fallback, escape/decoder boundaries, failures, navigation, and allocation behavior.
- [x] Measure serial Medium policy-only/scratch/repeated-baseline workloads; retain allocation gains with documented mixed timing tradeoffs.
- [x] Profile warmed contiguous object reads and test one narrow property-hash hypothesis using Medium A/B/A; reject the inconclusive candidate.
- [x] Validate the committed source with full Windows and Linux managed/native matrices, all four native fixtures, F# self-checks, and unchanged native size budgets.

The [benchmark report](docfx/docs/benchmarking.md) contains exact scratch allocations, timing/error tables, the rejected experiment, and evidence paths.
Split numbers/property-name allocations fall from 1384/2024 B to 104/768 B; string scenarios fall from 1496/3584 B to 1024 B.
Compared with the repeated baseline, segmented number/property-name timings are approximately 13% slower and unescaped strings approximately 7% slower; no blanket throughput win is claimed.
The latest remaining hotspot is object population/property lookup/dispatch, not the removed Unicode prepass.
The four-byte hash-unrolling candidate did not demonstrate a repeatable worthwhile win and is preserved only on local branch `perf/rejected-property-hash-unroll` (`8ac0e83`), not merged.
Candidate correctness execution was permission-blocked, so no candidate test pass is claimed.
Residual contiguous read cost and actual macOS execution remain open as tracked above.

| Evidence at `ef6e8f9` | Windows x64 | WSL Ubuntu 26.04 Linux x64 |
|---|---:|---:|
| Managed tests passed / failed / skipped | 4232 / 0 / 0 | 3272 / 0 / 0 |
| Native core .NET 9 passed / skipped | 973 / 2 | 973 / 2 |
| Native core .NET 10 passed / skipped | 973 / 2 | 973 / 2 |
| Native failures | 0 | 0 |
| All four native fixtures / F# self-check | PASS / PASS | PASS / PASS |
| Broad image bytes / MiB | 6113792 / 5.83056640625 | 6327336 / 6.034217834472656 |
| Unchanged broad budget, MiB | 5.79 +/- 0.2 | 5.88 +/- 0.2 |
| Minimal image bytes / MiB (uncalibrated) | 2787840 / 2.65869140625 | 3010480 / 2.8710174560546875 |

Both builds/native publishes use warnings as errors, and both broad size guards pass without recalibration.
Formatting verification passes, and the updated DocFX site builds with zero warnings/errors under warnings-as-errors.
The four skipped cases per OS remain solely the two unsupported nonzero-lower-bound input-array cases per native framework.
Full reports are `obj\unicode-scratch-windows-ef6e8f9\validation.json` and the isolated Linux checkout's `obj/unicode-scratch-linux-local-sdk-ef6e8f9/validation.json`.
An initial Linux prerequisite attempt selected the system SDK and correctly failed; the successful rerun explicitly used the existing repo-local SDK through process-local environment variables, without global changes or new installations.
The profile/benchmark artifacts and rejected candidate are intentionally retained outside the accepted branch for reproducibility.

## HISTORICAL documentation/evidence checkpoint (2026-10-21, source `7ea67c8`)

**Historical checkpoint, superseded by the current Unicode/scratch checkpoint above.**
It supersedes older counts, inactive-analyzer/parser deferrals, Windows native-linking blocks, and Linux-unverified statements retained below as historical evidence.
The requested cause identification, accepted fixes, remaining deferred integration, Medium measurements, and Windows/WSL validation are complete; residual performance work and an external Mac result remain open.
This checkpoint changes documentation only on `aarnott-miniature-engine`, with no further source changes, experiments, delegation, branch creation, push, PR, or amend.
Approved JSON/secure-comparer exceptions and the breaking-change/no-obsolete-alias policy above remain unchanged; an eventual PR must explain that policy, but no PR is requested or created here.

### Final platform evidence

The coordinator executed the complete `tools/Validate-Platform.ps1` run against `7ea67c8` on both operating systems.

| Evidence | Windows x64 | WSL Ubuntu 26.04 Linux x64 |
|---|---:|---:|
| Managed tests passed | 4,129 | 3,191 |
| Managed failures / skips | 0 / 0 | 0 / 0 |
| Analyzer tests included | 163 | 163 |
| Native core .NET 9 passed / skipped | 946 / 2 | 946 / 2 |
| Native core .NET 10 passed / skipped | 946 / 2 | 946 / 2 |
| Native total passed / total cases | 1,892 / 1,896 | 1,892 / 1,896 |
| Native failures | 0 | 0 |
| Native fixtures published with warnings as errors and executed | All four | All four |
| F# self-check / native image guards | PASS / PASS | PASS / PASS |
| Broad image bytes | 6,097,920 | 6,310,840 |
| Broad image MiB | 5.8154296875 | 6.018486022949219 |
| Unchanged broad budget, MiB | 5.79 +/- 0.2 | 5.88 +/- 0.2 |
| Minimal image bytes | 2,782,208 | 3,002,256 |
| Minimal image MiB (uncalibrated) | 2.6533203125 | 2.8631744384765625 |

The two native skips per framework are only non-zero-lower-bound `Array.CreateInstance` input creation; all managed normalization assertions remain intact.
All four fixtures means `AotSizeTest`, `AotRuntimeCompatibilityTest`, `AotNativeConsole`, and `AotCompatibilityTest`, not merely successful native linking.
DocFX with warnings as errors reports zero warnings/errors, `dotnet format` verification exits zero, and the source-checkpoint `git diff --check` was clean.
No source changed after these checks; this documentation-only checkpoint does not claim a new test run.

Windows evidence is in `obj\final-windows-reader-7ea\validation.json` and its complete run directory.
Linux evidence is in `obj/final-linux-reader-7ea/validation.json` beneath the isolated Linux-native checkout `/home/andrew/.local/share/nerdbank-json-copilot-70b1bb69/repo`.
The existing SDK helper installed SDK 10.0.402 and .NET/ASP.NET Core 8.0.32, 9.0.21, and 10.0.13 only under that checkout's `obj` directory; the system SDK was unchanged.
Linux had no clang, so the full run explicitly selected the validated `-CppCompilerAndLinker /usr/bin/g++`; no apt, sudo, or global installation was used.
Script fixes `0378400` (PowerShell working-directory artifact resolution and explicit Linux compiler) and `4e35844` (direct built DLL/executable MTP/TRX forwarding) are proven by these full runs; failed commands and missing reports remain nonzero failures.
Earlier failed prerequisite/forwarding runs are retained history, not final failures.

Actual macOS execution remains pending.
The [platform guide](docfx/docs/platform-validation.md) contains requirements and a standalone Copilot prompt: use a dedicated checkout of this branch, then run `pwsh -NoProfile -File ./tools/Validate-Platform.ps1 -ArtifactDirectory ./obj/platform-macos`.
Return the complete `validation.json`, logs, TRX, and native size artifacts, including failure evidence.
Medium is optional and must wait until no other jobs are running.
Do not recalibrate budgets: macOS arm64's historical 5.40 +/- 0.2 MiB guard remains unchanged and unverified on this source.

### Implemented surface and intentional limits

The reader borrows segmented JSON without flattening the entire payload.
Owned `RawJson` has an automatic converter; borrowed capture and `ToOwned` make lifetime transitions explicit.
Decoded UTF-8 string/token inspection, memory/array constructors, clones, and stateless scalar `JsonPrimitives` with final-block `DecodeResult` and shared numeric grammar are implemented.
NBJson001/NBJson002 are active contract-aware diagnostics, not descriptor-only stubs; NBJson031 uses conservative control-flow analysis, and NBJson051 gives real .NET mapping guidance.
Diagnostic pages, packaging/source-consumer checks, and all 163 analyzer tests pass.
Duck-typing required metadata, constructor checks, and concrete-base cases are fixed through `f0fcfba`; strict ambiguity rejection and the experimental marker remain, while serializer-naming-dependent validation stays at converter creation.

Decoded stateless strings, arbitrary raw/container primitives, dedicated narrow-number primitive overloads, and writer `GetSpan`/`Advance` are intentional remaining contract limits, not no-op stubs or unchecked implementation promises.
Asynchronous stream deserialization remains strict because a temporary pipe cannot return prefetched bytes to an arbitrary non-seekable stream.
The compiled retained-`PipeReader` example is tested for repeated first-value reads, and MVC's strict HTTP-body contract is not weakened.
Supported dictionary-key restrictions and immutable/constructor-bound cycle restrictions also remain intentional.

### Causal performance findings and final Medium evidence

Historical parent/child Medium jobs, counterfactuals, paired warm unboxed EventPipe traces, and disassembly identify two original regression causes rather than speculative context-size attribution.
At `e692cab`, provider initialization with record-`with` copies moves small writes from 179.8 to 198.1 ns (repeat 178.0 to 196.7); removing only the copies gives 178.3 ns.
At `d5b883f`, removing the last-converter shortcut moves small writes from 186.9-190.8 to 217.1-223.1 ns; restoring only that shortcut gives 196.0 ns, while padding the parent context does not reproduce the regression.
The old type-only shortcut is unsafe: public shape identity requires the actual provider identity.
Accepted fixes are internal readonly property dispatch `f2b7961` while preserving public converter by-value isolation, and atomic exact-shape locality caching `2fdeb10`.
`d2830c5` subsequently proves and repairs equality-colliding dictionary shape keys with a reference comparer.
The historical report below retains full controls and uncertainty.

Low-level integration introduced a separate measured regression: small/wide reads reached about 498/2,327 ns and writes 187/511 ns.
Warm profiles and disassembly identified per-ASCII-byte general UTF-8 validation, scalar validation work, and expanded segmented navigation on contiguous input.
Historical `440b7fe`, `8767712`, and `6938cba` added a printable-ASCII span fast path, runtime-vectorized ASCII range validation with strict fallback, and cached immutable length/outlined segmented helpers.
The 2026-10-21 user-directed Unicode policy below supersedes their Unicode-validation requirement, not the JSON lexical checks or borrowed-input design.
An explicit span-specialized parser experiment was rejected; its patch is retained outside the checkout and none remains in source.
The bounded-depth native-code total decreased from 6,521 to 4,934 bytes; this is not a whole-library image-size measurement.

The final Windows job uses .NET 9.0.21, SDK 10.0.402, BDN 0.15.8, Release, affinity mask 2, default tiering, two launches, ten warmups, and fifteen iterations, serially.
Errors below are 99.9% confidence-interval half-widths.

| Final `7ea67c8` operation | Mean +/- error, ns | Median, ns | B/op |
|---|---:|---:|---:|
| Small read | 351.1 +/- 2.13 | 351.1 | 200 |
| Wide read | 1,270.5 +/- 14.17 | 1,269.4 | 664 |
| Small write | 179.2 +/- 1.08 | 178.9 | 0 |
| Wide write | 479.3 +/- 5.62 | 477.3 | 0 |

Against accepted pre-low-level reads of about 313/1,077 ns and writes of 179-185/483-505 ns, reads remain about 12%/18% slower.
Earlier original `f64da69` Medium reads were 292.9/1,055.1 ns, so neither original nor pre-low-level read speed is fully recovered.
Strict validation and segmented-capable paths plausibly contribute to residual cost, but their causal shares are not fully isolated.
Paired warm sampled-thread-time frame percentages are relative attribution, not elapsed-time ratios.
All original object allocation parity targets are met; earlier noisy/bimodal distributions remain retained.
The full 64-case ReaderSkip Medium matrix succeeds, reporting 0-1 B contiguous and 104-107 B segmented per operation with BDN rounding.
These small navigation allocations do not grow with payload size; this is not a claim that all segmented readers allocate zero bytes or a paired skip-throughput improvement.

A real WSL Medium original/current pair uses frozen `f64da69` and `7ea67c8`, the same SDK/runtime/BDN, affinity 2, two launches, ten warmups, and fifteen iterations, serially with no other builds.

| WSL operation | Original mean +/- error, ns | Original median, ns | Current mean +/- error, ns | B/op, both |
|---|---:|---:|---:|---:|
| Small read | 352.7 +/- 21.77 | 348.7 | 398.9 +/- 3.31 | 200 |
| Wide read | 1,124.8 +/- 16.03 | 1,122.8 | 1,400.4 +/- 14.85 | 664 |
| Small write | 186.4 +/- 6.68 | 194.1 | 200.6 +/- 1.48 | 0 |
| Wide write | 508.8 +/- 6.09 | 511.6 | 526.2 +/- 4.67 | 0 |

The original Linux small-read run is noisier; this is one matched pair, not replicated Linux A/B/A evidence or a cross-OS speed comparison.
Minimum-iteration warnings and outliers are retained rather than hidden.
Reader evidence is retained outside source control at `C:\Users\andre\.copilot\session-state\6fbada64-d1bf-444d-be95-97580c7b0685\files\reader-perf`, including `RootCause.txt`, `object-measurements.csv`, traces, disassembly, and rejected patches.
Original causal evidence remains in session `ccd4d45f-edf7-4ad2-bce2-e9f5839aa2f8`, `files\perf-cause`.
Linux benchmark evidence includes the isolated `perf-linux-medium-baseline-f64/repo` and host logs `final-linux-medium.log` and `final-linux-perf-validation-summary.log`.
The requested checkpoint is complete with these explicit limits; perfect API parity and full speed recovery are not claimed.

## User-directed decoder policy (2026-10-21, after `a225be3`)

Unicode well-formedness is application policy, not a reader/writer precondition.
Removed the general UTF-8 validator and surrogate-pair rejection, while preserving JSON boundary, escape, control-byte, number, depth, cancellation, duplicate-property, and framing checks.
CLR reads use replacement fallback and preserve escaped UTF-16 code units; CLR writes escape lone surrogates.
UTF-8 writes and unescaped byte views retain non-ASCII payloads; escaped byte reads also retain non-ASCII runs, with replacement only for unrepresentable escaped lone UTF-16 surrogates.
`RawJson` retains exact bytes and its existing owned/borrowed lifetime contracts.
The scanner already frames ASCII boundaries independently of Unicode and required no policy change.
Async, native DOM, property-name, and interning paths consume the same reader; optional System.Text.Json converters still necessarily inherit that external DOM's own representation rules.
Fresh full managed validation passed 4,165 tests with zero skips after a warning-free Release solution build.
Filtered MTP invocations were permission-denied; the explicit solution invocation succeeded and actually executed the tests.
The isolated Medium measurements and uncertainty are recorded in `docfx/docs/benchmarking.md`; this policy change alone does not establish full object-read speed recovery.

## HISTORICAL deferred integration QA (2026-10-21, through `c4f2121`)

This section preserves the earlier integration evidence.
The FINAL `7ea67c8` checkpoint above supersedes its pending benchmark/platform work and Windows linker block.

The exclusive-main integration preserves the causal performance fixes, the user's `0aa59ea` project grouping, strict MVC body handling, and the current duck-typing and stream-ownership contracts.
`CustomConverters.cs`, `AsyncStreaming.cs`, and `AnalyzerGuidance.cs` are all included in the managed-only net8.0 sample group.
No branch, delegation, push, PR, amend, or platform-script change was made.

| Source commit | Integrated commit | Change |
|---|---|---|
| `bcbe720` | `f3e82b5` | Provable converter structure diagnostics |
| `67387d4` | `08b3af6` | Static shape mapping guidance |
| `a407a52` | `f56c00f` | Generated/reflected contract analysis |
| `a417af1` | `1947fce` | Non-returning helpers and compiled diagnostic guidance |
| `2ff2347` | `3737b21` | Borrowed segmented reader |
| `f93d66b` | `f951c3a` | Raw values and ownership |
| `75eda3e` | `c65152a` | UTF-8 strings, token inspection, constructors, clones |
| `45c2db7` | `088ae4f` | Stateless scalar primitives and shared number grammar |
| `ee6411d` | `df2cb2f` | Cursor/lifetime/boundary coverage and native runtime API coverage |
| `6080426` | `0d48372` | Durable segmented skip distributions |

Focused integration corrections:

* `46d09f2` removes unrelated compiler warnings from analyzer fixtures; all 163 analyzer tests actually execute.
* `d2830c5` reproduces equality-colliding shape instances aliasing converters on all managed frameworks, then uses exact reference identity in the resolved-converter dictionary.
* Historical `86c5e65` documented strict rejection of isolated surrogate escapes; the 2026-10-21 user-directed policy explicitly supersedes this rejection while retaining valid-pair and interning coverage.
* `31cc902` restores byte cursors after tentative escaped-property and escaped-string sizing scans, so forward fallback reads do not reset navigation to the sequence origin; untyped metadata probing uses an independent peek.
* `2d73f0a` runs allocation guards outside parallel test activity and excludes assertions from measured regions without weakening any threshold; three consecutive complete managed matrices passed those unchanged thresholds.
* `c4f2121` poisons returned pipe-pool buffers, forces collection, and verifies exact owned raw bytes and direct UTF-8 output; relaxed input settings do not relax raw-output validation.

`dotnet build -c Release --no-restore -warnaserror` succeeds with zero warnings/errors.
`dotnet test --solution Nerdbank.Json.slnx --no-build -c Release -- --report-trx --results-directory=obj\integration-deferred-tests` actually executes **4,057 tests**, all passing, with zero skips.
The explicit solution and assignment-form report option avoid SDK command-line ambiguity when the results directory already exists.

| Project | Framework | Passed |
|---|---|---:|
| Core, including all three documentation samples | net8.0 | 937 |
| Core | net9.0 | 930 |
| Core | net10.0 | 930 |
| Core | net472 (net48 runtime) | 920 |
| MVC | net8.0 | 59 |
| MVC | net9.0 | 59 |
| MVC | net10.0 | 59 |
| Analyzers | net8.0 | 163 |

All split positions, empty segments, reference metadata, constructor contracts, raw token spelling, BOM/source positions, scalar boundaries, and borrowed/owned lifetimes are covered.
Repeated escaped object members run across one-byte, 64-byte, and 4096-byte segments with independent peeks.
The final net9.0-targeted `JsonReader` measures 80 bytes in the x64 validation host using IL `sizeof`; no historical struct-size comparison was performed.
The reader carries wider offset/navigation state; segmented input allocates a small cursor object, and explicit segmented peeks allocate independent cursor objects rather than payload-sized copies.
The contiguous reader/scalar and cached graph/provider zero-allocation guards pass; segmented skip remains below its unchanged 1,024-byte bound.
No throughput benchmark ran during this build phase, and no speed claim is made for the wider reader or new segmented paths.
The coordinator owns the final matching medium baseline/candidate comparison after all builds stop.

`dotnet docfx docfx\docfx.json --warningsAsErrors --disableGitFeatures`, `dotnet format Nerdbank.Json.slnx --verify-no-changes --no-restore`, and the self-checking F# sample all exit successfully.
DocFX reports zero warnings/errors; formatting notes that F# is unsupported, so that sample is separately executed.
Logs and TRX reports are retained under `obj\integration-deferred-*`.

**Fresh Windows NativeAOT execution remains blocked, not passed.**
`dotnet publish test -c Release --no-restore` reaches native linking but fails with MSB3073/exit 123 because `vswhere.exe` is not on PATH; its diagnostic text contaminates the discovered linker executable path.
Separate fresh native core publishes for net9.0 and net10.0 also reach this linker failure with zero warnings; neither executable runs.
The command gate denies invocations setting the required process-local `C:\Program Files (x86)\Microsoft Visual Studio\Installer` PATH prefix.
The coordinator must repeat publishing with that prefix, then execute both eligible native core frameworks and all four freshly linked fixtures, and rerun the unchanged broad size guard.
Do not execute stale images as evidence for the final tree or infer success from compilation.
Only the two known unsupported lower-bound-array inputs may skip natively.
The coordinator-provided real WSL baseline pass predates this final integration; refreshed Linux validation and macOS execution remain separate handoff work.
No OS size budget was changed or recalibrated.

## HISTORICAL performance attribution follow-up (2026-10-20)

The historical checkpoint below is retained as evidence. The attribution follow-up
is complete with accepted code `2fdeb10`, following `f2b7961`; both descend from
the user's unchanged `0aa59ea`. Matched medium historical jobs, parent/child
replication, causal controls, and paired warm sampled-thread-time profiles identify
two concrete regression components. This is a substantial partial recovery, not
a claim that every timing difference across the 64-commit range is decomposed.
Production-default tiering/PGO and constant benchmark inputs are the primary configuration.
At this historical checkpoint, parser/raw-wrapper/analyzer work was still deferred; the FINAL checkpoint above records its implementation and later reader measurements.

### Causal findings, not just source hypotheses

All timing jobs below use BDN 0.15.8, SDK 10.0.402, .NET 9.0.21 x64, Windows 11,
Threadripper PRO 7995WX, Release, affinity mask 2, the actual **MediumRun** preset:
2 launches, 10 warmups and 15 measured iterations per launch, with BDN's default
adaptive iteration duration. The four `ObjectBenchmarks.*NerdbankJson` methods and
their generated models/input source are copied unchanged to each detached worktree.
No benchmark runs, builds, or traces overlap. No global power plan or applications
are changed. No tiering-disabled results are substituted for normal tiering/PGO.
These medium jobs are not the earlier 250-ms/10-iteration measurements.

| Boundary | Repeated small-read means (parent -> child), ns | Repeated small-write means (parent -> child), ns | Causal control |
|---|---|---|---|
| `f744a3f` -> `e692cab` | 300.7 -> 320.7; 296.7 -> 320.1 | 179.8 -> 198.1; 178.0 -> 196.7 | Provider-local initialization on `e692cab` gives 311.0 ns read and 178.3 ns write; the added record-`with` copies explain the write step, but not all read cost. |
| `4ec30c4` -> `d5b883f` | Parent 313.4-336.1; child 346.6-388.6, with noisy/multimodal child reads | Parent 186.9-190.8; child 217.1-223.1 | Restoring only the historical last-converter shortcut on `d5b883f` gives 320.0 ns read / 196.0 ns write without undoing `in T` or reference registration. |

Representative parent/child uncertainty (ns, BDN Error is the 99.9% CI half-width):

| Snapshot/run | Operation | Mean | Median | Error | StdDev |
|---|---|---:|---:|---:|---:|
| `replicate-f744a3f` | Small read | 296.7 | 297.4 | 6.69 | 9.16 |
| `replicate-e692cab` | Small read | 320.1 | 321.5 | 6.51 | 8.91 |
| `replicate-f744a3f` | Small write | 178.0 | 178.6 | 1.37 | 1.88 |
| `replicate-e692cab` | Small write | 196.7 | 194.1 | 7.21 | 10.34 |
| `boundary3-4ec30c4` | Small read | 313.4 | 311.9 | 5.09 | 7.30 |
| `boundary3-d5b883f` | Small read | 388.6 | 360.4 | 39.99 | 59.85 |
| `boundary3-4ec30c4` | Small write | 186.9 | 185.3 | 6.74 | 9.23 |
| `boundary3-d5b883f` | Small write | 223.1 | 222.6 | 2.36 | 3.38 |

The unmodified `d5b883f` snapshot does **not build**: its async-enumerable converter
was not migrated to `in TEnumerable?`. Its original failed log is retained under
`initial-d5b883f`. Subsequent measurements explicitly apply only that override
signature repair, to a converter never used by these object benchmarks. This is
an adjusted historical snapshot, not a claim that the original commit builds.
No intermediate closure-allocating snapshots are used; `13bc811` remains preserved.

Adding only the later `ReferenceId` auto-property to the parent as a layout control
gives 321.7 ns small read / 184.7 ns small write, not the child slowdown.
Thus attributing the second step primarily to the 72-to-80-byte context growth or
`in T` would be incorrect. The first provider change grows the context from
64 to 72 bytes and adds full-struct clones; the second adds another field, but its
removed locality shortcut is a separately demonstrated cost. The earlier unsafe
shortcut is a measurement-only control, never restored to shipping code.
The wider-object historical distributions do not support an equally precise
first-bad classification; they are retained rather than blindly labeled good/bad.

### Paired warm attribution

Official session-local `dotnet-trace` 10.0.750501 collects
`dotnet-common,dotnet-sampled-thread-time` for 20 seconds after six seconds of
startup/warmup, from already-built 30-second workload processes. The harness
validates results, retains observable outputs, and uses an unboxed write sink.
Pairs cover both causal boundaries and `0aa59ea` versus `2fdeb10`, for wide reads
and small writes. These are EventPipe sampled **thread-time** stacks, not precise
kernel on-CPU measurements or quantitative throughput comparisons.

The unboxed write profile at `f744a3f` attributes 37.70% exclusively to
`SerializeCore`; at `e692cab` this becomes 51.52%. At the second boundary,
dictionary `FindEntry` appears at 3.25% exclusive on the child and is absent from
the parent's top functions. The accepted locality cache removes it from the final
write top functions. `PopulateReference` is the dominant current wide-read frame:
60.47% exclusive before versus 46.83% after; its inclusive children still include
UTF-8 string decoding, property-name comparisons, and scalar conversion.
Inlining and changed work per second affect these percentages; they are not
additive nanosecond estimates or proof that all residual time is copying.

Medium-job warm disassembly shows the property-dispatch change removes one
80-byte bulk-copy call from the serialization Core body (4337 -> 4161 bytes for
the small fixture). The provider-local control removes two more bulk copies.
BDN code-size totals have bounded disassembly depth and are not whole-library
native-size measurements. Earlier Dry/ColdStart assembly is retained only as
cold-JIT diagnostic evidence, not used to explain warmed timings.

### Final replicated medium comparison

`final-*` runs serialize the original / pre-fix / accepted snapshots in sequence;
`final-repeat-*` repeats that entire sequence. All values are ns except bytes.
The final code has no provider-initialization experiment.

| Snapshot | Operation | Mean | Median | Error | StdDev | B/op | Repeat mean |
|---|---|---:|---:|---:|---:|---:|---:|
| Original `f64da69` | Small read | 292.9 | 301.4 | 11.25 | 15.77 | 200 | 282.0 |
| Pre-fix `0aa59ea` | Small read | 345.5 | 338.0 | 14.01 | 20.53 | 200 | 332.8 |
| Accepted `2fdeb10` | Small read | 312.9 | 302.8 | 21.54 | 30.19 | 200 | 313.7 |
| Original `f64da69` | Wide read | 1055.1 | 1063.8 | 35.04 | 47.96 | 664 | 1093.2 |
| Pre-fix `0aa59ea` | Wide read | 1139.5 | 1123.5 | 37.72 | 52.88 | 664 | 1150.7 |
| Accepted `2fdeb10` | Wide read | 1071.1 | 1054.8 | 35.66 | 52.27 | 664 | 1077.2 |
| Original `f64da69` | Small write | 179.0 | 178.4 | 3.57 | 5.23 | 0 | 177.8 |
| Pre-fix `0aa59ea` | Small write | 230.6 | 233.8 | 10.17 | 14.91 | 0 | 220.3 |
| Accepted `2fdeb10` | Small write | 185.3 | 184.8 | 1.99 | 2.92 | 0 | 179.7 |
| Original `f64da69` | Wide write | 495.9 | 496.5 | 5.80 | 8.13 | 0 | 495.5 |
| Pre-fix `0aa59ea` | Wide write | 555.8 | 551.0 | 11.27 | 16.16 | 0 | 554.7 |
| Accepted `2fdeb10` | Wide write | 504.9 | 499.0 | 14.06 | 20.61 | 0 | 482.7 |

The accepted means improve over pre-fix in both sequences: approximately 6-9%
small read, 6% wide read, 18-20% small write, and 9-13% wide write. These are
observed mean changes, not a uniform speed guarantee. Wide operations and small
writes return close to the frozen original range. Small-read means remain
approximately 7-11% above the original; their distributions also vary by launch.
No exact attribution of every remaining read nanosecond is established.
Median, CI, StdDev, GC, and allocation data for both sequences are retained in
the JSON/CSV reports and the generated `all-measurements.csv`.

### Rejected controls and final tradeoffs

| Experiment | Decision | Evidence and retention |
|---|---|---|
| Historical type-only last-converter restoration | Causal control only; unsafe for multiple providers | `d5b883f-cache-control.patch` and `causal-controls-d5b883f-cache-control`; do not merge. |
| Parent context padded with `ReferenceId` | Reject as explanation for the second step | `context-padding-control.patch` and `layout-intervention-4ec30c4-context80`. |
| Historical provider-local setter | Confirms the first write cause, not an accepted change | `e692cab-provider-control.patch` and `causal-controls-e692cab-provider-local`. |
| Current operation-local provider setter | Reject tradeoff | `provider-local-current.patch`; A/B/A/B small writes 176.8/169.7/180.9/172.7 ns, but wide writes 481.5/497.5/470.5/487.7 ns. Smaller-body assembly alone is not sufficient. |
| Provider fused into `Start` | Reject | `provider-start-current.patch`; repeated wide writes 535.7/538.6 ns versus preceding cache-only 470.5-481.5 ns; more wiring without a worthwhile win. |
| Readonly internal property dispatch | Accept `f2b7961` | Details below; custom converters retain by-value isolation. |
| Shape-aware locality cache | Accept `2fdeb10` | Details below; shape identity, concurrency and zero warm allocation are covered. |

All four final NativeAOT fixtures publish with warnings as errors and execute.
The broad image is **6,092,800 bytes / 5.810546875 MiB** and passes the actual
unchanged Windows **5.79 +/- 0.2 MiB** guard; it is 66,560 bytes smaller than the
prior 6,159,360-byte checkpoint. The minimal image is **2,767,360 bytes /
2.63916015625 MiB**, still explicitly uncalibrated. New cache entries/delegate
have a cold-memory cost, but warmed writes remain allocation-free, including
alternating shapes. Cold-start latency and aggregate IL size are not benchmarked;
the image sizes are measured, not inferred from bounded-depth JIT disassembly.
Final Release build and all 3,479 managed tests pass. The accepted native core
suite passes 808 tests with the same two unsupported input-creation skips.
No public API signatures, approved safety behavior, or features are removed.
Linux/macOS remain unexecuted and their budgets are unchanged.

### Reproduction and raw artifacts

Artifact root (intentionally retained outside source control):
`C:\Users\andre\.copilot\session-state\ccd4d45f-edf7-4ad2-bce2-e9f5839aa2f8\files\perf-cause`.
`Measure-History.ps1` contains the exact serial medium command; `Program.cs`,
`Rebuild-ProfileHarnesses.ps1`, and `Collect-Profiles.ps1` reproduce the final
unboxed warm profiles. Benchmark source is the existing `test\Benchmarks` project;
no parallel benchmark project or shipping harness is added.

Every measurement directory includes build logs, per-launch BDN raw logs, CSV and
full JSON reports. `initial-*`, `replicate-*`, `boundary2-*`, and `boundary3-*`
retain historical evidence; `acceptanceA/B/A2/B2-*` retain property-copy acceptance;
`cacheA/B/A2-*`, `cache-nested-*`, and `provider*` retain subsequent experiments.
An early `cache-screen` directory was reused for a candidate repeat: its first
summary JSON/CSV was overwritten, but both timestamped raw BDN logs remain.
Subsequent script runs refuse to overwrite measurement directories.
The final and replicated final reports are separately named and intact.

`unboxed-<sha>-<read-wide|write-small>.nettrace`, `-exclusive.txt`, `-inclusive.txt`,
and `.speedscope.json` are the authoritative paired profiles. Earlier profile
prefixes used a boxed integer write sink and are superseded for write attribution;
this never affected BDN operation timing or its allocation measurement.
`final-managed-build.log`, `final-managed-tests.log`, `final-*-publish.log`,
`final-*-run.log`, `final-broad-size-guard.json`, and `final-minimal-size-report.json`
retain final validation. The detached measurement worktrees and exact rejected
patches are intentionally retained for reproduction; no existing branch/worktree
was reset, overwritten, renamed, or removed, and nothing was pushed.

### Accepted internal property-dispatch copy reduction

Only the internal synchronous `JsonProperty` read/write dispatch takes the context
by readonly reference. Public converters still receive independent by-value
contexts; no public signature, depth, provider, reference, or state semantics change.
Sibling/start-context isolation is covered for both mutable and constructor-bound
objects. The Release solution builds without warnings; all 3,475 managed tests pass.

Matched BDN medium jobs (2 launches, 10 warmups, 15 measured iterations, affinity 2,
.NET 9.0.21 / SDK 10.0.402 / BDN 0.15.8) gave these means in ns:

| Operation | Baseline A | Candidate B | Baseline A2 | Candidate B2 |
|---|---:|---:|---:|---:|
| DeserializeSmall | 336.4 | 335.7 | 337.3 | 318.1 |
| DeserializeWide | 1144.2 | 1080.1 | 1128.4 | 1077.0 |
| SerializeSmall | 212.5 | 194.7 | 215.9 | 199.1 |
| SerializeWide | 548.6 | 543.3 | 548.3 | 497.6 |

The repeatable small-write and wide-read improvements justify the four signature
changes. Small-read and wide-write gains vary between launches/runs; no uniform
percentage gain or complete restoration is claimed. Allocation remains 200/664 B
for reads and zero for writes. Nested DeepChain improves 584.7 to 551.5 ns with
unchanged 152 B; nested Order is inconclusive (2144.2 +/- 39.31 versus
2192.5 +/- 78.21 ns), with unchanged 32 B.
Warm disassembly removes one 80-byte copy from the inlined serialization property
dispatch and reduces the small serialization Core body from 4337 to 4161 bytes.
Raw benchmark/profile evidence is under the current session's `files\perf-cause`;
the final causal report will retain the complete uncertainty and artifact mapping.

### Accepted shape-aware converter locality cache

The `d5b883f` parent had a last-converter shortcut that compared only `T`; removing
it was necessary for provider correctness but adds repeated multi-provider lookup.
A measurement-only restoration recovers the small-write step (223.1 to 196.0 ns)
without changing `in T` or reference registration. This unsafe historical control
is not production code.

The accepted replacement checks exact shape identity and atomically publishes an
immutable shape/converter pair. A resolved-entry dictionary retains each pair, so
alternating warmed shapes do not allocate a new pair on every miss. It preserves
the authoritative converter caches and reference wrappers, including NativeAOT.
Provider alternation, concurrent identity, and zero allocation after warmup have
explicit coverage. This adds one cold entry per resolved shape and a per-cache
factory delegate; no per-operation closure is introduced.

Medium property-fix-only A / locality-cache B / property-fix-only A2 means:
small read 322.4 / 308.2 / 325.2 ns; small write 197.8 / 190.4 / 195.5 ns.
Wide read is inconclusive (1071.7 / 1099.2 / 1060.9 ns, B error 48.55 ns);
wide write is noisy (595.1 / 499.6 / 527.2 ns, A error 100.11 ns).
Other medium cache runs give 302.4 ns small read and 185.5 ns small write.
Do not interpret the noisy wide-write mean as a precise improvement.
Nested chain/order are 530.8/2162.1 ns with unchanged allocations.
The sampled dictionary `FindEntry` hotspot disappears from the top write stacks;
this is sampled attribution, not a precise count of CPU nanoseconds.

Release builds and formatting pass; all 3,479 managed tests pass on repeat.
One initial full run reported 7,624 unexpected bytes in the existing .NET 10
warmed-object allocation test. Its isolated rerun and the full rerun pass unchanged;
the cause of that one-off test-thread allocation is not established.
NativeAOT net10.0 passes 808 tests with the same two platform-input skips
(810 total), including provider/identity/allocation tests.

## HISTORICAL accepted checkpoint (2026-10-20, code `13bc811`)

This section records the **earlier** accepted checkpoint. Its statement of finality
is scoped to that checkpoint; the performance follow-up above supersedes its
elapsed-time result and final-code designation without replacing its historical evidence.

**This was the authoritative result for `13bc811`, not the current source.**
It superseded the earlier managed counts, native input-creation failures, size measurements, pending benchmark comparison, and unverified platform-guard notes retained below as historical evidence.
The accepted implementation is `13bc811`; the rejected provider-local experiment is not included.
The branch remains `aarnott-miniature-engine`; this documentation checkpoint creates no branch or PR, pushes nothing, and amends nothing.
Approved differences above are unchanged. Breaking cleanup remains authorized, with no obsolete aliases restored.
The eventual PR must explain that policy; creating it is future work, not part of this run.

### Final correctness and compatibility evidence

* All **3,467 managed tests pass, zero failures and zero skips**: core on .NET 8/9/10/net472, MVC on .NET 8/9/10, and analyzers on .NET 8. This includes the new warmed object-allocation regression test.
* The Release solution builds all configured target frameworks with **zero warnings and zero errors**.
* The complete Windows net10.0 native core suite has **807 total: 805 passed, zero failed, two explicitly skipped**. Only `Flat_NonZeroLowerBounds_AreNormalized(false/true)` is skipped because NativeAOT cannot create those array inputs before entering the serializer. All managed variants still fully assert normalization. The earlier two failures are superseded platform-input results, not existing serialization bugs or a remaining CI test failure.
* All four NativeAOT fixtures publish with `-warnaserror`, zero warnings/errors, and execute the final code: minimal `AotSizeTest`; `AotRuntimeCompatibilityTest` covering nested records, arrays, dynamic dictionaries, and automatic ordinary/open-generic converter associations; broad `AotNativeConsole`, which prints `Success`; and root-all shipping `AotCompatibilityTest`.
* MVC commits `9fd8126` and `e4562e7` wire the public object async APIs while preserving strict single-JSON HTTP bodies, UTF-8/UTF-16, split reads, BOM handling, ownership, and ModelState/400 behavior. Unterminated trailing block comments are rejected; no MVC test skips remain.
* DocFX with warnings as errors finishes with zero warnings/errors; `dotnet format` verification exits 0; the F# self-check executes successfully.
* The public-API audit and removed-API declaration/usage scan are complete for this checkpoint. JSON-specific `AsyncReader.SkipValueAsync` is not an obsolete `SkipValue` alias. Remaining low-level breadth is explicitly deferred; perfect binary parity is not claimed.

Final Windows native image measurements:

| Native image | Bytes | MiB | Size result |
|---|---:|---:|---|
| Minimal `AotSizeTest` | 2,771,456 | 2.64306640625 | Uncalibrated report |
| Broad `AotNativeConsole` | 6,159,360 | 5.8740234375 | PASS: unchanged Windows 5.79 +/- 0.2 MiB guard |

Historical Linux 5.88 +/- 0.2 MiB and macOS 5.40 +/- 0.2 MiB broad budgets are preserved; neither OS was locally executed or recalibrated.
Final validation artifacts retained outside the checkout include `accepted-native-core-tests.log`, `accepted-broad-size.json`, `accepted-minimal-size.json`, `accepted-docfx.log`, and `closure-fixed-managed-tests.log`.

### Completed matched performance comparison

BenchmarkDotNet 0.15.8, .NET 9.0.21, SDK 10.0.402, Windows 11, Threadripper 7995WX, Release: five warmups, ten iterations, 250 ms iteration time, one launch.
The frozen original `f64` and accepted `13bc811` use matched jobs.
Intervals below are mean +/- 99.9% confidence-interval half-width; repeat means are the original rerun after the candidate.

| Object operation | Original mean (ns) | Accepted mean (ns) | Original repeat mean (ns) | Original and accepted allocations |
|---|---:|---:|---:|---:|
| DeserializeSmall | 302.6 +/- 1.94 | 362.6 +/- 6.38 | 297.8 | 200 B |
| DeserializeWide | 1053.4 +/- 3.74 | 1191.8 +/- 22.84 | 1020.1 | 664 B |
| SerializeSmall | 180.1 +/- 0.66 | 222.6 +/- 1.77 | 177.9 | 0 B |
| SerializeWide | 494.1 +/- 2.37 | 556.9 +/- 6.73 | 478.6 | 0 B |

Object allocation parity is restored, but elapsed cost remains roughly **13-24% higher** in these four microbenchmarks.
The original repeat confirms the original timing range with modest drift; this is not full speed restoration.
No exact causal split between feature/safety work and other costs is claimed.
Elapsed-time optimization remains an explicit unchecked follow-up above.

All **24 final scenarios completed**: four objects, eight interning, and twelve skipping.
All twelve skip scenarios allocate **0 B**. Interner allocations match the original.
Large escaped interned input improved from 801.616 us original to 642.214 us accepted in the matched run, but other string cases were mostly slower; this is not an overall performance-win claim.
Raw baseline, candidate, final accepted, and original-repeat reports are retained outside the checkout by the coordinator as `perf-comparison-baseline`, `perf-comparison-candidate`, `perf-final-accepted`, and `perf-original-repeat`.

**Accepted allocation fix (`13bc811`):** IL attributed the measured +32 B/op regression to unconditional allocation of `<>c__DisplayClass38_0<T>`, capturing `this` and `shape` in `GetOrAddConverter<T>`.
The static state factory removes that 32 B/op, restoring exact allocation parity while preserving atomic cache publication.
The build and full managed/native correctness checks above verify the accepted code.

**Rejected/inconclusive provider-local experiment:** an operation-local provider-assignment mutator eliminated two 80-byte `BulkMoveWithWriteBarrier` copies from each synchronous Core method in disassembly.
Its candidate passed 3,503 managed tests, but wide serialization measured 608.3 versus 522.2 ns across two identical runs, and the baseline also drifted.
The candidate was surgically reverted; accepted `13bc811` is unchanged, and its extra 36 tests are not included in the final 3,467 count.
Patch/evidence remains outside the checkout in child session `9ea17ac4-60dd-4182-ba35-c0d060ada2a2`, under `files/provider-local-experiment`.
This is not an accepted optimization.

### Historical remaining boundaries and deferrals

The following deferrals describe `13bc811` only; the FINAL checkpoint above supersedes the parser/raw/analyzer and Linux status.

Zero-copy segmented parsing, the direct raw stateless wrapper, remaining low-level breadth, NBJson031/NBJson051, elapsed-time optimization, and Linux/macOS execution remain deferred as tracked above.
NBJson001/NBJson002 remain descriptor-only inactive diagnostics, as documented.
Experimental duck typing does not have identical constructor-time validation semantics in every case.
Async-stream first-value reads cannot preserve prefetched bytes when disposing the temporary pipe; use a caller-owned pipe instead.
Supported JSON dictionary-key limits and immutable/constructor-bound cycle limits remain intentional feature constraints, not unimplemented promises.

## HISTORICAL fixed-snapshot integration checkpoint

The sections below preserve historical integration evidence. Their counts, failures, pending work, and verification limitations are superseded where explicitly resolved by the final accepted checkpoint above.

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

The earlier integration pass added only the six explicitly approved behavior/documentation commits below.
The final safety/API follow-ups are recorded separately after this table.

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

### Final safety/API follow-ups

Only navigation source commits `de2b131` and `0574cf8` were imported in the final pass, as `4452bf0` and `0d4f6ee`.
No other branch commits were imported.
The integration preserves lexical scalar boundaries, normalized `JsonSerializationException` causes and actual cancellation, first-value UTF-8/caller-pipe behavior, `in` converter writes, `UnusedDataPacket`, and both union envelope formats.
Raw capture, unknown-property/constructor capture, optional object/dynamic/Expando converters, NameValueCollection custom string capture, and navigation use the active operation context.
The parameterless raw-text convenience remains bounded at depth 64; no shipping converter resets its depth or cancellation budget through that convenience.
Sibling traversal copies contexts instead of decrementing a global budget.
Known flat arrays retain MessagePack-compatible logical-array depth semantics; skipping raw JSON counts actual containers.

`24468b7` completes the object-envelope adaptation for generated unions, with boundary tests for both envelope formats and generated/runtime registrations.
`e805a41` adds the matching constructor-association metadata and nonsealed modifier to `JsonConverterAttribute`, without changing runtime-registered converter activation or its constructor annotations.
Tests assert generated ordinary and closed-generic associations without manual metadata; the runtime native fixture executes both generic instantiations and the ordinary converter.
The JSON DOM modifiers are unchanged.
`fa409fa` restricts XML inheritance to appropriate nodes and documents the actual overload parameters, eliminating all 75 duplicate-parameter warnings without suppressions or lost summaries/ownership documentation.

### Historical verification and deferrals (2026-10-20, superseded)

`dotnet build Nerdbank.Json.slnx -c Release --no-restore -warnaserror` builds all configured targets with **zero warnings and zero errors**.
`dotnet test --no-build -c Release` passes all **3,269 managed tests**, with zero failures or skips:

| Project | Framework | Passed |
|---|---|---:|
| Core, including converter samples | net8.0 | 786 |
| Core | net9.0 | 782 |
| Core | net10.0 | 782 |
| Core | net472 (net48 runtime) | 775 |
| MVC | net8.0 | 26 |
| MVC | net9.0 | 26 |
| MVC | net10.0 | 26 |
| Analyzers | net8.0 | 66 |

`dotnet run --project samples\fs\fs.fsproj --no-build -c Release` executes the self-checking F# sample successfully.

All four native fixtures publish for Windows x64 with `-warnaserror`, **zero warnings and zero errors**, and their native executables exit successfully:
`AotSizeTest`, `AotRuntimeCompatibilityTest`, `AotNativeConsole`, and root-all `AotCompatibilityTest`.
Nested records and arrays no longer FAILFAST; the broad model fixture prints `Success`.
No obsolete union type was restored, and only the existing root-all compatibility fixture roots shipping assemblies.
The final full core NativeAOT suite was published for net10.0/win-x64 with `NativeAOT=true`, `--no-restore`, and `-warnaserror`, with zero warnings/errors.
The historical execution exited 2: **780 succeeded, 2 failed, 0 skipped, 782 total**; the platform guard below supersedes these test-input failures.
Both `JsonMultidimensionalArrayTests.Flat_NonZeroLowerBounds_AreNormalized(False/True)` failures occur at test-input creation in `Array.CreateInstance(Type, Int32[], Int32[])`, with `PlatformNotSupportedException: Arrays with non-zero lower bounds are not supported.`
These two cases now use TUnit's runtime `Skip.Test` guard when `RuntimeFeature.IsDynamicCodeCompiled` is false, explicitly identifying NativeAOT's inability to create non-zero-lower-bound arrays.
All managed round-trip assertions remain intact, including both arguments on net8.0/net9.0/net10.0/net472; all other native array tests remain enabled.
The existing IL3050 suppression now explains this managed-only input requirement; statically referenced ranks do not enable NativeAOT lower-bound support.
The targeted managed flat-test build/run was attempted after priming, but command approval was denied in the non-interactive session, so it did not execute.
The coordinator must rerun the full managed and native suites to verify this correction; no post-correction native pass is claimed.
All new navigation and converter-association tests pass natively, including read-only dynamic dictionary access.
The broader multi-project/multi-framework native traversal was not run.

The final pass successfully publishes with a process-local `C:\Program Files (x86)\Microsoft Visual Studio\Installer` PATH prefix.
The earlier pass's invocation-local MSBuild overrides remain documented in history; no machine environment or repository toolchain configuration was changed.

`tools\Measure-AotImage.ps1` executed both images and produced:

| Native image | Bytes | MiB | Size result |
|---|---:|---:|---|
| Minimal `AotSizeTest` | 2,770,432 | 2.64208984375 | Uncalibrated report |
| Broad `AotNativeConsole` | 6,155,776 | 5.87060546875 | PASS: original Windows 5.79 +/- 0.2 MiB guard |

The Linux 5.88 +/- 0.2 MiB and macOS 5.40 +/- 0.2 MiB historical broad thresholds remain unchanged.
Neither OS was locally measured or recalibrated.

Caller-owned pipes return the first value without discarding prefetched data, including on context/object/DOM overloads.
Scalars require a delimiter or EOF; malformed scalar prefixes remain failures.
UTF-8 buffers and sequences return the first value, while synchronous streams do so after buffering through EOF.
String overloads remain strict documents.
Asynchronous stream deserialization deliberately remains strict because disposing a temporary pipe after a first-value read can discard prefetched bytes; repeated first-value reads require a caller-owned pipe.
NDJSON remains strict about newline framing.
Failures retain causal exceptions and existing serialization error codes; real operation/enumerator cancellation is not weakened.

`dotnet docfx docfx\docfx.json --warningsAsErrors --disableGitFeatures` exits 0 with **zero warnings and zero errors**.
No documentation warning was suppressed.
The refreshed NBJson001/NBJson002 pages correctly describe descriptor-only inactive diagnostics, not implemented analysis.
`dotnet format Nerdbank.Json.slnx --verify-no-changes --no-restore` exits 0.
It reports that F# formatting is unsupported; the F# sample is separately built and executed.

The final logs are `obj\integration-final-managed-tests.log`, `obj\integration-docfx-final.log`, `obj\integration-native-core-publish.log`, and `obj\integration-native-core-tests.log`; size reports are `obj\integration-final-minimal-size.json` and `obj\integration-final-broad-size.json`.
The remaining broader public-API audit, zero-copy parser, stateless raw-wrapper API, NBJson031/NBJson051, and stable performance comparison remain deferred.
Async-stream first-value support remains blocked by temporary-pipe ownership, not enabled with a data-loss-prone workaround.
The twelve skip benchmark scenarios build with zero warnings, but no benchmarks were run during this pass.
Depth checks and periodic cancellation checks add work to skipping/framing; no speedup, throughput, or final-snapshot allocation claim is made.
The coordinator owns the matching stable candidate comparison against its frozen baseline after all builds stop.
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
