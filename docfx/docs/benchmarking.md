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
