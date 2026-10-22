[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path,
    [Parameter(Mandatory)]
    [string]$RuntimeIdentifier,
    [Parameter(Mandatory)]
    [string]$OutputPath,
    [ValidateRange(0.000001, [double]::MaxValue)]
    [double]$ExpectedSizeMiB,
    [ValidateRange(0, [double]::MaxValue)]
    [double]$AllowedVarianceMiB = 0.2
)

$ErrorActionPreference = 'Stop'
$image = Get-Item -LiteralPath $Path
if ($image.PSIsContainer -or $image.Length -le 0) {
    throw "Expected a nonempty native executable: $Path"
}

& $image.FullName
if ($LASTEXITCODE -ne 0) {
    throw "Native executable failed with exit code $LASTEXITCODE`: $Path"
}

$hasBudget = $PSBoundParameters.ContainsKey('ExpectedSizeMiB')
$sizeResult = if (!$hasBudget) {
    'NOT_CALIBRATED'
} elseif ([math]::Abs($image.Length / 1MB - $ExpectedSizeMiB) -le $AllowedVarianceMiB) {
    'PASS'
} else {
    'FAIL'
}

[ordered]@{
    FileName = $image.Name
    RuntimeIdentifier = $RuntimeIdentifier
    SizeBytes = $image.Length
    SizeMiB = $image.Length / 1MB
    Result = 'PASS'
    ExpectedSizeMiB = if ($hasBudget) { $ExpectedSizeMiB } else { $null }
    AllowedVarianceMiB = if ($hasBudget) { $AllowedVarianceMiB } else { $null }
    SizeRegressionResult = $sizeResult
} | ConvertTo-Json | Set-Content -LiteralPath $OutputPath

if ($sizeResult -eq 'FAIL') {
    throw "Native image size $($image.Length / 1MB) MiB is outside $ExpectedSizeMiB +/- $AllowedVarianceMiB MiB: $Path"
}
