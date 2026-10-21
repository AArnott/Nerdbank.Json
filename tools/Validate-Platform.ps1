#!/usr/bin/env pwsh
#requires -Version 7.2

<#
.SYNOPSIS
    Collects Release managed, NativeAOT, and optional performance validation in a new artifact directory.
.PARAMETER ArtifactDirectory
    A new directory. Existing paths are rejected to preserve previous evidence.
.PARAMETER TestFilter
    MTP tree-node filter for managed and native tests. The default runs all tests.
.PARAMETER RunBenchmarks
    Runs BenchmarkDotNet serially with the Medium job in Release.
.PARAMETER BenchmarkFilter
    BenchmarkDotNet filters, used only when benchmarks are requested.
.PARAMETER CollectCpuProfile
    Also runs a separate, instrumented in-process Medium job under dotnet-trace.
.PARAMETER TraceCommand
    An already-installed dotnet-trace executable (or its full path). No tool is installed.
.PARAMETER TraceProfile
    Explicit comma-separated EventPipe profiles supported by the installed trace tool.
#>
[CmdletBinding()]
param(
    [string]$ArtifactDirectory,
    [ValidateNotNullOrEmpty()]
    [string]$TestFilter = '/*/*/*/*',
    [switch]$RunBenchmarks,
    [ValidateNotNullOrEmpty()]
    [string[]]$BenchmarkFilter = @('*ObjectBenchmarks*', '*SerializationWorkloadBenchmarks*'),
    [switch]$CollectCpuProfile,
    [ValidateNotNullOrEmpty()]
    [string]$TraceCommand = 'dotnet-trace',
    [ValidateNotNullOrEmpty()]
    [string]$TraceProfile = 'dotnet-common,dotnet-sampled-thread-time'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repo = Split-Path $PSScriptRoot -Parent
$pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
if (!$ArtifactDirectory) {
    $ArtifactDirectory = Join-Path $repo (Join-Path 'obj' (Join-Path 'platform-validation' ([guid]::NewGuid().ToString('N'))))
}
$ArtifactDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)
if (Test-Path -LiteralPath $ArtifactDirectory) {
    throw "Artifact directory already exists; choose a new path: $ArtifactDirectory"
}
$null = New-Item -ItemType Directory -Path $ArtifactDirectory
$steps = [Collections.Generic.List[object]]::new()
$testReports = [Collections.Generic.List[object]]::new()
$budgets = @{ 'linux-x64' = 5.88; 'osx-arm64' = 5.40; 'win-x64' = 5.79 }
$report = [ordered]@{
    SchemaVersion = 1
    StartedUtc = [DateTime]::UtcNow.ToString('o')
    FinishedUtc = $null
    Status = 'RUNNING'
    ValidationSucceeded = $false
    Repository = $repo
    ArtifactDirectory = $ArtifactDirectory
    Git = $null
    Host = [ordered]@{
        OS = [Runtime.InteropServices.RuntimeInformation]::OSDescription
        OSArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        PowerShellArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        PowerShellExecutable = $pwsh
        DotnetExecutable = $null
        TraceExecutable = $null
        DotnetArchitecture = $null
        DotnetHostRID = $null
        TargetRID = $null
        SDK = $null
        RequiredSDK = $null
        InstalledSDKs = @()
        InstalledRuntimes = @()
    }
    Options = [ordered]@{
        Configuration = 'Release'
        TestFilter = $TestFilter
        TestScope = $(if ($TestFilter -eq '/*/*/*/*') { 'ALL' } else { 'TARGETED' })
        RunBenchmarks = [bool]$RunBenchmarks
        BenchmarkJob = 'Medium'
        BenchmarkFilter = $BenchmarkFilter
        CollectCpuProfile = [bool]$CollectCpuProfile
        TraceCommand = $TraceCommand
        TraceProfile = $TraceProfile
    }
    BroadSizeBudgetsMiB = $budgets
    AllowedVarianceMiB = 0.2
    BroadSizeGuard = 'NOT_RUN'
    MinimalSizeGuard = 'NOT_CALIBRATED'
    ExpectedNativeSkips = 'Two Flat_NonZeroLowerBounds_AreNormalized data cases per native core framework: NativeAOT cannot create non-zero-lower-bound input arrays.'
    Stages = [ordered]@{
        Prerequisites = 'NOT_RUN'
        ReleaseBuild = 'NOT_RUN'
        ManagedTests = 'NOT_RUN'
        FSharpSelfCheck = 'NOT_RUN'
        NativePublish = 'NOT_RUN'
        NativeTests = 'NOT_RUN'
        NativeFixtures = 'NOT_RUN'
        Benchmarks = $(if ($RunBenchmarks) { 'NOT_RUN' } else { 'NOT_REQUESTED' })
        CpuProfile = $(if ($CollectCpuProfile) { 'NOT_RUN' } else { 'NOT_REQUESTED' })
    }
    Steps = $steps
    TestReports = $testReports
    ImageReports = @()
    Errors = [Collections.Generic.List[string]]::new()
    Artifacts = @()
}

function Invoke-Recorded {
    param([string]$Name, [string]$File, [string[]]$Arguments = @())
    $log = '{0:D3}-{1}.log' -f ($steps.Count + 1), $Name
    $step = [ordered]@{
        Name = $Name
        File = $File
        Arguments = $Arguments
        WorkingDirectory = (Get-Location).Path
        Command = '& ' + ((@($File) + @($Arguments) | ForEach-Object { "'" + $_.Replace("'", "''") + "'" }) -join ' ')
        StartedUtc = [DateTime]::UtcNow.ToString('o')
        DurationSeconds = 0
        ExitCode = $null
        Status = 'RUNNING'
        Log = $log
    }
    $steps.Add($step)
    Write-Host "[$Name] $($step.Command)"
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $output = @()
    try {
        $null = New-Item -ItemType File -Path (Join-Path $ArtifactDirectory $log)
        $output = @(& $File @Arguments 2>&1 | Tee-Object -FilePath (Join-Path $ArtifactDirectory $log) -Append)
        $step.ExitCode = $LASTEXITCODE
        $step.Status = if ($LASTEXITCODE -eq 0) { 'PASS' } else { 'FAIL' }
    } catch {
        $_ | Out-String | Add-Content -LiteralPath (Join-Path $ArtifactDirectory $log)
        $step.Status = 'FAIL'
        $step.ExitCode = -1
    } finally {
        $step.DurationSeconds = $clock.Elapsed.TotalSeconds
    }
    [pscustomobject]@{ Step = $step; Text = ($output | Out-String); Succeeded = $step.Status -eq 'PASS' }
}

function Assert-Command {
    param($Result)
    if (!$Result.Succeeded) {
        throw "Command failed: $($Result.Step.Name). See $($Result.Step.Log)."
    }
}

function Invoke-DotnetWithAssets {
    param([string]$Name, [string[]]$Arguments, [string]$RestoreTarget)
    $result = Invoke-Recorded $Name $dotnetExecutable $Arguments
    if (!$result.Succeeded -and $result.Text -match 'NETSDK(1004|1005|1047|1064)\b') {
        $restore = Invoke-Recorded "$Name-restore-missing-assets" $dotnetExecutable @('restore', $RestoreTarget, '-p:Configuration=Release', '-m:1')
        if ($restore.Succeeded) {
            $retryArguments = @($Arguments | ForEach-Object {
                if ($_ -like '-bl:*') { $_ -replace '\.binlog$', '-retry.binlog' } else { $_ }
            })
            $result = Invoke-Recorded "$Name-retry" $dotnetExecutable $retryArguments
            if ($result.Succeeded) {
                $steps | Where-Object { $_.Name -eq $Name } | ForEach-Object { $_.Status = 'RECOVERED_MISSING_ASSETS' }
            }
        } else {
            $result = $restore
        }
    }
    $result
}

function Read-TestReport {
    param([string]$Directory, [bool]$Native)
    $files = @(Get-ChildItem -LiteralPath $Directory -Filter '*.trx' -File -Recurse)
    if ($files.Count -eq 0) { throw "Missing test report in $Directory" }
    foreach ($file in $files) {
        $trx = [xml](Get-Content -LiteralPath $file.FullName -Raw)
        $counters = $trx.TestRun.ResultSummary.Counters
        if (!$counters -or [int]$counters.total -le 0 -or [int]$counters.executed -le 0) {
            throw "Test report has no executed tests: $($file.FullName)"
        }
        $skips = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'NotExecuted' } | ForEach-Object {
            [ordered]@{
                Name = $_.testName
                Reason = $_.Output.ErrorInfo.Message
                KnownNativeArrayLimitation = $Native -and $_.testName -like '*Flat_NonZeroLowerBounds_AreNormalized*'
            }
        })
        $testReports.Add([ordered]@{
            Path = [IO.Path]::GetRelativePath($ArtifactDirectory, $file.FullName)
            Native = $Native
            Total = [int]$counters.total
            Executed = [int]$counters.executed
            Passed = [int]$counters.passed
            Failed = [int]$counters.failed
            Skipped = $skips
        })
        if ([int]$counters.failed -ne 0 -or $trx.TestRun.ResultSummary.outcome -notin @('Completed', 'Passed')) {
            throw "Failed or incomplete test report: $($file.FullName)"
        }
    }
}

function Invoke-TestRun {
    param([string]$Name, [string]$File, [string[]]$Arguments, [bool]$Native)
    $directory = Join-Path $ArtifactDirectory $Name
    $null = New-Item -ItemType Directory -Path $directory
    $result = Invoke-Recorded $Name $File ($Arguments + @(
        '--treenode-filter', $TestFilter, '--results-directory', $directory,
        '--report-trx', '--report-trx-filename', "$Name.trx",
        '--diagnostic', '--diagnostic-output-directory', $directory))
    $valid = $result.Succeeded
    try { Read-TestReport $directory $Native } catch {
        $report.Errors.Add($_.Exception.Message)
        $valid = $false
    }
    $valid
}

function Test-BenchmarkReports {
    param([string]$Directory, [string]$ConsoleOutput)
    $planned = [regex]::Match($ConsoleOutput, 'Found\s+(\d+)\s+benchmark\(s\)\s+in\s+total')
    if (!$planned.Success -or [int]$planned.Groups[1].Value -le 0) { return $false }
    if (!(Test-Path -LiteralPath $Directory -PathType Container)) { return $false }
    $files = @(Get-ChildItem -LiteralPath $Directory -Filter '*-report-full*.json' -Recurse -File)
    if ($files.Count -eq 0) { return $false }
    $measured = 0
    foreach ($file in $files) {
        $bdn = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        if (!$bdn.Benchmarks -or @($bdn.Benchmarks | Where-Object { !$_.Statistics -or $_.Statistics.N -le 0 }).Count -gt 0) {
            return $false
        }
        $measured += @($bdn.Benchmarks).Count
    }
    return $measured -eq [int]$planned.Groups[1].Value
}

Push-Location $repo
$savedGitEngine = $env:NBGV_GitEngine
try {
    Assert-Command (Invoke-Recorded 'git-head' 'git' @('rev-parse', 'HEAD'))
    $head = (Get-Content -LiteralPath (Join-Path $ArtifactDirectory $steps[-1].Log) -Raw).Trim()
    $dirty = Invoke-Recorded 'git-dirty' 'git' @('status', '--porcelain=v1', '--untracked-files=all')
    Assert-Command $dirty
    $report.Git = @{ Head = $head; DirtyFileCount = @($dirty.Text -split '\r?\n' | Where-Object { $_ }).Count }
    $prime = Join-Path (Join-Path $repo '.github') 'Prime-ForCopilot.ps1'
    $primeStep = [ordered]@{
        Name = 'prime'; File = $prime; Arguments = @(); WorkingDirectory = $repo; Command = ". '$($prime.Replace("'", "''"))'"
        StartedUtc = [DateTime]::UtcNow.ToString('o'); DurationSeconds = 0; ExitCode = $null; Status = 'RUNNING'; Log = 'prime.log'
    }
    $steps.Add($primeStep)
    $primeClock = [Diagnostics.Stopwatch]::StartNew()
    $null = New-Item -ItemType File -Path (Join-Path $ArtifactDirectory 'prime.log')
    # Dot-source once so the shallow-clone workaround applies to all child dotnet commands.
    try {
        . $prime *>&1 | Tee-Object -FilePath (Join-Path $ArtifactDirectory 'prime.log') -Append | Out-Host
        $primeStep.ExitCode = $LASTEXITCODE
        $primeStep.Status = if ($LASTEXITCODE -eq 0) { 'PASS' } else { 'FAIL' }
    } catch {
        $primeStep.Status = 'FAIL'
        throw
    } finally {
        $primeStep.DurationSeconds = $primeClock.Elapsed.TotalSeconds
    }
    if ($primeStep.Status -ne 'PASS') { throw 'Prime-ForCopilot failed; no dotnet command was run.' }
    if ($CollectCpuProfile -and !$RunBenchmarks) { throw '-CollectCpuProfile requires -RunBenchmarks.' }
    if ($IsWindows -and $RunBenchmarks) {
        throw 'Windows benchmark opt-in is blocked: BenchmarkDotNet 0.15.8 defaults to changing the system power plan and exposes no CLI override. Run managed/native validation without benchmarks, or run benchmarks on macOS/Linux.'
    }
    $globalJson = Get-Content -LiteralPath (Join-Path $repo 'global.json') -Raw | ConvertFrom-Json
    $report.Host.RequiredSDK = $globalJson.sdk.version
    $dotnet = Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1
    $dotnetExecutable = $dotnet.Source
    $report.Host.DotnetExecutable = $dotnetExecutable
    $info = Invoke-Recorded 'dotnet-info' $dotnetExecutable @('--info')
    Assert-Command $info
    $sdks = Invoke-Recorded 'dotnet-sdks' $dotnetExecutable @('--list-sdks')
    Assert-Command $sdks
    $runtimes = Invoke-Recorded 'dotnet-runtimes' $dotnetExecutable @('--list-runtimes')
    Assert-Command $runtimes
    $report.Host.InstalledSDKs = @($sdks.Text.Trim() -split '\r?\n')
    $report.Host.InstalledRuntimes = @($runtimes.Text.Trim() -split '\r?\n')
    $sdk = Invoke-Recorded 'dotnet-selected-sdk' $dotnetExecutable @('--version')
    Assert-Command $sdk
    $report.Host.SDK = $sdk.Text.Trim()
    if ($report.Host.SDK -ne $globalJson.sdk.version) {
        throw "This handoff requires exact SDK $($globalJson.sdk.version); selected $($report.Host.SDK). No SDK will be installed."
    }
    foreach ($runtime in @('Microsoft.NETCore.App', 'Microsoft.AspNetCore.App')) {
        foreach ($major in @(8, 9, 10)) {
            if ($runtimes.Text -notmatch "(?m)^$([regex]::Escape($runtime)) $major\.0\.\d+ ") {
                throw "Missing $runtime $major.0 runtime for managed test/sample TFMs."
            }
        }
    }
    $properties = Invoke-Recorded 'target-platform' $dotnetExecutable @('msbuild', (Join-Path (Join-Path 'test' 'AotSizeTest') 'AotSizeTest.csproj'), '-nologo', '-getProperty:DefaultRuntimeIdentifier,NETCoreSdkRuntimeIdentifier,NETCoreSdkPortableRuntimeIdentifier')
    Assert-Command $properties
    $evaluated = ($properties.Text | ConvertFrom-Json).Properties
    $rid = $evaluated.DefaultRuntimeIdentifier
    $report.Host.TargetRID = $rid
    $report.Host.DotnetHostRID = $evaluated.NETCoreSdkRuntimeIdentifier
    $report.Host.DotnetArchitecture = ($evaluated.NETCoreSdkPortableRuntimeIdentifier -split '-')[-1]
    if (!$rid -or $rid -notmatch '^(win|linux|osx)-(x64|arm64)$') { throw "Unsupported target RID: $rid" }
    if ($report.Host.DotnetArchitecture -ne ($rid -split '-')[-1] -or
        $report.Host.PowerShellArchitecture.ToLowerInvariant() -ne ($rid -split '-')[-1]) {
        throw 'Architecture mismatch (possibly Rosetta). Use native-architecture PowerShell and dotnet; do not mix artifacts.'
    }
    if ($IsMacOS) {
        Assert-Command (Invoke-Recorded 'mac-hardware-arm64' 'sysctl' @('-n', 'hw.optional.arm64'))
        $appleSilicon = (Get-Content -LiteralPath (Join-Path $ArtifactDirectory $steps[-1].Log) -Raw).Trim() -eq '1'
        if ($appleSilicon -and $rid -ne 'osx-arm64') { throw 'Apple Silicon detected with a translated x64 target. Rerun natively, not under Rosetta.' }
        Assert-Command (Invoke-Recorded 'native-clang' 'xcrun' @('--find', 'clang'))
        Assert-Command (Invoke-Recorded 'native-sdk' 'xcrun' @('--show-sdk-path'))
        Assert-Command (Invoke-Recorded 'native-compiler-version' 'clang' @('--version'))
    } elseif ($IsLinux) {
        Assert-Command (Invoke-Recorded 'native-compiler-version' 'clang' @('--version'))
        Assert-Command (Invoke-Recorded 'native-linker-version' 'ld' @('--version'))
        Assert-Command (Invoke-Recorded 'native-zlib' 'pkg-config' @('--modversion', 'zlib'))
        Assert-Command (Invoke-Recorded 'linux-kernel' 'uname' @('-a'))
    } elseif ($IsWindows) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} (Join-Path 'Microsoft Visual Studio' (Join-Path 'Installer' 'vswhere.exe'))
        $compiler = Invoke-Recorded 'native-msvc' $vswhere @('-latest', '-products', '*', '-requires', 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64', '-find', 'VC\Tools\MSVC\**\bin\Hostx64\x64\cl.exe')
        Assert-Command $compiler
        if (!$compiler.Text.Trim()) { throw 'Visual Studio C++ x64 build tools are missing. NativeAOT also requires the Windows SDK.' }
    }
    if ($CollectCpuProfile) {
        $TraceCommand = (Get-Command $TraceCommand -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
        $report.Host.TraceExecutable = $TraceCommand
        Assert-Command (Invoke-Recorded 'trace-version' $TraceCommand @('--version'))
        $profiles = Invoke-Recorded 'trace-profiles' $TraceCommand @('list-profiles')
        Assert-Command $profiles
        foreach ($profile in ($TraceProfile -split ',')) {
            if (!$profile -or $profiles.Text -notmatch "(?m)^\s*$([regex]::Escape($profile))(?:\s|$)") {
                throw "Installed dotnet-trace does not advertise requested profile '$profile'. Select explicit profiles supported by this tool; no fallback is applied."
            }
        }
    }
    $report.Stages.Prerequisites = 'PASS'

    $solution = Join-Path $repo 'Nerdbank.Json.slnx'
    $build = Invoke-DotnetWithAssets 'release-build' @('build', $solution, '-c', 'Release', '--no-restore', '-m:1', '-warnAsError', '-warnNotAsError:NU1901,NU1902,NU1903,NU1904', "-bl:$(Join-Path $ArtifactDirectory 'release-build.binlog')") $solution
    $report.Stages.ReleaseBuild = $build.Step.Status
    if ($build.Succeeded) {
        $managedOK = $true
        foreach ($project in @('Nerdbank.Json.Tests', 'Nerdbank.Json.AspNetCoreMvcFormatter.Tests', 'Nerdbank.Json.Analyzers.Tests')) {
            $frameworks = if ($project -eq 'Nerdbank.Json.Analyzers.Tests') { @('net8.0') } else { @('net8.0', 'net9.0', 'net10.0') }
            if ($IsWindows -and $project -eq 'Nerdbank.Json.Tests') { $frameworks += 'net472' }
            foreach ($framework in $frameworks) {
                $projectPath = Join-Path (Join-Path 'test' $project) "$project.csproj"
                $ok = Invoke-TestRun "managed-$project-$framework" $dotnetExecutable @('test', '--project', $projectPath, '--no-build', '--no-restore', '-c', 'Release', '--framework', $framework, '--') $false
                $managedOK = $ok -and $managedOK
            }
        }
        $report.Stages.ManagedTests = if ($managedOK) { 'PASS' } else { 'FAIL' }
        $sample = Invoke-Recorded 'fsharp-self-check' $dotnetExecutable @('run', '--project', (Join-Path (Join-Path 'samples' 'fs') 'fs.fsproj'), '--no-build', '--no-restore', '-c', 'Release')
        $report.Stages.FSharpSelfCheck = $sample.Step.Status
    }

    $traversal = Join-Path 'test' 'dirs.proj'
    $publish = Invoke-DotnetWithAssets 'native-publish' @('publish', $traversal, '--no-restore', '-c', 'Release', '-m:1', '-warnAsError', '-warnNotAsError:NU1901,NU1902,NU1903,NU1904', "-bl:$(Join-Path $ArtifactDirectory 'native-publish.binlog')") $traversal
    $report.Stages.NativePublish = $publish.Step.Status
    if ($publish.Succeeded) {
        $discoveryScript = (Join-Path $PSScriptRoot 'Get-NativeAOTTestProjects.ps1').Replace("'", "''")
        $discovery = Invoke-Recorded 'native-test-discovery' $pwsh @('-NoProfile', '-Command', "& '$discoveryScript' -Configuration Release | ConvertTo-Json -Depth 10 -AsArray")
        Assert-Command $discovery
        $nativeTests = @($discovery.Text | ConvertFrom-Json)
        if ($nativeTests.Count -eq 0 -or !($nativeTests | Where-Object ProjectName -eq 'Nerdbank.Json.Tests')) {
            throw 'Native traversal returned no core tests.'
        }
        $nativeOK = $true
        foreach ($test in $nativeTests) {
            $ok = Invoke-TestRun "native-$($test.ProjectName)-$($test.TargetFramework)" $test.ExecutablePath @() $true
            $nativeOK = $ok -and $nativeOK
        }
        $report.Stages.NativeTests = if ($nativeOK) { 'PASS' } else { 'FAIL' }
        $fixturesOK = $true
        foreach ($fixture in @('AotCompatibilityTest', 'AotRuntimeCompatibilityTest', 'AotNativeConsole', 'AotSizeTest')) {
            $imageDirectory = Join-Path $repo (Join-Path 'bin' (Join-Path $fixture (Join-Path 'Release' (Join-Path 'net10.0' (Join-Path $rid 'publish')))))
            $image = Join-Path $imageDirectory ($fixture + $(if ($IsWindows) { '.exe' } else { '' }))
            if ($fixture -in @('AotNativeConsole', 'AotSizeTest')) {
                $output = Join-Path $ArtifactDirectory "$fixture-size.json"
                $arguments = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'Measure-AotImage.ps1'), '-Path', $image, '-RuntimeIdentifier', $rid, '-OutputPath', $output)
                if ($fixture -eq 'AotNativeConsole' -and $budgets.ContainsKey($rid)) {
                    $arguments += @('-ExpectedSizeMiB', $budgets[$rid].ToString([Globalization.CultureInfo]::InvariantCulture), '-AllowedVarianceMiB', '0.2')
                }
                $measurement = Invoke-Recorded "measure-$fixture" $pwsh $arguments
                $fixturesOK = $measurement.Succeeded -and $fixturesOK
                if (!(Test-Path -LiteralPath $output)) { throw "Missing required image report: $output" }
                $size = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
                $report.ImageReports += $size
                if ($size.Result -ne 'PASS' -or $size.SizeBytes -le 0 -or $size.RuntimeIdentifier -ne $rid) {
                    throw "Invalid image report: $output"
                }
                if ($fixture -eq 'AotNativeConsole') {
                    $report.BroadSizeGuard = $size.SizeRegressionResult
                    if ($budgets.ContainsKey($rid) -and ($size.SizeRegressionResult -ne 'PASS' -or
                        $size.ExpectedSizeMiB -ne $budgets[$rid] -or $size.AllowedVarianceMiB -ne 0.2 -or
                        [math]::Abs($size.SizeBytes / 1MB - $budgets[$rid]) -gt 0.2)) { $fixturesOK = $false }
                } elseif ($size.SizeRegressionResult -ne 'NOT_CALIBRATED') {
                    throw 'Minimal image must remain uncalibrated.'
                }
                if ($fixture -eq 'AotSizeTest') {
                    $contract = Invoke-Recorded 'image-measurement-contract' $pwsh @('-NoProfile', '-File', (Join-Path (Join-Path (Join-Path $repo 'test') 'AotSizeTest') 'Test-ImageMeasurement.ps1'), '-Path', $image, '-RuntimeIdentifier', $rid)
                    $fixturesOK = $contract.Succeeded -and $fixturesOK
                }
            } else {
                $execution = Invoke-Recorded "run-$fixture" $image
                $fixturesOK = $execution.Succeeded -and $fixturesOK
            }
        }
        $report.Stages.NativeFixtures = if ($fixturesOK) { 'PASS' } else { 'FAIL' }
    }

    if ($RunBenchmarks -and $build.Succeeded) {
        $benchmarkDll = Join-Path $repo (Join-Path 'bin' (Join-Path 'Benchmarks' (Join-Path 'Release' (Join-Path 'net9.0' 'Benchmarks.dll'))))
        $benchmarkArtifacts = Join-Path $ArtifactDirectory 'benchmarks-timing'
        $benchmarkArgs = @($benchmarkDll, '--job', 'Medium', '--filter') + $BenchmarkFilter + @('--artifacts', $benchmarkArtifacts, '--exporters', 'json')
        $timing = Invoke-Recorded 'benchmarks-timing' $dotnetExecutable $benchmarkArgs
        $validBenchmarks = $timing.Succeeded -and (Test-BenchmarkReports $benchmarkArtifacts $timing.Text)
        $report.Stages.Benchmarks = if ($validBenchmarks) { 'PASS' } else { 'FAIL' }
        if (!$validBenchmarks) { $report.Errors.Add('Benchmark command failed, reports are missing, or benchmarks have no measurements.') }
        if ($CollectCpuProfile) {
            # In-process execution makes this launch-mode EventPipe trace cover actual workloads, not just the BDN controller.
            $profileArtifacts = Join-Path $ArtifactDirectory 'benchmarks-profile-not-timing'
            $trace = Join-Path $ArtifactDirectory 'cpu-profile.nettrace'
            $profileArgs = @('collect', '--profile', $TraceProfile, '--show-child-io', '--output', $trace, '--', $dotnetExecutable, $benchmarkDll, '--job', 'Medium', '--inProcess', '--filter') +
                $BenchmarkFilter + @('--artifacts', $profileArtifacts, '--exporters', 'json')
            $profile = Invoke-Recorded 'cpu-profile-not-timing' $TraceCommand $profileArgs
            $hasTrace = (Test-Path -LiteralPath $trace) -and (Get-Item -LiteralPath $trace).Length -gt 0
            $validProfile = $profile.Succeeded -and $hasTrace -and (Test-BenchmarkReports $profileArtifacts $profile.Text)
            $report.Stages.CpuProfile = if ($validProfile) { 'PASS' } else { 'FAIL' }
            if (!$validProfile) { $report.Errors.Add('CPU profile command failed, trace is missing/empty, or instrumented benchmark reports lack measurements.') }
        }
    }
} catch {
    $report.Errors.Add($_.Exception.Message)
    $_ | Out-String | Add-Content -LiteralPath (Join-Path $ArtifactDirectory 'failure.log')
} finally {
    # Preserve partial native outputs and compiler reports even when publish or execution failed.
    try {
        foreach ($fixture in @('AotCompatibilityTest', 'AotRuntimeCompatibilityTest', 'AotNativeConsole', 'AotSizeTest')) {
            $source = Join-Path $repo (Join-Path 'bin' (Join-Path $fixture 'Release'))
            if (Test-Path -LiteralPath $source) {
                $destination = Join-Path $ArtifactDirectory (Join-Path 'native-images' $fixture)
                $null = New-Item -ItemType Directory -Path $destination -Force
                Get-ChildItem -LiteralPath $source -Directory -Recurse -Filter 'publish' | ForEach-Object {
                    $target = Join-Path $destination ([IO.Path]::GetRelativePath($source, $_.FullName))
                    $null = New-Item -ItemType Directory -Path $target -Force
                    Copy-Item -Path (Join-Path $_.FullName '*') -Destination $target -Recurse
                }
            }
            $compilerRoot = Join-Path $repo (Join-Path 'obj' (Join-Path 'test' $fixture))
            if (Test-Path -LiteralPath $compilerRoot) {
                Get-ChildItem -LiteralPath $compilerRoot -File -Recurse | Where-Object { $_.Name -like '*.mstat' -or $_.Name -like '*.dgml.xml' } | ForEach-Object {
                    $target = Join-Path $ArtifactDirectory (Join-Path 'native-compiler' ([IO.Path]::GetRelativePath($repo, $_.FullName)))
                    $null = New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force
                    Copy-Item -LiteralPath $_.FullName -Destination $target
                }
            }
        }
    } catch {
        $report.Errors.Add("Artifact collection failed: $($_.Exception.Message)")
    }
    $env:NBGV_GitEngine = $savedGitEngine
    Pop-Location
    foreach ($name in @($report.Stages.Keys)) {
        if ($report.Stages[$name] -eq 'NOT_RUN') { $report.Stages[$name] = 'BLOCKED' }
    }
    $failedStages = @($report.Stages.Values | Where-Object { $_ -in @('FAIL', 'BLOCKED') })
    $report.ValidationSucceeded = $failedStages.Count -eq 0 -and $report.Errors.Count -eq 0
    $report.Status = if ($report.ValidationSucceeded) {
        if ($report.BroadSizeGuard -eq 'NOT_CALIBRATED') { 'COMPLETED_WITH_UNCALIBRATED_BROAD_SIZE' } else { 'PASS' }
    } else { 'FAIL_OR_BLOCKED' }
    $report.FinishedUtc = [DateTime]::UtcNow.ToString('o')
    $report.Artifacts = @(Get-ChildItem -LiteralPath $ArtifactDirectory -File -Recurse | ForEach-Object {
        [ordered]@{ Path = [IO.Path]::GetRelativePath($ArtifactDirectory, $_.FullName); Bytes = $_.Length }
    })
    $report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'validation.json') -Encoding utf8
    Write-Host "$($report.Status): $(Join-Path $ArtifactDirectory 'validation.json')"
}
if (!$report.ValidationSucceeded) { exit 1 }
