# macOS and Linux validation handoff

Use `tools/Validate-Platform.ps1` from this branch to collect platform evidence without editing code or recalibrating baselines.
It runs serial Release builds, core/MVC/analyzer managed tests for every declared framework (including .NET Framework on Windows), the F# round-trip self-check, eligible NativeAOT test variants discovered through the existing traversal, and all four native fixtures.
Benchmarks and CPU traces are opt-in and must wait until other performance measurements have finished.
No macOS or Linux execution is implied by the existence of this script.

## Requirements

Work in a dedicated checkout of the requested branch with its full git history where possible.
Do not reset, rebase, force checkout, or discard local edits.
The report records the checked-out HEAD and dirty-file count so the receiver can identify the measured source.

Check `command -v pwsh`, `command -v dotnet`, and `command -v git` in the macOS/Linux shell.
PowerShell 7.2 or newer is required; if absent, follow the [PowerShell installation instructions](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) for the host rather than attempting to execute `.ps1` files in bash.
Run all subsequent commands in `pwsh` from the repository root.

The handoff requires the exact SDK pinned in `global.json` (currently **10.0.402**), even though the repository permits patch roll-forward.
It also needs **Microsoft.NETCore.App and Microsoft.AspNetCore.App runtimes 8.0, 9.0, and 10.0** for the managed test and sample frameworks.
An installed .NET 10 SDK or runtime alone does not supply the .NET 8/9 runtimes.
The script runs `.github/Prime-ForCopilot.ps1` once before its first dotnet command and records `dotnet --info`, `--list-sdks`, `--list-runtimes`, and the selected SDK.
It stops with explicit failure evidence for missing prerequisites; it does not install them or alter global SDK configuration.

If provisioning is authorized and no measurement is in progress, the existing repository helper can install SDK/runtimes at repository scope:

```powershell
. ./tools/Install-DotNetSdk.ps1 -InstallLocality repo -IncludeAspNetCore
```

Keep using that PowerShell session so its local PATH/environment changes remain effective.
Do not use machine-wide installation, upgrade a global tool, or change `global.json` just to make validation run.
For a missing PowerShell executable, install it through the host's approved mechanism first; the SDK helper itself requires PowerShell.
The validation script attempts `--no-restore` first and only restores/retries after a recognized missing-assets/package error.
It does not automatically restore unrelated tools.

macOS needs the Xcode Command Line Tools, a usable `xcrun`/`clang`, and a macOS SDK (`xcrun --show-sdk-path`).
Linux needs `clang`, a linker (`ld`), and zlib development files discoverable by `pkg-config --modversion zlib`; use the distribution's [NativeAOT prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites) if provisioning is authorized.
If Linux already has a usable GCC C++ toolchain instead of clang, explicitly select its executable with `-CppCompilerAndLinker /usr/bin/g++`.
This Linux-only opt-in resolves an executable file, requires its `--version` command to succeed, and records its exact path and the passed `-p:CppCompilerAndLinker=...` MSBuild property.
The property is invocation-only for native publishing (including a missing-assets restore/retry) and platform/native-test discovery evaluations; it does not change project files, global configuration, PATH, or managed build/test settings.
Without this option, the Linux clang prerequisite is unchanged; no automatic compiler fallback or installation is attempted, and macOS/Windows compiler overrides are rejected.
GCC selection still requires the linker and zlib development prerequisites and does not establish equivalence of compiler output sizes; keep the existing size budgets unchanged.
Windows needs Visual Studio C++ x64 build tools, the Windows SDK, and .NET Framework 4.7.2 or newer for the Windows-only managed target.
Missing compiler/linker dependencies remain failures, not native-test skips.

On Apple Silicon, use native **arm64** PowerShell and dotnet, not a Rosetta terminal or an x64 SDK.
The script audits OS/PowerShell/SDK architectures, evaluated RID, and `hw.optional.arm64`, rejecting translated or mixed-architecture runs.
An Intel Mac uses `osx-x64`, whose broad size budget is **not calibrated**; never substitute the arm64 budget.
WSL is a separate Linux environment and needs its own SDK, runtimes, PowerShell, and native prerequisites; Windows installations do not satisfy these requirements.
The initial WSL Ubuntu 26.04 handoff environment lacked the pinned SDK and .NET 8/9 runtimes; subsequently authorized provisioning installed SDK 10.0.402 and .NET/ASP.NET Core 8.0.32, 9.0.21, and 10.0.13 only in the isolated checkout's `obj/tools/.dotnet`.
A minimal NativeAOT publish and JSON round-trip succeeded there with explicit `-p:CppCompilerAndLinker=/usr/bin/g++`; clang remains absent, and the initial full validation attempt failed at that prerequisite as expected.
Neither that probe nor these script checks establish a full Linux validation pass; the coordinator owns the updated full run, and macOS validation remains pending.
Do not provision, build, or run benchmarks while competing measurements are running.

## Run and collect

Choose a **new, nonexistent** output directory for each run.
The script refuses an existing directory, never clears previous results, and does not upload anything.
Relative artifact paths resolve against the caller's PowerShell filesystem location, even when the process working directory differs.
Non-filesystem provider paths are rejected.

```powershell
pwsh -NoProfile -File ./tools/Validate-Platform.ps1 -ArtifactDirectory ./obj/platform-macos
```

On Linux use the same command with `./obj/platform-linux` as the artifact directory.
For an explicitly selected Linux GCC toolchain, run `./tools/Validate-Platform.ps1 -ArtifactDirectory ./obj/platform-linux-gcc -CppCompilerAndLinker /usr/bin/g++`.
The default runs all relevant tests; for a targeted investigation, add `-TestFilter '/*/*/JsonObjectSerializerTests/*'` (MTP tree-node syntax, not VSTest `--filter`).
The JSON labels targeted scope explicitly; a targeted run is not a substitute for the default full handoff.
Native core array tests have two expected skipped data cases per framework for `Flat_NonZeroLowerBounds_AreNormalized`, because NativeAOT cannot construct non-zero-lower-bound input arrays.
Their names and reasons are preserved in the TRX files and classified in the JSON; do not weaken or remove those tests.

`validation.json` contains source/host identity, options, stage statuses, exact executable/argument arrays and working directories, exit codes, durations, test counters/skips, size reports, errors, and an artifact inventory.
Logs, build binlogs, TRX/diagnostic files, native fixture publish directories, and available compiler `.mstat`/`.dgml.xml` files are retained under the same directory, including partial results on failure.
Previously existing native outputs may be collected after a failure, but they are not executed or accepted as fresh publish success.
Missing test reports, zero executed tests, incomplete results, missing image reports, failed commands, or blocked stages produce a nonzero exit.
Inspect both the exit code and JSON status; never infer success from an artifact folder merely existing.

The broad `AotNativeConsole` guard preserves **linux-x64 5.88 MiB**, **osx-arm64 5.40 MiB**, and **win-x64 5.79 MiB**, each **+/- 0.2 MiB**.
Other supported RIDs are reported as `NOT_CALIBRATED`, not as a passed size guard.
The separate minimal `AotSizeTest` remains uncalibrated on every RID and cannot replace the broad guard.
`AotCompatibilityTest` roots the shipping assemblies, while `AotRuntimeCompatibilityTest` checks native runtime behavior; both published executables are also run.
Do not automatically update size baselines from a measured result.

Only after all competing measurements stop, request serial BenchmarkDotNet **Medium** timing runs:

```powershell
./tools/Validate-Platform.ps1 -ArtifactDirectory ./obj/platform-medium -RunBenchmarks -BenchmarkFilter '*ObjectBenchmarks*','*SerializationWorkloadBenchmarks*'
```

The default benchmark filters are those same two groups; provide an explicit array for other workloads.
Timing uses the Release net9.0 benchmark executable, outside a debugger, with BenchmarkDotNet's normal out-of-process toolchain.
BenchmarkDotNet may restore its generated benchmark project as part of that toolchain.
The current benchmark classes use one runtime/toolchain and one Medium job, yielding one build partition; the script never launches concurrent benchmark commands.
Keep the full reports, not just console means; compare only like-for-like OS, RID, SDK/runtime, filters, job, and workload, noting shared-machine load and thermal conditions.
Do not reduce the job to Short/Dry or change power settings, stop other users' applications, or run builds/benchmarks concurrently.
Windows managed/native validation is supported, but benchmark opt-in is explicitly blocked there: the pinned BenchmarkDotNet 0.15.8 defaults to switching the system power plan and has no CLI override.
This handoff does not bypass that restriction or change the benchmark project; use macOS/Linux for its optional performance runs.

For optional managed CPU hotspot profiling, first check an **already-installed** `dotnet-trace` with `--version` and `list-profiles`.
The default explicit profiles are `dotnet-common,dotnet-sampled-thread-time`; if the installed version lacks them, report the block or explicitly select supported EventPipe profiles with `-TraceProfile`, never silently substitute.
Older tools may advertise `cpu-sampling`, but that name was removed from modern cross-platform `collect` and must not be confused with privileged Linux CPU sampling.
If absent, report the block or arrange an explicitly authorized local tool installation separately, never a global installation from this script.

```powershell
./tools/Validate-Platform.ps1 -ArtifactDirectory ./obj/platform-profile -RunBenchmarks -CollectCpuProfile -TraceCommand dotnet-trace
```

This first collects ordinary timing in `benchmarks-timing`, then launches a **separate in-process Medium** workload under EventPipe into `cpu-profile.nettrace` and `benchmarks-profile-not-timing`.
The trace tool version, available profiles, selected profile, and exact launch command are recorded.
In-process execution ensures the trace samples the workloads rather than only the BenchmarkDotNet controller.
Instrumented/in-process numbers are not comparable timing results; use only the unprofiled out-of-process reports for throughput/allocations.
These EventPipe samples attribute managed thread activity, not precise kernel on-CPU time; they do not measure aggregate CPU consumption.
No privileged ETW session is requested.

After the script exits, retain the complete run directory even on failure.
Package it locally, using a new archive path, and send it back manually:

```powershell
tar -czf ./obj/platform-macos-results.tar.gz -C ./obj platform-macos
```

Use the corresponding run/archive names on Linux or for optional performance runs.
Check the archive contains `validation.json`, logs, and requested reports/traces before sharing; review logs/binlogs for private paths or environment information and use the agreed private channel.
No upload is performed by the script.

## Lightweight script checks

Run `./tools/Test-ValidatePlatform.ps1` to parse the script and exercise artifact location/provider safety, existing-evidence preservation, compiler defaults/guards, and publish/restore/retry/discovery property forwarding with mocked external commands.
These checks use a temporary isolated fixture, intentionally make PowerShell and process working directories differ, and do not run dotnet, native compilers, builds, tests, or benchmarks.
They do not replace real platform validation.

## Standalone Copilot prompt for the Mac

> In the dedicated checkout of the supplied branch, read `CONTRIBUTING.md` and `docfx/docs/platform-validation.md`, then run `pwsh -NoProfile -File ./tools/Validate-Platform.ps1 -ArtifactDirectory ./obj/platform-macos`.
> Verify the supplied branch/HEAD without changing branches or discarding edits, check native Apple Silicon versus Rosetta architecture, and honor the exact SDK 10.0.402 plus .NET/ASP.NET Core 8/9/10 runtime requirements.
> Do not modify code or size baselines, install global tools/SDKs, change power settings, stop other users' apps, or upload anything.
> If prerequisites are missing, preserve the failure JSON/logs and report the precise block; request permission before provisioning repository-local prerequisites.
> Do not run benchmarks until the coordinator confirms other measurements have stopped; when explicitly requested use `./tools/Validate-Platform.ps1 -ArtifactDirectory ./obj/platform-macos-medium -RunBenchmarks`, adding `-CollectCpuProfile` only if CPU profiling is requested and dotnet-trace is already available.
> Inspect the exit code, `validation.json`, test TRX files, broad/minimal size reports, and requested BenchmarkDotNet reports; missing reports are not success.
> Validate the artifact inventory, create a new local tar archive of the entire run directory as described in the guide, and return the HEAD, OS/architectures/RID, stage results, exact blocks or failures, and archive location.
> Send the JSON plus complete logs/reports and CPU profiles if requested through the agreed private channel; retain all failure evidence and do not automatically upload.
