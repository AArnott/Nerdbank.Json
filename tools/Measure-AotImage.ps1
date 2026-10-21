[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path,
    [Parameter(Mandatory)]
    [string]$RuntimeIdentifier,
    [Parameter(Mandatory)]
    [string]$OutputPath
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

[ordered]@{
    FileName = $image.Name
    RuntimeIdentifier = $RuntimeIdentifier
    SizeBytes = $image.Length
    SizeMiB = $image.Length / 1MB
    Result = 'PASS'
} | ConvertTo-Json | Set-Content -LiteralPath $OutputPath
