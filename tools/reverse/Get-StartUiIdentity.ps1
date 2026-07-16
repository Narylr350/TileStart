param(
    [string]$StartUiPath,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($StartUiPath)) {
    $package = Get-ChildItem 'C:\Windows\SystemApps' -Directory |
        Where-Object Name -Like 'Microsoft.Windows.StartMenuExperienceHost_*' |
        Select-Object -First 1
    if ($null -eq $package) {
        throw 'Microsoft.Windows.StartMenuExperienceHost package was not found.'
    }

    $StartUiPath = Join-Path $package.FullName 'StartUI.dll'
}

$StartUiPath = (Resolve-Path -LiteralPath $StartUiPath).Path
$bytes = [IO.File]::ReadAllBytes($StartUiPath)
$rsdsOffset = -1
for ($index = 0; $index -le $bytes.Length - 24; $index++) {
    if ($bytes[$index] -eq 0x52 -and $bytes[$index + 1] -eq 0x53 -and
        $bytes[$index + 2] -eq 0x44 -and $bytes[$index + 3] -eq 0x53) {
        $rsdsOffset = $index
        break
    }
}

if ($rsdsOffset -lt 0) {
    throw "RSDS record was not found in $StartUiPath."
}

$guidBytes = [byte[]]::new(16)
[Array]::Copy($bytes, $rsdsOffset + 4, $guidBytes, 0, 16)
$pdbGuid = [Guid]::new($guidBytes)
$pdbAge = [BitConverter]::ToUInt32($bytes, $rsdsOffset + 20)
$pdbStart = $rsdsOffset + 24
$pdbEnd = $pdbStart
while ($pdbEnd -lt $bytes.Length -and $bytes[$pdbEnd] -ne 0) {
    $pdbEnd++
}
$pdbPath = [Text.Encoding]::UTF8.GetString($bytes, $pdbStart, $pdbEnd - $pdbStart)
$pdbName = [IO.Path]::GetFileName($pdbPath)
$symbolKey = ($pdbGuid.ToString('N') + $pdbAge.ToString('x')).ToUpperInvariant()
$file = Get-Item -LiteralPath $StartUiPath

$result = [ordered]@{
    schemaVersion = 1
    startUiPath = $StartUiPath
    fileVersion = $file.VersionInfo.FileVersion
    sizeBytes = $file.Length
    sha256 = (Get-FileHash -LiteralPath $StartUiPath -Algorithm SHA256).Hash
    pdb = [ordered]@{
        name = $pdbName
        embeddedPath = $pdbPath
        guid = $pdbGuid.ToString().ToUpperInvariant()
        age = $pdbAge
        symbolKey = $symbolKey
    }
}

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $parent = Split-Path -Parent $OutputPath
    if ($parent) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
}

[pscustomobject]$result
