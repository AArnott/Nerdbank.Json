[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path,
    [Parameter(Mandatory)]
    [string]$RuntimeIdentifier
)

$ErrorActionPreference = 'Stop'
$measureScript = Join-Path $PSScriptRoot '..\..\tools\Measure-AotImage.ps1'
$sizeMiB = (Get-Item -LiteralPath $Path).Length / 1MB
$outputPath = [System.IO.Path]::GetTempFileName()

try {
    & $measureScript -Path $Path -RuntimeIdentifier $RuntimeIdentifier -OutputPath $outputPath
    $report = Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
    if ($report.SizeRegressionResult -ne 'NOT_CALIBRATED' -or $null -ne $report.ExpectedSizeMiB -or $null -ne $report.AllowedVarianceMiB) {
        throw 'A report without a budget must not claim to pass a size regression check.'
    }

    # Exercise both bounds with real native images, including exact binary-representable boundaries.
    $cases = @(
        @{ Offset = -0.201; Variance = 0.2; Result = 'FAIL' },
        @{ Offset = -0.199; Variance = 0.2; Result = 'PASS' },
        @{ Offset = 0.199; Variance = 0.2; Result = 'PASS' },
        @{ Offset = 0.201; Variance = 0.2; Result = 'FAIL' },
        @{ Offset = -0.25; Variance = 0.25; Result = 'PASS' },
        @{ Offset = 0.25; Variance = 0.25; Result = 'PASS' },
        @{ Offset = -0.25 - 1 / 1MB; Variance = 0.25; Result = 'FAIL' },
        @{ Offset = 0.25 + 1 / 1MB; Variance = 0.25; Result = 'FAIL' },
        @{ Offset = 0; Variance = 0; Result = 'PASS' }
    )

    foreach ($case in $cases) {
        $rejected = $false
        try {
            & $measureScript -Path $Path -RuntimeIdentifier $RuntimeIdentifier -OutputPath $outputPath `
                -ExpectedSizeMiB ($sizeMiB + $case.Offset) -AllowedVarianceMiB $case.Variance
        } catch {
            if ($_.Exception.Message -notlike 'Native image size * is outside *') {
                throw
            }

            $rejected = $true
        }

        $report = Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
        if ($report.SizeRegressionResult -ne $case.Result -or $rejected -ne ($case.Result -eq 'FAIL') `
            -or $report.Result -ne 'PASS' -or $report.SizeBytes -ne ($sizeMiB * 1MB) `
            -or $report.RuntimeIdentifier -ne $RuntimeIdentifier) {
            throw "Unexpected size check result for offset $($case.Offset), variance $($case.Variance): $($report | ConvertTo-Json -Compress)"
        }
    }

    Write-Host 'Native image measurement checks passed (uncalibrated report and nine budget cases).'
} finally {
    Remove-Item -LiteralPath $outputPath
}
