param(
    [string]$StartUiPath,
    [string]$DestinationRoot = (Join-Path $env:TEMP 'TileStart\symbols'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$identityScript = Join-Path $PSScriptRoot 'Get-StartUiIdentity.ps1'
$identity = & $identityScript -StartUiPath $StartUiPath
$destination = Join-Path $DestinationRoot (Join-Path $identity.pdb.symbolKey $identity.pdb.name)
$destinationDirectory = Split-Path -Parent $destination
$partial = "$destination.partial"

if ((Test-Path -LiteralPath $destination) -and -not $Force) {
    Get-Item -LiteralPath $destination
    return
}

New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
if ($Force) {
    Remove-Item -LiteralPath $destination, $partial -Force -ErrorAction SilentlyContinue
}

$uri = "https://msdl.microsoft.com/download/symbols/$($identity.pdb.name)/$($identity.pdb.symbolKey)/$($identity.pdb.name)"
$curl = Get-Command curl.exe -ErrorAction SilentlyContinue
if ($null -ne $curl) {
    & $curl.Source --fail --location --retry 5 --retry-delay 2 --continue-at - --output $partial $uri
    if ($LASTEXITCODE -ne 0) {
        throw "curl failed with exit code $LASTEXITCODE while downloading $uri"
    }
} else {
    Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
    Invoke-WebRequest -Uri $uri -OutFile $partial -MaximumRetryCount 3 -RetryIntervalSec 2
}

$file = Get-Item -LiteralPath $partial
if ($file.Length -eq 0) {
    throw "Downloaded PDB is empty: $uri"
}

Move-Item -LiteralPath $partial -Destination $destination -Force
Get-Item -LiteralPath $destination
