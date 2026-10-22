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

## Forward-only numeric token scanning

Warmed segmented profiles attributed 52.4% exclusive managed-thread samples to byte-cursor access and another 8.9% to casts.
The integer leading-zero check reread the token's first digit after scanning it; when digits crossed segments, that backward access reset the cached byte cursor to the entire sequence's start.
An originally constant-time span lookup became repeated whole-prefix traversal after borrowed segmented input was introduced in `3737b21`; `088ae4f` later moved the same pattern into the shared stateless lexer.
Retaining the first integer byte, including the byte following a minus sign, removes that lookback without changing the digit loop, grammar, cancellation cadence, final-block classification, token storage, or public APIs.

`NumberTokenScalingBenchmarks` constructs deterministic arrays of `12345.625` outside measurement and validates every value and final source position.
The same serial Windows/.NET 9 Medium settings as the lexical experiment apply.
The original lexer was measured twice; errors are 99.9% confidence-interval half-widths.

| Count / segment bytes | Original A1 mean, us | Original A2 mean +/- error, us | Forward-only mean +/- error, us | B/op, unchanged |
|---|---:|---:|---:|---:|
| 32 / contiguous | 1.697 | 1.752 +/- 0.103 | 1.739 +/- 0.039 | 0 |
| 256 / contiguous | 13.680 | 13.080 +/- 0.195 | 13.987 +/- 0.271 | 0 |
| 1024 / contiguous | 55.005 | 52.733 +/- 1.445 | 55.343 +/- 0.885 | 0 |
| 32 / 3 | 14.889 | 13.785 +/- 0.527 | 7.478 +/- 0.762 | 104 |
| 256 / 3 | 509.432 | 502.584 +/- 10.573 | 49.785 +/- 0.497 | 104 |
| 1024 / 3 | 7309.659 | 7255.690 +/- 261.187 | 218.070 +/- 16.889 | 104 |
| 32 / 64 | 3.998 | 3.796 +/- 0.092 | 3.742 +/- 0.096 | 104 |
| 256 / 64 | 32.369 | 32.042 +/- 0.837 | 29.718 +/- 0.673 | 104 |
| 1024 / 64 | 161.799 | 160.152 +/- 8.064 | 119.108 +/- 1.490 | 104 |

Tiny-segment growth is now approximately linear rather than quadratic: the 1,024-value case falls from about 7.3 ms to 0.218 ms, roughly 33 times faster, without increasing allocation.
This is segmentation-dependent scaling evidence, not a 33-times general serializer claim.
Contiguous controls vary between baseline repeats and do not establish a new speedup; ordinary object allocation targets remain 200/664/0/0 B.
The fixed 32-number segmented control repeats at 6.70/6.98 us versus original 13.51-14.43 us, all 104 B.
An unrelated escaped-string control was noisy: one candidate run measured 14.71 us versus original 10.55-10.61 us; a repeat measured 11.59 +/- 1.12 us with a 10.83 us median.
No source in that string path changes here, but the anomaly is retained rather than silently discarded or called a causal regression.
Paired warmed profiles move byte-cursor exclusive share from 52.4% to 19.7%, with split-token copying/parsing becoming more prominent; samples are attribution, not elapsed-time or kernel CPU counts.
All 5,013 managed tests pass; new tests were first checked against the original parser, retaining existing legacy zero-sign semantics and exact source positions rather than changing unrelated behavior.
Full reports and paired traces are retained in session `70b1bb69-b0bc-449e-8893-8e6ae0ea5d2d`, under `files\numeric-forward-baseline-A1`, `files\numeric-forward-baseline-A2`, `files\numeric-forward-candidate-B1`, `files\numeric-forward-confirm-B2`, and `files\segmented-*-baseline` / `files\segmented-*-forward-cache`.

## Segmented span-probe traversal

After the numeric rewind fix, profiles still show token-end seeking during a span-availability query followed by the same seeking again when a split token is copied.
The retained query resolves the cached start position, validates the full requested range, and inspects the first memory block without walking to the token's end.
Copy/sequence-returning paths retain full slicing and the same independent cursors; no storage, ownership, table layout, or public API changes.

Fresh serial .NET 9 Medium A/B/A/B runs use the settings above; errors are 99.9% confidence-interval half-widths.

| Operation | Original A1 mean +/- error, us | Candidate B1 mean +/- error, us | Original A2 mean +/- error, us | Candidate B2 mean +/- error, us | B/op, all |
|---|---:|---:|---:|---:|---:|
| Numbers | 6.717 +/- 0.364 | 6.841 +/- 0.489 | 6.712 +/- 0.301 | 6.030 +/- 0.211 | 104 |
| Property names | 6.736 +/- 0.411 | 6.513 +/- 0.133 | 7.617 +/- 0.662 | 6.221 +/- 0.060 | 768 |
| Unescaped string | 3.761 +/- 0.198 | 3.599 +/- 0.047 | 3.832 +/- 0.143 | 3.568 +/- 0.093 | 1024 |
| Escaped string | 10.790 +/- 0.255 | 10.848 +/- 0.379 | 11.198 +/- 0.502 | 10.479 +/- 0.227 | 1024 |

The repeat supports a modest benefit for numbers/property names (about 10%/8% versus A1); the first numeric pair alone did not establish a win, and string-case changes overlap uncertainty.
Unchanged write controls drifted upward during the first candidate run, so these are not blanket or normalized serializer throughput claims.
The full 5,069-test managed matrix passes, plus four subsequently added non-array-backed memory-manager cases (60 total span-contract cases across the four core frameworks).
Contract tests compile the actual internal input source without expanding shipping visibility, and cover every small range, whole-segment boundaries, empty segments, invalid ranges/exceptions, >2 GiB logical offsets, borrowed identity, copies, and independent peeks.
Reports are retained in session `70b1bb69-b0bc-449e-8893-8e6ae0ea5d2d`, under `files\span-probe-baseline-A1`, `files\span-probe-baseline-A2`, `files\span-probe-candidate-B1`, and `files\span-probe-confirm-B2`.

### Division-free power-of-two property buckets

Warmed object profiles and disassembly identify property lookup arithmetic in `PopulateReference`/`FindSplitProperty` as another cost after lexical scanning.
The table still has exactly one bucket per property; only positive power-of-two counts cache a mask. All other counts, including the empty-table sentinel, keep the original positive-hash modulus.
Hashing, collision order, unknown-property behavior, and public APIs are unchanged. The cost is one metadata `int` per table (potentially additional alignment padding) and a branch, not a new per-read allocation.
Captured disassembly confirms baseline signed `idiv` and candidate mask-test/AND with a cold modulus fallback; the helper remains inlined in the reader.

Serial .NET 9 Medium A/B/A/B results use the same environment and defaults as above; errors are 99.9% confidence-interval half-widths.

| Operation | Original A1 mean +/- error, ns | Candidate B1 mean +/- error, ns | Original A2 mean +/- error, ns | Candidate B2 mean +/- error, ns | B/op, all |
|---|---:|---:|---:|---:|---:|
| Small read (five properties, modulus fallback) | 402.91 +/- 12.50 | 402.79 +/- 5.15 | 430.36 +/- 26.17 | 415.05 +/- 16.60 | 200 |
| Wide read (sixteen properties, masked) | 1435.70 +/- 68.21 | 1372.45 +/- 71.17 | 1414.95 +/- 35.89 | 1334.37 +/- 38.40 | 664 |
| Small write control | 248.10 +/- 37.93 | 187.73 +/- 2.48 | 203.31 +/- 8.45 | 209.07 +/- 6.78 | 0 |
| Wide write control | 583.54 +/- 46.85 | 520.88 +/- 9.26 | 522.98 +/- 18.37 | 512.35 +/- 3.81 | 0 |

Wide medians are 1459.72/1415.89 ns original and 1337.22/1328.84 ns candidate; both comparisons improve, with a roughly 5.7% mean reduction in the repeat. The initial mean intervals overlap, and small-read differences remain inconclusive.
The original A1 write controls were unstable and much slower than later runs, despite no write-path change; these measurements are not normalized against writes or promoted into blanket serializer speed claims.
The full 5,117-test managed matrix passes, including zero/singleton bucket counts, high-bit hashes, collision chains at power-of-two and other sizes, escaped/Unicode names, misses and case-insensitive fallback.
Reports and disassembly are retained in session `70b1bb69-b0bc-449e-8893-8e6ae0ea5d2d`, under `files\bucket-mask-baseline-A1`, `files\bucket-mask-baseline-A2`, `files\bucket-mask-candidate-B1`, and `files\bucket-mask-confirm-B2`.

### Per-block segmented delimiter search

After numeric cursor rewinds were removed, warmed property-name profiles still attributed 6.64% exclusive / 24.22% inclusive sampled thread time to bytewise segmented delimiter search, with byte access itself at 26.52% exclusive.
The retained search positions the existing byte cursor once per memory block, then borrows the remaining span and uses `IndexOfAny` for quote/backslash. It does not move the independent token-slice cursor, add state or allocations, validate Unicode, or treat controls as delimiters; callers retain lexical validation and failure positions.
The existing byte cursor still handles backward searches, empty/clipped segments and long offsets. The contiguous path is unchanged.

`SegmentedStringRunBenchmarks` makes an early escape followed by a 32- or 4,096-byte ordinary run, with 3/64/4,096-byte blocks; the short cases fitting one block are contiguous controls.
Setup validates the result and full byte consumption. Existing tiny-segment numbers, names, escaped/unescaped strings and ordinary objects remain fixed controls.
Serial .NET 9 Medium A/B/A/B results use the settings above; all times below are ns and errors are 99.9% confidence-interval half-widths.

| Operation | Original A1 mean +/- error | Candidate B1 mean +/- error | Original A2 mean +/- error | Candidate B2 mean +/- error | B/op, all |
|---|---:|---:|---:|---:|---:|
| ObjectDeserializeSmall_NerdbankJson | 413.91 +/- 21.46 | 413.05 +/- 46.29 | 400.65 +/- 16.81 | 376.47 +/- 8.58 | 200 |
| ObjectDeserializeWide_NerdbankJson | 1281.66 +/- 11.22 | 1377.69 +/- 102.40 | 1321.93 +/- 25.00 | 1257.77 +/- 25.11 | 664 |
| ObjectSerializeSmall_NerdbankJson | 196.77 +/- 4.04 | 217.20 +/- 35.14 | 196.66 +/- 4.73 | 187.35 +/- 4.78 | 0 |
| ObjectSerializeWide_NerdbankJson | 493.63 +/- 8.72 | 605.74 +/- 107.78 | 497.80 +/- 17.80 | 491.14 +/- 9.94 | 0 |
| SegmentedStringRunReadString(RunLength: 32, SegmentSize: 3) | 555.53 +/- 6.74 | 535.85 +/- 27.96 | 554.91 +/- 15.66 | 510.72 +/- 17.61 | 192 |
| SegmentedStringRunReadString(RunLength: 32, SegmentSize: 64) | 73.18 +/- 2.75 | 75.80 +/- 2.94 | 77.57 +/- 2.79 | 74.54 +/- 1.89 | 88 |
| SegmentedStringRunReadString(RunLength: 32, SegmentSize: 4096) | 72.40 +/- 0.56 | 75.55 +/- 1.62 | 82.80 +/- 6.87 | 75.51 +/- 0.80 | 88 |
| SegmentedStringRunReadString(RunLength: 4096, SegmentSize: 3) | 54788.00 +/- 854.30 | 52299.81 +/- 1611.38 | 56632.50 +/- 1367.50 | 50946.50 +/- 1447.31 | 8376 |
| SegmentedStringRunReadString(RunLength: 4096, SegmentSize: 64) | 30245.02 +/- 200.34 | 3247.80 +/- 143.99 | 30373.91 +/- 660.20 | 3031.42 +/- 74.57 | 8376 |
| SegmentedStringRunReadString(RunLength: 4096, SegmentSize: 4096) | 28799.34 +/- 215.65 | 833.34 +/- 13.20 | 29317.11 +/- 770.01 | 846.11 +/- 31.07 | 8376 |
| SegmentedTokenNumbers | 5740.14 +/- 91.87 | 5944.53 +/- 47.63 | 5796.19 +/- 48.54 | 6081.21 +/- 74.32 | 104 |
| SegmentedTokenPropertyNames | 6204.75 +/- 417.57 | 6094.41 +/- 84.34 | 5910.51 +/- 121.55 | 6677.48 +/- 656.60 | 768 |
| SegmentedTokenUnescapedString | 3547.93 +/- 174.13 | 3487.22 +/- 84.09 | 3447.64 +/- 36.19 | 3622.92 +/- 161.04 | 1024 |
| SegmentedTokenEscapedString | 10333.97 +/- 243.44 | 8704.46 +/- 170.24 | 10271.50 +/- 87.45 | 9073.60 +/- 325.74 | 1024 |

For the 4,096-byte run, repeated original/candidate comparisons improve about 10x at 64-byte blocks and 35x at 4,096-byte blocks; three-byte fragmentation remains dominated by navigation/copying and improves only about 5-10%.
The existing dense escaped-string case improves about 12-16%, with unchanged allocations. This is not a general segmented-string or object-speed claim.
The numeric control instead becomes about 3.6%/4.9% slower in the two candidate runs despite not calling delimiter search; this repeatable adjacent cost remains an explicit follow-up, not a discarded outlier or an attributed scanning tradeoff. JIT/code-layout effects are a hypothesis, not a demonstrated cause.
Property-name and unescaped-string controls vary, and B1 object/write controls are particularly unstable. B2 object reads also improve without using the new segmented path; these uncontrolled changes are not claimed as wins.
All 5,173 managed tests pass. Of 116 direct input-contract cases, the original source passes 92 functional cases and fails exactly the 24 new deterministic per-block work-count cases; the candidate passes all 116.
Tests compare bytewise cursor/result parity, all non-delimiter byte values, empty/clipped/truncated inputs, backward searches, source mutation, borrowed/non-array-backed storage, save/restore, independent peeks/shared copies, and offsets beyond 2 GiB.
The work-count test proves at most two memory-span accesses per visited nonempty block rather than per character, without a timing assertion.
Reports are retained in session `70b1bb69-b0bc-449e-8893-8e6ae0ea5d2d` under `files\segment-runs-baseline-A1`, `files\segment-runs-baseline-A2`, `files\segment-runs-candidate-B1`, and `files\segment-runs-confirm-B2`.

### Rejected: direct-stack numeric scratch setup

The post-rewind numeric profile attributes 24.82% exclusive sampled time to the segmented Double helper, which includes inlined copying/materialization work.
A candidate copied short split tokens directly into 128-byte stack scratch and parsed without returning a disposable token, outlining the unchanged pooled path for longer spellings into eleven cold helpers.
All 5,205 managed tests passed, including new 127/128/129/4,096-byte fraction/overflow spellings across floating and integer types; the new contract tests also passed on the original source.
`SegmentedNumericScratchBenchmarks` is retained to exercise short and pooled Single/Double/Decimal spellings, alongside the existing array-scaling and adjacent controls.

The candidate is **rejected**: neither single-token Double nor the fixed numeric-array control improves meaningfully, while several Single/Decimal/long-token results worsen.
The initial 32-value scaling cell is bimodal/noisy (original median 8,410.75 ns, candidate 5,669.91 ns), unlike the stable independent 32-number control; it is not evidence of a general win.
Disassembly shows both short-token helpers still reserve a 0x168-byte frame; `ReadDoubleSegmented` itself grows from 1,531 to 1,847 native bytes, excluding the additional pooled helper.
Removing the disposable source syntax did not remove the dominant sequence-copy setup and does not justify extra methods/IL.
The production candidate is preserved only on local `perf/numeric-scratch-setup` at `90f3982`, not on the PR branch; only reusable tests/benchmarks and this evidence are retained.

Matching serial .NET 9 Medium A/B results follow, in ns with 99.9% confidence-interval half-widths and unchanged allocations.

| Operation | Original A1 mean +/- error | Candidate B1 mean +/- error | B/op, both |
|---|---:|---:|---:|
| NumberTokenScalingNumbers(ValueCount: 32, SegmentSize: 0) | 1742.14 +/- 19.19 | 1850.70 +/- 40.57 | 0 |
| NumberTokenScalingNumbers(ValueCount: 32, SegmentSize: 3) | 7609.51 +/- 884.73 | 5711.34 +/- 485.45 | 104 |
| NumberTokenScalingNumbers(ValueCount: 32, SegmentSize: 64) | 3609.55 +/- 110.25 | 3727.39 +/- 94.87 | 104 |
| NumberTokenScalingNumbers(ValueCount: 256, SegmentSize: 0) | 14202.16 +/- 166.94 | 14182.21 +/- 166.02 | 0 |
| NumberTokenScalingNumbers(ValueCount: 256, SegmentSize: 3) | 47760.01 +/- 356.98 | 49043.20 +/- 1148.92 | 104 |
| NumberTokenScalingNumbers(ValueCount: 256, SegmentSize: 64) | 28310.07 +/- 257.15 | 29191.08 +/- 797.09 | 104 |
| NumberTokenScalingNumbers(ValueCount: 1024, SegmentSize: 0) | 56102.22 +/- 1352.75 | 54610.33 +/- 599.53 | 0 |
| NumberTokenScalingNumbers(ValueCount: 1024, SegmentSize: 3) | 195295.72 +/- 2152.47 | 189138.05 +/- 2782.10 | 104 |
| NumberTokenScalingNumbers(ValueCount: 1024, SegmentSize: 64) | 115991.22 +/- 885.87 | 113969.21 +/- 612.91 | 104 |
| ObjectDeserializeSmall_NerdbankJson | 397.92 +/- 4.99 | 401.78 +/- 15.48 | 200 |
| ObjectDeserializeWide_NerdbankJson | 1318.57 +/- 11.41 | 1355.09 +/- 70.48 | 664 |
| ObjectSerializeSmall_NerdbankJson | 198.00 +/- 6.41 | 205.84 +/- 8.79 | 0 |
| ObjectSerializeWide_NerdbankJson | 504.45 +/- 7.51 | 567.43 +/- 53.44 | 0 |
| SegmentedNumericScratchReadDouble(TokenLength: 9) | 188.57 +/- 3.20 | 189.77 +/- 2.81 | 104 |
| SegmentedNumericScratchReadSingle(TokenLength: 9) | 186.35 +/- 2.17 | 199.01 +/- 4.70 | 104 |
| SegmentedNumericScratchReadDecimal(TokenLength: 9) | 175.39 +/- 2.25 | 180.81 +/- 5.08 | 104 |
| SegmentedNumericScratchReadDouble(TokenLength: 129) | 1283.04 +/- 11.49 | 1308.68 +/- 20.84 | 104 |
| SegmentedNumericScratchReadSingle(TokenLength: 129) | 1251.46 +/- 13.89 | 1259.70 +/- 14.03 | 104 |
| SegmentedNumericScratchReadDecimal(TokenLength: 129) | 1167.75 +/- 26.80 | 1234.56 +/- 24.55 | 104 |
| SegmentedTokenNumbers | 6040.68 +/- 60.98 | 6029.06 +/- 93.39 | 104 |
| SegmentedTokenPropertyNames | 6672.83 +/- 550.57 | 6415.40 +/- 84.94 | 768 |
| SegmentedTokenUnescapedString | 3453.06 +/- 31.35 | 3600.76 +/- 91.20 | 1024 |
| SegmentedTokenEscapedString | 8670.20 +/- 129.09 | 9094.73 +/- 128.21 | 1024 |

Reports and disassembly are retained in session `70b1bb69-b0bc-449e-8893-8e6ae0ea5d2d`, under `files\numeric-scratch-baseline-A1` and `files\numeric-scratch-candidate-B1`.
The next credible copy-path hypothesis is avoiding full end-position traversal and a temporary sequence slice when copying a known-length split token, not duplicating the numeric parser or limiting valid token lengths.

### Retained: outline the existing segmented token copy

The simpler fix keeps the original `Slice(...).CopyTo(...)` algorithm and introduces only a non-inlined internal copy boundary on the segmented path.
The contiguous path, numeric helpers, token ownership/pooling, lexical grammar, exceptions, range prevalidation, byte/token-slice cursors, and public APIs are unchanged.
The measured cause is **inlined sequence-copy machinery inflating numeric reader code and local-frame initialization**, not the source-level disposable token syntax.
For `ReadDoubleSegmented`, captured JIT code falls from 1,531 to 666 native bytes and its fixed local frame falls from 0x168 to 0x90 bytes (excluding the separate 128-byte scratch reservation and register saves).
The shared outlined BCL copy helper is 460 bytes in the captured disassembly; it replaces duplicated inlined work rather than adding eleven numeric fallback methods.

Three copy candidates were tested, not merely assumed equivalent:

| Candidate | Local preservation | Numeric control, mean +/- error ns (affinity 2) | Generated Double helper | Disposition |
|---|---|---:|---|---|
| Original BCL slice/copy, inlined | `1c5776b` | 6040.68 +/- 60.98 | 1,531 B; 0x168 frame | Baseline A1 |
| Direct block loop, inlined | `perf/segment-direct-copy` at `2913b9c` | 6310.11 +/- 320.88 | 1,868 B; 0x138 frame | Rejected: no convincing gain |
| Direct block loop, outlined | `perf/segment-copy-outlined` at `3ac4fc2` | 4817.15 +/- 182.59 | 666 B; 0x90 frame | Promising, superseded by simpler BCL implementation |
| Original BCL slice/copy, outlined | Retained implementation | 5591.39 +/- 246.31 | 666 B; 0x90 frame | Repeat on a separately labeled core below |

The original-core repeat became substantially unstable: original A2 numeric control was 6743.36 +/- 156.52 ns, while outlined B4 was 7402.18 +/- 916.22 ns (median 7968.74 ns), reversing the first comparison.
Unchanged small/wide reads also rose from 427/1414 to 595/1842 ns and writes from 203/568 to 230/602 ns in that pair.
These results are retained, not normalized against writes or discarded to manufacture a win.
All 23 cells, medians, distributions and disassembly are preserved under `numeric-scratch-baseline-A1`, `segment-copy-candidate-B1`, `segment-copy-outlined-B2`, `segment-copy-simple-B3`, `segment-copy-baseline-A2`, and `segment-copy-simple-confirm-B4` in the session artifacts.

A separate matched Medium A/B/A/B sequence therefore uses affinity **16**, after observing host activity, with the same .NET 9 runtime, two launches, ten warmups, fifteen measurements, production-default JIT and user power plan.
These numbers must not be compared directly with affinity-2 historical results.
All times below are ns; errors are 99.9% confidence-interval half-widths.

| Operation | Original A3 mean +/- error | Outlined B5 mean +/- error | Original A4 mean +/- error | Outlined B6 mean +/- error | B/op, all |
|---|---:|---:|---:|---:|---:|
| ObjectDeserializeSmall_NerdbankJson | 459.17 +/- 3.39 | 578.52 +/- 107.45 | 509.30 +/- 18.79 | 504.05 +/- 10.52 | 200 |
| ObjectDeserializeWide_NerdbankJson | 1530.11 +/- 64.96 | 1584.38 +/- 51.99 | 1612.55 +/- 99.44 | 1543.57 +/- 36.64 | 664 |
| ObjectSerializeSmall_NerdbankJson | 224.76 +/- 4.86 | 237.03 +/- 9.15 | 250.61 +/- 11.34 | 221.09 +/- 5.24 | 0 |
| ObjectSerializeWide_NerdbankJson | 611.84 +/- 9.74 | 626.87 +/- 18.72 | 606.93 +/- 24.73 | 573.83 +/- 19.67 | 0 |
| SegmentedNumericScratchReadDouble(TokenLength: 9) | 226.16 +/- 8.81 | 198.97 +/- 10.68 | 226.64 +/- 11.39 | 191.72 +/- 9.04 | 104 |
| SegmentedNumericScratchReadSingle(TokenLength: 9) | 242.85 +/- 5.89 | 208.25 +/- 6.48 | 227.84 +/- 8.55 | 185.31 +/- 5.62 | 104 |
| SegmentedNumericScratchReadDecimal(TokenLength: 9) | 213.50 +/- 8.18 | 187.29 +/- 8.81 | 228.85 +/- 8.01 | 170.17 +/- 4.54 | 104 |
| SegmentedNumericScratchReadDouble(TokenLength: 129) | 1500.98 +/- 40.05 | 1680.14 +/- 93.83 | 1642.96 +/- 86.96 | 1409.08 +/- 26.86 | 104 |
| SegmentedNumericScratchReadSingle(TokenLength: 129) | 1550.28 +/- 38.80 | 1569.54 +/- 50.76 | 1548.60 +/- 59.36 | 1393.56 +/- 45.54 | 104 |
| SegmentedNumericScratchReadDecimal(TokenLength: 129) | 1495.45 +/- 60.45 | 1447.26 +/- 78.91 | 1490.83 +/- 149.83 | 1321.61 +/- 48.62 | 104 |
| SegmentedTokenNumbers | 7499.51 +/- 176.19 | 6221.74 +/- 120.79 | 7236.81 +/- 379.75 | 6143.11 +/- 770.60 | 104 |
| SegmentedTokenPropertyNames | 9635.70 +/- 1834.97 | 7167.16 +/- 166.62 | 7292.45 +/- 473.19 | 6884.15 +/- 276.57 | 768 |
| SegmentedTokenUnescapedString | 4358.35 +/- 148.23 | 4307.52 +/- 98.37 | 4145.14 +/- 182.59 | 3915.35 +/- 178.39 | 1024 |
| SegmentedTokenEscapedString | 10699.16 +/- 405.20 | 10721.88 +/- 283.36 | 10202.81 +/- 473.31 | 9582.49 +/- 96.73 | 1024 |

The nine-byte Single/Double/Decimal cases improve in both pairs, and the numeric-array control improves about 17%/15%, with the last candidate array distribution still noisy.
Long pooled spellings do not show a consistent penalty or benefit across repeats; the first long-Double candidate is slower and the repeat faster, so no long-token speed claim is made.
Property-name timings and unchanged object/write controls also drift; this is a targeted short split-token improvement, not complete original serializer-speed restoration.
The full 5,265-test managed matrix passes, including 176 direct input-contract cases that passed against the original implementation before timing.
New copy coverage checks all small ranges, empty/clipped/non-array-backed blocks, >2 GiB logical offsets, borrowed mutation, peeks/copies/restores, original exception types/parameter names and no writes before invalid-range failure.

Separate paired warm affinity-2 traces corroborate the code boundary: `ReadDoubleSegmented` exclusive sampled share moves from 20.47% to 8.19%, while the now-separate copy helper appears at 11.91%.
This is a redistribution of attribution, not a claim of a 60% total CPU reduction; elapsed-time evidence is the unprofiled table above.
Byte-cursor access remains a material lead (23.18% original / 26.56% outlined exclusive share) after setup work shrinks; these relative shares do not establish its absolute CPU cost.
Reports are retained in session `70b1bb69-b0bc-449e-8893-8e6ae0ea5d2d` under `files\copy-quiet-baseline-A3`, `files\copy-quiet-simple-B5`, `files\copy-quiet-baseline-A4`, and `files\copy-quiet-simple-confirm-B6`; paired traces are `files\numbers-copy-original.nettrace` and `files\numbers-copy-outlined.nettrace` with text summaries.

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
