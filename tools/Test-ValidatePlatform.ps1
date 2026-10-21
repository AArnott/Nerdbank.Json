#!/usr/bin/env pwsh
#requires -Version 7.2

[CmdletBinding()]
param(
    [switch]$ManagedSmoke,
    [string]$SmokeArtifactDirectory
)

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'Validate-Platform.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
$source = Get-Content -LiteralPath $scriptPath -Raw
$mock = @'
function Get-Command {
    param($Name, $CommandType, $ErrorAction)
    if ($Name -eq 'dotnet') { return [pscustomobject]@{ Source = $compiler } }
    Microsoft.PowerShell.Core\Get-Command $Name -CommandType Application -ErrorAction Stop
}

function Invoke-Recorded {
    param([string]$Name, [string]$File, [string[]]$Arguments = @())
    $text = ''
    $succeeded = $true
    switch ($Name) {
        'git-head' { $text = 'mock-head' }
        'dotnet-info' { $text = 'mock dotnet' }
        'dotnet-sdks' { $text = "$($globalJson.sdk.version) [mock]" }
        'dotnet-runtimes' {
            $text = (@('Microsoft.NETCore.App', 'Microsoft.AspNetCore.App') | ForEach-Object {
                $runtime = $_
                8, 9, 10 | ForEach-Object { "$runtime $_.0.1 [mock]" }
            }) -join "`n"
        }
        'dotnet-selected-sdk' { $text = $globalJson.sdk.version }
        'target-platform' {
            $architecture = $report.Host.PowerShellArchitecture.ToLowerInvariant()
            $text = @{ Properties = @{
                DefaultRuntimeIdentifier = "linux-$architecture"
                NETCoreSdkRuntimeIdentifier = "linux-$architecture"
                NETCoreSdkPortableRuntimeIdentifier = "linux-$architecture"
            } } | ConvertTo-Json
        }
        'native-compiler-version' { $succeeded = !$failCompilerVersion }
        'native-msvc' { $text = 'mock-cl.exe' }
        'release-build' { $succeeded = $runManaged }
        { $_ -like 'managed-*' } {
            $directory = $Arguments[[array]::IndexOf($Arguments, '--results-directory') + 1]
            $filename = $Arguments[[array]::IndexOf($Arguments, '--report-trx-filename') + 1]
            $executed = if ($managedReportMode -eq 'zero') { 0 } else { 1 }
            $failed = if ($managedReportMode -eq 'failed') { 1 } else { 0 }
            $outcome = if ($managedReportMode -eq 'incomplete') { 'Aborted' } else { 'Completed' }
            $succeeded = $managedReportMode -ne 'exit'
            if ($managedReportMode -ne 'missing') {
                Set-Content -LiteralPath (Join-Path $directory $filename) -Value @"
<TestRun><Results><UnitTestResult testName="Flat_NonZeroLowerBounds_AreNormalized" outcome="NotExecuted"><Output><ErrorInfo><Message>mock skip</Message></ErrorInfo></Output></UnitTestResult></Results><ResultSummary outcome="$outcome"><Counters total="2" executed="$executed" passed="1" failed="$failed" /></ResultSummary></TestRun>
"@
            }
        }
        'native-publish' { $succeeded = $false; $text = 'NETSDK1004: mocked missing assets' }
        'native-test-discovery' {
            $text = @{ TargetResults = @{ GetNativeAOTTests = @{ Items = @() } } } | ConvertTo-Json -Depth 5
        }
    }
    $step = [ordered]@{
        Name = $Name; File = $File; Arguments = $Arguments; Log = "$Name.log"
        Status = $(if ($succeeded) { 'PASS' } else { 'FAIL' })
        ExitCode = $(if ($succeeded) { 0 } else { 1 })
    }
    $steps.Add($step)
    Set-Content -LiteralPath (Join-Path $ArtifactDirectory $step.Log) -Value $text
    [pscustomobject]@{ Step = $step; Text = $text; Succeeded = $succeeded }
}
'@

$root = Join-Path ([IO.Path]::GetTempPath()) ("validate-platform-tests-" + [guid]::NewGuid().ToString('N'))
$savedProcessDirectory = [Environment]::CurrentDirectory
$savedLocation = Get-Location
try {
    $fixture = Join-Path $root 'fixture'
    $psDirectory = Join-Path $root 'powershell-location'
    $processDirectory = Join-Path $root 'process-location'
    $null = New-Item -ItemType Directory -Path $fixture, $psDirectory, $processDirectory
    $null = New-Item -ItemType Directory -Path (Join-Path $fixture '.github')
    Copy-Item -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'global.json') -Destination $fixture
    Set-Content -LiteralPath (Join-Path $fixture '.github\Prime-ForCopilot.ps1') -Value '$global:LASTEXITCODE = 0'

    # Replace only command execution, the repository fixture, and the OS probe; run the real orchestration.
    $replacements = @(
        @{ Node = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-Recorded' }, $true); Text = $mock },
        @{ Node = $ast.Find({ param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -eq '$repo' }, $true)
            Text = '$repo = ''' + $fixture.Replace("'", "''") + '''; $TestIsLinux = $simulateLinux; $TestIsWindows = $simulateWindows; $TestIsMacOS = $false' },
        @{ Node = $ast.Find({ param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -eq '$vswhere' }, $true)
            Text = '$vswhere = Join-Path $repo ''vswhere.exe''' }
    )
    foreach ($replacement in ($replacements | Sort-Object { $_.Node.Extent.StartOffset } -Descending)) {
        $extent = $replacement.Node.Extent
        $source = $source.Remove($extent.StartOffset, $extent.EndOffset - $extent.StartOffset).Insert($extent.StartOffset, $replacement.Text)
    }
    $source = $source -replace '\$IsLinux\b', '$TestIsLinux'
    $source = $source -replace '\$IsMacOS\b', '$TestIsMacOS'
    $source = $source -replace '\$IsWindows\b', '$TestIsWindows'
    $caseScript = Join-Path $fixture 'Validate-Platform-Mocked.ps1'
    Set-Content -LiteralPath $caseScript -Value $source

    Set-Location $psDirectory
    [Environment]::CurrentDirectory = $processDirectory
    $simulateLinux = $true
    $simulateWindows = $false
    $runManaged = $false
    $failCompilerVersion = $false
    $compiler = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
    & $caseScript -ArtifactDirectory '.\nested\override' -CppCompilerAndLinker $compiler
    if ($LASTEXITCODE -ne 1) { throw 'Mocked empty discovery must produce a nonzero exit.' }
    $artifact = Join-Path $psDirectory 'nested\override'
    $report = Get-Content -LiteralPath (Join-Path $artifact 'validation.json') -Raw | ConvertFrom-Json
    if ($report.ArtifactDirectory -ne $artifact -or @(Get-ChildItem -LiteralPath $processDirectory -Force).Count -ne 0) {
        throw 'ArtifactDirectory did not follow the PowerShell location exclusively.'
    }
    if ([Environment]::CurrentDirectory -ne $processDirectory -or (Get-Location).Path -ne $psDirectory) {
        throw 'Validation changed the caller directory.'
    }
    $property = "-p:CppCompilerAndLinker=$compiler"
    if ($report.Host.CppCompilerAndLinker -ne $compiler -or $report.Options.NativeMSBuildProperties -notcontains $property) {
        throw 'Resolved compiler path and property were not reported.'
    }
    foreach ($name in @('target-platform', 'native-publish', 'native-publish-restore-missing-assets', 'native-publish-retry', 'native-test-discovery')) {
        $step = @($report.Steps | Where-Object Name -eq $name)
        if ($step.Count -ne 1 -or $step[0].Arguments -notcontains $property) { throw "Compiler property missing from $name." }
    }
    $compilerStep = $report.Steps | Where-Object Name -eq 'native-compiler-version'
    if ($compilerStep.File -ne $compiler -or $compilerStep.Arguments.Count -ne 1 -or $compilerStep.Arguments[0] -ne '--version' -or
        @($report.Steps | Where-Object Name -eq 'release-build')[0].Arguments -contains $property -or
        $report.ValidationSucceeded -or $report.Errors -notcontains 'Native traversal returned no core tests.') {
        throw 'Compiler selection, managed isolation, or discovery failure contract changed.'
    }

    $before = Get-Content -LiteralPath (Join-Path $artifact 'validation.json') -Raw
    try {
        & $caseScript -ArtifactDirectory '.\nested\override'
        throw 'Expected an existing-directory rejection.'
    } catch {
        if ($_.Exception.Message -notlike 'Artifact directory already exists;*') { throw }
    }
    if ((Get-Content -LiteralPath (Join-Path $artifact 'validation.json') -Raw) -ne $before) {
        throw 'Existing evidence was modified.'
    }
    try {
        & $caseScript -ArtifactDirectory 'Env:\VALIDATE_PLATFORM_TEST'
        throw 'Expected a non-filesystem rejection.'
    } catch {
        if ($_.Exception.Message -ne 'ArtifactDirectory must use the FileSystem provider.') { throw }
    }

    & $caseScript -ArtifactDirectory '.\default'
    $defaultReport = Get-Content -LiteralPath (Join-Path $psDirectory 'default\validation.json') -Raw | ConvertFrom-Json
    if (($defaultReport.Steps | Where-Object Name -eq 'native-compiler-version').File -ne 'clang' -or
        $defaultReport.Options.NativeMSBuildProperties.Count -ne 0) { throw 'Default clang selection changed.' }

    $failCompilerVersion = $true
    & $caseScript -ArtifactDirectory '.\bad-version' -CppCompilerAndLinker $compiler
    $versionReport = Get-Content -LiteralPath (Join-Path $psDirectory 'bad-version\validation.json') -Raw | ConvertFrom-Json
    if ($LASTEXITCODE -ne 1 -or $versionReport.ValidationSucceeded -or
        @($versionReport.Steps | Where-Object Name -in @('release-build', 'native-publish')).Count -ne 0 -or
        $versionReport.Errors -notcontains 'Command failed: native-compiler-version. See native-compiler-version.log.') {
        throw 'A failed explicit compiler version check did not block execution.'
    }

    & $caseScript -ArtifactDirectory '.\missing-compiler' -CppCompilerAndLinker (Join-Path $root 'missing-compiler')
    $missingReport = Get-Content -LiteralPath (Join-Path $psDirectory 'missing-compiler\validation.json') -Raw | ConvertFrom-Json
    if ($LASTEXITCODE -ne 1 -or $missingReport.Errors.Count -eq 0 -or $missingReport.Steps.Count -ne 0) {
        throw 'An invalid compiler must fail before invoking commands.'
    }

    & $caseScript -ArtifactDirectory '.\compiler-directory' -CppCompilerAndLinker $fixture
    $directoryReport = Get-Content -LiteralPath (Join-Path $psDirectory 'compiler-directory\validation.json') -Raw | ConvertFrom-Json
    if ($LASTEXITCODE -ne 1 -or $directoryReport.Errors.Count -eq 0 -or $directoryReport.Steps.Count -ne 0) {
        throw 'A compiler directory must fail before invoking commands.'
    }

    $simulateLinux = $false
    & $caseScript -ArtifactDirectory '.\unsupported-os' -CppCompilerAndLinker $compiler
    $osReport = Get-Content -LiteralPath (Join-Path $psDirectory 'unsupported-os\validation.json') -Raw | ConvertFrom-Json
    if ($LASTEXITCODE -ne 1 -or $osReport.Errors -notcontains '-CppCompilerAndLinker is supported only on Linux; macOS and Windows retain their platform toolchains.') {
        throw 'An explicit compiler on an unsupported OS was not rejected.'
    }

    $runManaged = $true
    $failCompilerVersion = $false
    $filter = '/*/*/JsonReaderTests/*'
    foreach ($windows in @($false, $true)) {
        $simulateWindows = $windows
        $simulateLinux = !$windows
        foreach ($managedReportMode in @('valid', 'missing', 'zero', 'failed', 'incomplete', 'exit')) {
            $case = "managed-$windows-$managedReportMode"
            & $caseScript -ArtifactDirectory ".\$case" -TestFilter $filter
            $managedReport = Get-Content -LiteralPath (Join-Path $psDirectory "$case\validation.json") -Raw | ConvertFrom-Json
            $expected = @(
                'Nerdbank.Json.Tests|net8.0', 'Nerdbank.Json.Tests|net9.0', 'Nerdbank.Json.Tests|net10.0',
                'Nerdbank.Json.AspNetCoreMvcFormatter.Tests|net8.0', 'Nerdbank.Json.AspNetCoreMvcFormatter.Tests|net9.0',
                'Nerdbank.Json.AspNetCoreMvcFormatter.Tests|net10.0', 'Nerdbank.Json.Analyzers.Tests|net8.0'
            )
            if ($windows) { $expected += 'Nerdbank.Json.Tests|net472' }
            $managedSteps = @($managedReport.Steps | Where-Object Name -like 'managed-*')
            if ($managedSteps.Count -ne $expected.Count) { throw "Incomplete managed matrix in $case." }
            foreach ($target in $expected) {
                $project, $framework = $target.Split('|')
                $name = "managed-$project-$framework"
                $step = @($managedSteps | Where-Object Name -eq $name)
                if ($step.Count -ne 1) { throw "Missing or duplicate target $name." }
                $step = $step[0]
                $extension = if ($framework -eq 'net472') { '.exe' } else { '.dll' }
                $binary = Join-Path $fixture "bin\$project\Release\$framework\$project$extension"
                $directory = Join-Path $managedReport.ArtifactDirectory $name
                $expectedArgs = @(
                    '--treenode-filter', $filter, '--results-directory', $directory,
                    '--report-trx', '--report-trx-filename', "$name.trx",
                    '--diagnostic', '--diagnostic-output-directory', $directory
                )
                $expectedFile = $compiler
                if ($framework -eq 'net472') { $expectedFile = $binary } else { $expectedArgs = @($binary) + $expectedArgs }
                if ($step.File -ne $expectedFile -or $step.TargetFramework -ne $framework -or
                    (ConvertTo-Json -InputObject @($step.Arguments) -Compress) -cne (ConvertTo-Json -InputObject $expectedArgs -Compress) -or
                    ![IO.Path]::IsPathFullyQualified($directory)) {
                    throw "Wrong executable, framework, or direct MTP arguments for $name."
                }
            }
            $expectedStatus = if ($managedReportMode -eq 'valid') { 'PASS' } else { 'FAIL' }
            if ($managedReport.Stages.ManagedTests -ne $expectedStatus -or $managedReport.ValidationSucceeded -or $LASTEXITCODE -ne 1) {
                throw "Managed report/exit failure semantics changed in $case."
            }
            if ($managedReportMode -eq 'valid' -and ($managedReport.TestReports.Count -ne $expected.Count -or
                @($managedReport.TestReports | Where-Object { $_.Native -or $_.Executed -ne 1 -or
                    $_.Skipped.Count -ne 1 -or $_.Skipped[0].KnownNativeArrayLimitation }).Count -ne 0)) {
                throw 'Managed counters or skip classification changed.'
            }
            if ($managedReportMode -eq 'missing' -and @($managedReport.Errors | Where-Object { $_ -like 'Missing test report in *' }).Count -ne $expected.Count) {
                throw 'Missing TRX reports were not all rejected.'
            }
            if ($managedReportMode -eq 'zero' -and @($managedReport.Errors | Where-Object { $_ -like 'Test report has no executed tests:*' }).Count -ne $expected.Count) {
                throw 'Zero-execution TRX reports were not all rejected.'
            }
        }
    }
    Write-Host 'Platform script checks passed: paths, evidence preservation, compiler guards/forwarding, all seven managed targets plus Windows net472, direct MTP arguments, and report/exit failures.'
} finally {
    Set-Location $savedLocation
    [Environment]::CurrentDirectory = $savedProcessDirectory
    Remove-Item -LiteralPath $root -Recurse -Force
}

if ($ManagedSmoke) {
    $repo = Split-Path $PSScriptRoot -Parent
    if (!$SmokeArtifactDirectory) {
        $SmokeArtifactDirectory = Join-Path $repo "obj\platform-managed-smoke-$([guid]::NewGuid().ToString('N'))"
    }
    $ArtifactDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($SmokeArtifactDirectory)
    if (Test-Path -LiteralPath $ArtifactDirectory) { throw "Smoke artifact directory already exists: $ArtifactDirectory" }
    $null = New-Item -ItemType Directory -Path $ArtifactDirectory
    $steps = [Collections.Generic.List[object]]::new()
    $testReports = [Collections.Generic.List[object]]::new()
    $report = @{ Errors = [Collections.Generic.List[string]]::new() }
    $TestFilter = '/*/*/JsonReaderTests/*'
    foreach ($name in @('Invoke-Recorded', 'Read-TestReport', 'Invoke-TestRun', 'Invoke-ManagedTestRun')) {
        $node = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $true)
        . ([scriptblock]::Create($node.Extent.Text))
    }
    . (Join-Path $repo '.github\Prime-ForCopilot.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Prime-ForCopilot failed.' }
    $dotnetExecutable = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
    $sdk = Invoke-Recorded 'smoke-sdk' $dotnetExecutable @('--version')
    $requiredSdk = (Get-Content -LiteralPath (Join-Path $repo 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if (!$sdk.Succeeded -or $sdk.Text.Trim() -ne $requiredSdk) { throw "Smoke requires SDK $requiredSdk." }
    $succeeded = Invoke-ManagedTestRun 'Nerdbank.Json.Tests' 'net8.0'
    @{
        Succeeded = $succeeded; SDK = $sdk.Text.Trim(); Repository = $repo; ArtifactDirectory = $ArtifactDirectory
        Steps = $steps; TestReports = $testReports; Errors = $report.Errors
    } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'smoke.json')
    if (!$succeeded) { throw "Managed smoke failed; evidence: $ArtifactDirectory" }
    Write-Host "Managed JsonReaderTests/TRX smoke passed: $ArtifactDirectory"
}
