#!/usr/bin/env pwsh
#requires -Version 7.2

[CmdletBinding()]
param()

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
    if ($Name -eq 'dotnet') { return [pscustomobject]@{ Source = $pwsh } }
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
        'release-build' { $succeeded = $false }
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
            Text = '$repo = ''' + $fixture.Replace("'", "''") + '''; $TestIsLinux = $simulateLinux; $TestIsMacOS = $false' }
    )
    foreach ($replacement in ($replacements | Sort-Object { $_.Node.Extent.StartOffset } -Descending)) {
        $extent = $replacement.Node.Extent
        $source = $source.Remove($extent.StartOffset, $extent.EndOffset - $extent.StartOffset).Insert($extent.StartOffset, $replacement.Text)
    }
    $source = $source -replace '\$IsLinux\b', '$TestIsLinux'
    $source = $source -replace '\$IsMacOS\b', '$TestIsMacOS'
    $caseScript = Join-Path $fixture 'Validate-Platform-Mocked.ps1'
    Set-Content -LiteralPath $caseScript -Value $source

    Set-Location $psDirectory
    [Environment]::CurrentDirectory = $processDirectory
    $simulateLinux = $true
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
    Write-Host 'Platform script checks passed: location/provider safety, evidence preservation, compiler opt-in/default/guards, and property forwarding.'
} finally {
    Set-Location $savedLocation
    [Environment]::CurrentDirectory = $savedProcessDirectory
    Remove-Item -LiteralPath $root -Recurse -Force
}
