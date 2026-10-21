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

String validation must remain strict for control bytes, malformed UTF-8, and unpaired surrogate escapes, including segmented input.
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
Older targets retain scalar validation, and no target relaxes malformed UTF-8, control-byte, or surrogate checks.

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

## Final Linux Medium comparison

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
