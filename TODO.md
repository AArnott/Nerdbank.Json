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
- [ ] Implement the deferred zero-copy segmented parser work.
- [ ] Design the deferred direct stateless raw-wrapper API.
- [ ] Address deferred analyzer rules NBJson031 and NBJson051.
- [ ] Address the explicitly deferred remaining low-level API breadth.
- [x] Investigate and optimize the object elapsed-time regression without sacrificing allocation parity, safety, or correctness; causal boundaries and accepted fixes are recorded below.
- [ ] Further reduce the residual small-object read cost; the accepted fixes do not establish complete original-speed restoration.
- [ ] Execute Linux/macOS validation; do not infer results or recalibrate their budgets from Windows measurements.

## Active performance attribution follow-up (2026-10-20)

The historical checkpoint below is retained as evidence. The attribution follow-up
is complete with accepted code `2fdeb10`, following `f2b7961`; both descend from
the user's unchanged `0aa59ea`. Matched medium historical jobs, parent/child
replication, causal controls, and paired warm sampled-thread-time profiles identify
two concrete regression components. This is a substantial partial recovery, not
a claim that every timing difference across the 64-commit range is decomposed.
Production-default tiering/PGO and constant benchmark inputs are the primary configuration.
Deferred parser/raw-wrapper/analyzer work follows this performance investigation.

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

## Final accepted checkpoint (2026-10-20, code `13bc811`)

This section records the **earlier** accepted checkpoint. Its statement of finality
is scoped to that checkpoint; the performance follow-up above supersedes its
elapsed-time result and final-code designation without replacing its historical evidence.

**This is the authoritative final result.** It supersedes the earlier managed counts, native input-creation failures, size measurements, pending benchmark comparison, and unverified platform-guard notes retained below as historical evidence.
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

### Remaining boundaries and deferrals

Zero-copy segmented parsing, the direct raw stateless wrapper, remaining low-level breadth, NBJson031/NBJson051, elapsed-time optimization, and Linux/macOS execution remain deferred as tracked above.
NBJson001/NBJson002 remain descriptor-only inactive diagnostics, as documented.
Experimental duck typing does not have identical constructor-time validation semantics in every case.
Async-stream first-value reads cannot preserve prefetched bytes when disposing the temporary pipe; use a caller-owned pipe instead.
Supported JSON dictionary-key limits and immutable/constructor-bound cycle limits remain intentional feature constraints, not unimplemented promises.

## Fixed-snapshot integration checkpoint

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
