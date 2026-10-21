# Serialization benchmarks

The benchmark project under `test/Benchmarks/` compares Nerdbank.Json with source-generated System.Text.Json using a reused `ArrayBufferWriter<byte>`. Each invocation creates a writer, serializes an already-constructed model, flushes, observes the byte count, and clears the buffer. Model construction and validation are outside the measured operation.

| Workload | Shape | Coverage |
| --- | --- | --- |
| `ObjectBenchmarks.Small` | Five flat scalar properties | Writer setup and short ASCII values. |
| `ObjectBenchmarks.Wide` | Sixteen flat scalar properties | Repeated property dispatch, property names, strings, numbers, and a boolean. |
| `SerializationWorkloadBenchmarks.Order` | Nested customer/address and 12 line items | Objects, collections, nullable decimals, GUID, timestamp, and a roughly 1 KB payload. |
| `SerializationWorkloadBenchmarks.SensorBatch` | 128 readings in an array | Repeated numeric, boolean, and timestamp properties and a roughly 14 KB payload. |
| `SerializationWorkloadBenchmarks.LogBatch` | 128 log entries in an array | Repeated UTC timestamps, varying durations, and short messages. |
| `SerializationWorkloadBenchmarks.TextDocument` | Repeated escaped ASCII text and tags | Several kilobytes of quotes, backslashes, and control characters. |
| `SerializationWorkloadBenchmarks.UnicodeDocument` | Repeated non-ASCII text and tags | UTF-8 encoding of accented characters, CJK characters, and surrogate pairs. |
| `SerializationWorkloadBenchmarks.DeepChain` | Twelve linked objects | Deeply nested containers, including the inline-to-heap stack transition. This is a boundary case, not a typical payload. |

The additional workloads validate that both serializers produce equivalent parsed JSON before measurement. For the order, sensor, log, and deep-chain workloads, the serialized byte counts also match. For the text workloads, default escaping differs: System.Text.Json writes more bytes for these inputs, especially Unicode, so their time ratios are **not** equal-byte throughput comparisons.

These fixtures are deliberately diverse, not a statistically representative sample of application traffic. They do not cover dictionaries, runtime polymorphism, large binary values, cold metadata creation, streaming, or buffer growth. Their relative importance depends on the application's actual payloads and serializer options.

Run the workload comparison in optimized mode after following the repository's contribution guide:

```powershell
dotnet build .\test\Benchmarks\Benchmarks.csproj -c Release
dotnet run --project .\test\Benchmarks\Benchmarks.csproj --no-build -c Release -- --filter "*SerializationWorkloadBenchmarks*" --job medium
```

BenchmarkDotNet reports elapsed time and allocated bytes separately. Results from a shared or thermally unstable machine can be multimodal; compare repeated runs with the same runtime, affinity, and job before treating a time ratio as a regression or improvement.

Cached object serialization into a reused, sufficiently large buffer should allocate no managed memory when reference preservation is off.
`JsonObjectSerializerTests.Serialize_CachedObjectGraph_DoesNotAllocate` guards this warmed path, including converter lookup with a generated object shape.
NativeAOT converter lookup uses a static state factory so its fallback does not introduce a closure allocation on either managed or native cache hits.

## Contiguous reader validation

**Historical results below describe the former Unicode-validation policy, superseded by the user-directed policy change on 2026-10-21.**
Current readers enforce JSON lexical grammar, not Unicode well-formedness.
CLR strings use replacement UTF-8 decoding and preserve escaped UTF-16 code units; raw byte APIs retain source bytes.
Current writers preserve non-ASCII byte payloads and escape lone CLR surrogates.
The old Unicode prevalidation is removed, not hidden behind an optimization or strictness flag.

### Historical validation optimization

The former policy checked control bytes, malformed UTF-8, and unpaired surrogate escapes, including segmented input.
Printable ASCII can be checked without invoking the general segmented UTF-8 validator for every byte.
Paired warm CPU traces and Tier-1 disassembly identified this per-byte dispatch in both property-name lookup and string-value reading after segmented input was introduced.

On Windows x64, .NET 9.0.21, SDK 10.0.402, BenchmarkDotNet 0.15.8, the medium job (two launches, ten warmups, fifteen iterations) at affinity mask 2 measured small-object reads at 498/504 ns before and 445/436 ns after this validation change.
Wide-object reads changed from 2327/2329 ns to 1589/1592 ns.
Allocations remained 200 B and 664 B respectively; both object writes remained allocation-free.
These are paired local measurements, not cross-platform guarantees or a claim that all segmented-input overhead has been recovered.
The small-object candidate distributions and some baseline runs were noisy; retain the raw distributions and repeat comparisons rather than relying on a single mean.

On .NET 8 and later, contiguous string-byte validation also uses the runtime's vectorized ASCII range search before an outlined strict fallback.
Two medium runs measured wide reads at 1446/1413 ns, versus 1589/1592/1605 ns for the preceding scalar ASCII validation; small reads were 413/410 ns, with the preceding results varying from 412 to 445 ns.
The wide-object improvement repeated, while the incremental small-object timing overlapped the preceding variation.
At that historical checkpoint, older targets retained scalar validation and all targets checked malformed UTF-8, control bytes, and surrogates.

Caching the immutable input length and outlining segmented token slicing and delimiter search further reduced contiguous wrapper work without duplicating the parser.
Two medium runs measured small reads at 379/357 ns and wide reads at 1237/1278 ns; read allocations stayed at 200/664 B and both writes stayed allocation-free.
The input view grows by one `long` field, while segmented navigation still borrows the payload and copies only split tokens when required.
This recovers most, but not all, of the integration regression: the accepted pre-segmented reader measured about 313 ns and 1071–1077 ns without the new strict string validation.
The remaining difference is not attributed solely to validation; these measurements do not isolate every remaining reader cost.
An explicit contiguous string-parser specialization was also tested and removed because its small effect did not justify a second path.

The final Medium run of source `7ea67c8` measured small reads at 351.1 ± 2.13 ns (median 351.1 ns) and wide reads at 1270.5 ± 14.17 ns (median 1269.4 ns), with unchanged 200/664 B allocations.
Writes measured 179.2 ± 1.08 ns (median 178.9 ns) and 479.3 ± 5.62 ns (median 477.3 ns), both at 0 B.
Intervals are mean +/- 99.9% confidence-interval half-width.
Compared with accepted pre-low-level reads near 313/1077 ns, final reads remain about 12%/18% slower; earlier frozen-original Medium reads were 292.9/1055.1 ns, so full speed recovery is not claimed.
The accepted reader fixes are `440b7fe` (printable-ASCII fast path), `8767712` (vectorized range validation with strict fallback), and `6938cba` (cached length and outlined segmented helpers).
Strict validation and segmented-capable navigation remain possible residual contributors, not a fully isolated causal split.
Paired warm sampled-thread-time frame percentages are relative attribution, not elapsed-time ratios.
The full 64-case skip matrix also completed at the same medium job and affinity, covering four token distributions, two sizes, four segment sizes, and cancellable/non-cancellable contexts.
It reported 0–1 B per contiguous operation and 104–107 B per segmented operation across those payload sizes, consistent with bounded navigation state rather than payload materialization; the small differences within those ranges were not separately attributed.
These final-only skip measurements do not establish a paired skip-throughput improvement.

## User-directed Unicode policy (2026-10-21)

A fresh `a225be3` baseline and policy-only candidate used the Windows/runtime/SDK/job/affinity settings above, serially, with normal tiering and dynamic PGO.
This is an intentional data-policy change, not a semantics-preserving optimization.
There is no general Unicode prevalidation on reads or UTF-8 string writes.
JSON quotes, escapes, control bytes, numeric grammar, depth, cancellation, and document-framing contracts are unchanged.

| Operation | Revision | Mean, ns | Error, ns | SD, ns | Median, ns | B/op |
|---|---|---:|---:|---:|---:|---:|
| Small read | `a225be3` | 373.8 | 15.54 | 21.27 | 385.4 | 200 |
| Small read | Policy only | 373.7 | 4.85 | 6.95 | 373.9 | 200 |
| Wide read | `a225be3` | 1270.7 | 29.36 | 41.15 | 1266 | 664 |
| Wide read | Policy only | 1244.8 | 24.22 | 34.73 | 1237 | 664 |
| Small write | `a225be3` | 183.0 | 5.61 | 8.22 | 180.1 | 0 |
| Small write | Policy only | 183.0 | 4.97 | 7.28 | 182.1 | 0 |
| Wide write | `a225be3` | 484.8 | 10.92 | 15.30 | 478.9 | 0 |
| Wide write | Policy only | 495.8 | 6.92 | 10.35 | 494.6 | 0 |

Object throughput changes overlap noise; no isolated object speedup is claimed.
The segmented unescaped Unicode string moved from 3.632 +/- 0.0515 us to 3.243 +/- 0.1056 us, with unchanged 1496 B/op.
The escaped-string measurements were highly variable (baseline 10.569 +/- 1.572 us, candidate 11.577 +/- 1.767 us); their timing is inconclusive, not a demonstrated regression or improvement.
The new `SegmentedTokenBenchmarks` holds number, property-name, escaped-string, and unescaped-string inputs outside measurement so subsequent scratch-storage work has its own allocation baseline.
Its split-number and property-name policy-only results are 11.768 +/- 0.1387 us at 1384 B/op and 6.109 +/- 0.1742 us at 2024 B/op.
Full logs, CSVs, and distributions are retained outside source control in session `37e160af-a898-413e-9aab-72e6a566cbe9`, `files\decoder-perf`, under `baseline`, `baseline-tokens`, and `policy`.

## Caller-owned scratch and remaining read cost

Commit `ef6e8f9` replaces implicit split-token arrays with borrowed spans, caller-owned fixed stack scratch, and pooled fallbacks for larger tokens.
The scratch threshold is not a token-length limit: long valid numeric spellings and property names remain supported.
Segmented strings retain decoder state across batches and flush it at escape boundaries so malformed UTF-8 has the same replacement behavior as contiguous input.
Ownership-bearing raw results still copy when required; temporary scratch never escapes into a returned value.

Policy-only `7e2e3cf` and the scratch candidate used serial Release Medium jobs on Windows x64, .NET 9.0.21, SDK 10.0.402, BDN 0.15.8, affinity mask 2, normal tiering/PGO, two launches, ten warmups, and fifteen measurement iterations.
Errors below are 99.9% confidence-interval half-widths.

| Segmented operation | Policy-only mean +/- error, us | Scratch mean +/- error, us | Policy-only repeat mean +/- error, us | B/op before / after |
|---|---:|---:|---:|---:|
| Numbers | 13.361 +/- 1.151 | 13.245 +/- 0.634 | 11.716 +/- 0.250 | 1384 / 104 |
| Property names | 6.097 +/- 0.109 | 6.960 +/- 0.380 | 6.137 +/- 0.084 | 2024 / 768 |
| Unescaped string | 3.301 +/- 0.068 | 3.466 +/- 0.057 | 3.232 +/- 0.028 | 1496 / 1024 |
| Escaped string | 12.332 +/- 1.604 | 10.406 +/- 0.310 | 10.404 +/- 1.521 | 3584 / 1024 |

The allocation reduction is established, but this is not a general throughput improvement.
Against the repeated baseline, numbers and property names are approximately 13% slower and unescaped strings approximately 7% slower; escaped-string distributions are noisy and do not establish a speedup.
The allocation-oriented implementation is retained with those explicit tradeoffs.
Contiguous object read allocations remain 200/664 B and writes remain allocation-free.
The same scratch comparison measured small/wide object reads at 365.5/1294.1 ns before and 366.5/1264.5 ns after; writes were 186.4/481.8 ns before and 190.2/507.5 ns after.
Those object timings do not establish complete speed restoration or an isolated scratch-induced write regression, since the writer is unchanged and later unchanged-source controls drift too.

### Rejected property-hash experiment

Fresh warmed managed-thread-time profiles at `ef6e8f9` attribute 43.1%/49.5% of small/wide exclusive samples to object population, including inlined lookup and dispatch work.
String reads account for another 12.2%/11.5%, and insignificant-character scanning 8.4%/6.0%.
These are attribution samples, not precise on-CPU measurements or proof that hashing alone dominates.
The removed Unicode prepass is not the remaining hotspot; ordinary UTF-8-to-CLR-string decoding is still necessary.
Disassembly suggested one narrow hypothesis: unroll the serial base-31 property-name hash four bytes at a time while preserving every hash value.

| Operation | A1 baseline mean +/- error (median), ns | B candidate mean +/- error (median), ns | A2 baseline mean +/- error (median), ns | B/op, all |
|---|---:|---:|---:|---:|
| Small read | 365.6 +/- 6.9 (362.5) | 360.6 +/- 5.4 (359.1) | 382.8 +/- 13.5 (375.5) | 200 |
| Wide read | 1272.7 +/- 32.6 (1257.3) | 1258.1 +/- 41.0 (1283.3) | 1271.3 +/- 10.0 (1269.6) | 664 |
| Small write | 177.1 +/- 2.7 (176.4) | 179.6 +/- 5.2 (183.7) | 187.2 +/- 9.1 (180.9) | 0 |
| Wide write | 468.0 +/- 5.9 (466.4) | 465.8 +/- 5.6 (467.2) | 495.2 +/- 25.9 (489.5) | 0 |

The serial Medium A/B/A experiment used the same settings and explicitly retained the user's existing power plan.
The apparent 1% read-mean improvement overlaps uncertainty, the wide-read median worsens, and controls show drift and bimodality.
Generated assembly changed as intended, but the candidate does not demonstrate a repeatable worthwhile improvement and is rejected.
Its correctness-test command was permission-blocked, so no candidate correctness pass is claimed; it is not on the accepted branch.
Identical baseline source also produced different native method sizes under dynamic PGO, so neither code-size nor startup improvements are claimed.
The rejected implementation is retained locally at `perf/rejected-property-hash-unroll`, commit `8ac0e83`, without merging it.

The original context-copy/cache changes and the low-level reader's former validation/segmented-access costs have measured causal evidence and accepted fixes, as recorded above and in `TODO.md`.
The residual gap relative to the pre-segmented reader is still open; the latest profile narrows where execution time remains but does not establish another causal change or complete recovery.
Hardware counters were not collected.
Scratch reports are retained in session `70b1bb69-b0bc-449e-8893-8e6ae0ea5d2d`, under `files\scratch-medium-policy-only`, `files\scratch-medium-scratch-candidate`, and `files\scratch-medium-baseline-repeat`.
Paired profiles, disassembly, full distributions, extracted statistics, and the rejected patch are retained in session `79c835e9-2a31-4687-b0fa-7738581900dd`, under `files\contiguous-perf`.

## Historical Linux Medium comparison

An isolated WSL Ubuntu 26.04 Linux-native checkout measured the frozen original `f64da69` and current `7ea67c8` with SDK 10.0.402, .NET 9.0.21, BDN 0.15.8, Release, affinity mask 2, default tiering, two launches, ten warmups, and fifteen iterations.
The runs were serial with no other builds.
Errors are 99.9% confidence-interval half-widths; allocations are unchanged.

| Operation | Original mean +/- error, ns | Original median, ns | Current mean +/- error, ns | B/op, both |
|---|---:|---:|---:|---:|
| Small read | 352.7 +/- 21.77 | 348.7 | 398.9 +/- 3.31 | 200 |
| Wide read | 1124.8 +/- 16.03 | 1122.8 | 1400.4 +/- 14.85 | 664 |
| Small write | 186.4 +/- 6.68 | 194.1 | 200.6 +/- 1.48 | 0 |
| Wide write | 508.8 +/- 6.09 | 511.6 | 526.2 +/- 4.67 | 0 |

This is one like-for-like Linux pair, not replicated A/B/A evidence or a cross-OS performance comparison.
The original small-read distribution is noisier; minimum-iteration warnings and outliers are retained.
All original object allocation parity targets are met, but neither this pair nor the Windows results demonstrate full elapsed-time recovery.

The earlier provider-initialization copies and removed converter-locality shortcut were separately established as causal regressions using historical parent/child Medium jobs and counterfactuals.
Their accepted fixes preserve public converter by-value isolation and exact provider/shape identity; the unsafe historical type-only shortcut is not shipping code.
The repository's `TODO.md` retains those controls, accepted/rejected decisions, and full artifact mapping.
Reader raw evidence is outside source control in session `6fbada64-d1bf-444d-be95-97580c7b0685`, `files/reader-perf`, including `RootCause.txt`, `object-measurements.csv`, traces, disassembly, and rejected patches.
Earlier causal evidence remains in session `ccd4d45f-edf7-4ad2-bce2-e9f5839aa2f8`, `files/perf-cause`.
Linux evidence includes `perf-linux-medium-baseline-f64/repo` and host logs `final-linux-medium.log` and `final-linux-perf-validation-summary.log`.

## Final scalar and direct-buffer API impact

Source `a28b0ef` completes overloaded scalar `TryRead`/`TryWrite` APIs, including every 8- through 64-bit integer width and single precision, and adds complete-value writer `GetSpan`/`Advance` reservations.
These APIs are retained for parity and direct-buffer use, without treating residual throughput work as an API-finalization gate.
The writer adds an internal reservation-length field and reservation-clearing stores; their padded-size/startup effects have not been isolated, and no public contract requires that representation.

Serial Windows x64 Release Medium jobs used .NET 9.0.21, SDK 10.0.402, BDN 0.15.8, affinity 2, normal tiering/PGO, two launches, ten warmups, fifteen measurement iterations, and the existing user power plan.
The baseline is `ac0ad15`; the candidate has the shipping implementation at `81f3fbd` plus the subsequently committed benchmark fixture.
Errors are 99.9% confidence-interval half-widths.

| Object operation | Before mean +/- error (median), ns | After mean +/- error (median), ns | B/op, both |
|---|---:|---:|---:|
| Small read | 365.9 +/- 9.1 (369.9) | 378.0 +/- 7.3 (376.9) | 200 |
| Wide read | 1301.2 +/- 46.4 (1290.6) | 1331.8 +/- 68.6 (1280.6) | 664 |
| Small write | 180.1 +/- 3.6 (179.8) | 178.6 +/- 1.1 (178.3) | 0 |
| Wide write | 465.0 +/- 3.5 (464.3) | 469.0 +/- 6.2 (464.9) | 0 |

The read means move upward by approximately 3.3%/2.3%, but confidence intervals overlap and the wide-read median moves downward.
This pair does not establish a new causal regression or a speedup; the unchanged reader paths are controls, not evidence that writer reservation bookkeeping caused their variation.
Object allocation parity remains intact.
Minimum-iteration warnings and outliers remain in the raw reports; this is one matched pair, not replicated A/B/A evidence.

| Scalar operation | Stateful API mean +/- error, ns | Primitive API mean +/- error, ns | B/op, both |
|---|---:|---:|---:|
| Read Int32 | 44.26 +/- 1.10 | 25.73 +/- 0.37 | 0 |
| Read Single | 66.09 +/- 1.55 | 47.61 +/- 1.47 | 0 |
| Write Single | 101.42 +/- 2.27 | 104.32 +/- 1.55 | 0 |

`PrimitiveBenchmarks` validates inputs and equivalent written bytes outside measurement and compares exact, already-framed scalar tokens rather than complete serializer workloads.
The lower-level reads avoid reader setup and insignificant-character handling; that is useful specialization, not proof of general serializer acceleration.
The direct-buffer writer case also uses transactional primitive formatting and is not faster than ordinary scalar writing in this sample; the APIs allow callers to encode directly but do not guarantee that every encoder is cheaper.
Legacy numeric formatting/rounding behavior is documented separately in the [converter guide](custom-converters.md); these timing/allocation results are for modern .NET 9 only.
Full reports, distributions, medians, and allocation counts are retained in session `70b1bb69-b0bc-449e-8893-8e6ae0ea5d2d`, under `files\api-final-medium-baseline` and `files\api-final-medium-candidate`.

## Contiguous lexical string scanning

A renewed investigation at `75a817c` targeted the byte-at-a-time CLR string scanner identified in warmed profiles, not Unicode prevalidation (which is already removed).
The retained implementation scans a short ordinary prefix through the borrowed contiguous span, then uses an outlined SIMD search for quotes, backslashes, and all 32 ASCII controls.
The original delimiter/error handlers retain escape semantics and exact failure positions; segmented and legacy inputs keep their scalar loop.
Search state is held outside the reader struct, and early escapes do not pay for the vector helper.
This does not change public APIs, Unicode fallback policy, ownership, token limits, or allocations.

Serial Windows x64 .NET 9.0.21 Release Medium runs used SDK 10.0.402, BDN 0.15.8, affinity 2, default tiering/PGO, two launches, ten warmups, fifteen measurements, and the user's existing power plan.
The original source was repeated as A2; the final candidate B5 was also repeated.
Errors below are 99.9% confidence-interval half-widths, and all allocation columns are unchanged.

| String-array case | Original A2 mean +/- error, us | B5 mean +/- error, us | B5 repeat mean +/- error, us | B/op |
|---|---:|---:|---:|---:|
| Plain | 3.239 +/- 0.047 | 3.094 +/- 0.041 | 3.128 +/- 0.041 | 5288 |
| Plain, interned | 4.076 +/- 0.085 | 4.078 +/- 0.112 | 4.135 +/- 0.118 | 2592 |
| Escaped | 10.704 +/- 0.266 | 10.728 +/- 0.202 | 10.473 +/- 0.264 | 6312 |
| Escaped, interned | 12.096 +/- 0.325 | 12.000 +/- 0.134 | 11.813 +/- 0.229 | 2608 |
| Unicode escapes | 9.352 +/- 0.280 | 9.194 +/- 0.090 | 9.421 +/- 0.224 | 4776 |
| Unicode escapes, interned | 10.500 +/- 0.228 | 10.414 +/- 0.280 | 10.311 +/- 0.357 | 2584 |
| Large escaped | 576.088 +/- 4.977 | 79.548 +/- 2.552 | 77.893 +/- 2.209 | 1052328 |
| Large escaped, interned | 833.474 +/- 12.622 | 345.262 +/- 3.561 | 343.543 +/- 2.984 | 18952 |

The large escaped cases are approximately 86%/59% faster without/with interning; these are 64-string fixtures, not per-string or general serializer speedup claims.
Normal object read controls do not demonstrate full recovery: original A2 small/wide means were 378.0/1306.1 ns, B5 386.8/1345.1 ns, and B5 repeat 394.0/1377.8 ns.
Their intervals, launch distributions and write-control drift prevent attributing these small upward moves conclusively; no normal-object speedup is claimed, and this remains an explicit investigation lead rather than hidden performance debt.
Segmented B5 controls retain 104/768/1024/1024 B allocations and are 14.486/6.746/3.593/10.337 us, versus A2 13.915/7.049/3.592/10.562 us, with noisy numeric/name distributions.

Earlier candidates are preserved locally: `47be292` on `perf/lexical-string-scan` gates inside the loop, `199c9cb` on `perf/lexical-token-gate-b2` moves the gate before it, `72a4f19` on `perf/lexical-isolated-b3` splits the entire contiguous parser, and `5f6ffc4` on `perf/lexical-guarded-b4` outlines only search.
They retain the large-string gains but have worse segmented or short escaped-string tradeoffs; they are not merged.
The final candidate passes all 4,841 managed tests, including 432 exhaustive lexical cases across .NET 8/9/10 and .NET Framework, with explicit scalar-prefix/SIMD thresholds, malformed UTF-8, escaped code units, segment splits, interning, and exact failure consumption.
Native/startup impact is a separate validation item; no startup or native-size improvement is inferred from these throughput results.
All distributions, medians and raw measurements are retained in session `70b1bb69-b0bc-449e-8893-8e6ae0ea5d2d`, under `files\lexical-scan-baseline-A1`, `files\lexical-scan-baseline-A2`, `files\lexical-scan-candidate-B1` through `B5`, and `files\lexical-scan-confirm-B5`.

## Native image size

Native executable size is a separate measurement from throughput and managed allocations.
The minimal [AotSizeTest fixture](https://github.com/AArnott/Nerdbank.Json/blob/main/test/AotSizeTest/Program.cs) round-trips a generated record/array contract and enables compiler `.mstat` and dependency reports.
It measures reachable code for that small workload, not the entire serializer feature surface.
The [measurement script](https://github.com/AArnott/Nerdbank.Json/blob/main/tools/Measure-AotImage.ps1) executes the published image and records its exact byte count, RID, and successful exit status.
A successful report alone does not establish a calibrated size budget or broad runtime compatibility.

Compare sizes only for the same fixture, RID, SDK, and publish configuration.
The minimal size report must supplement rather than replace the broader [AotNativeConsole fixture](https://github.com/AArnott/Nerdbank.Json/blob/main/test/AotNativeConsole/Program.cs) and the runtime and size regression guard for that workload.
The root-all [AotCompatibilityTest fixture](https://github.com/AArnott/Nerdbank.Json/blob/main/test/AotCompatibilityTest/Program.cs) serves a different purpose: checking the shipping API surface with the native compiler.
Do not root the entire shipping assembly in the minimal size fixture, because doing so would change the measurement.
These fixtures describe the validation surfaces; native execution and size results must be verified for the integrated code before claiming they pass.
