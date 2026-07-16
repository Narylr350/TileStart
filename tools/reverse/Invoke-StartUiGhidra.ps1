param(
    [Parameter(Mandatory)]
    [string]$GhidraHome,

    [Parameter(Mandatory)]
    [string[]]$Needle,

    [string]$StartUiPath,
    [string]$PdbPath,
    [string]$WorkspaceRoot = (Join-Path $env:TEMP 'TileStart\reverse\startui-ghidra'),
    [string]$OutputPath = (Join-Path $env:TEMP 'TileStart\reverse\startui-evidence.txt'),
    [switch]$RecreateProject
)

$ErrorActionPreference = 'Stop'

$headless = Join-Path $GhidraHome 'support\analyzeHeadless.bat'
if (-not (Test-Path -LiteralPath $headless)) {
    throw "Ghidra analyzeHeadless.bat was not found under $GhidraHome."
}

$identityScript = Join-Path $PSScriptRoot 'Get-StartUiIdentity.ps1'
$pdbScript = Join-Path $PSScriptRoot 'Get-StartUiPdb.ps1'
$identity = & $identityScript -StartUiPath $StartUiPath
$StartUiPath = $identity.startUiPath
if ([string]::IsNullOrWhiteSpace($PdbPath)) {
    $PdbPath = (& $pdbScript -StartUiPath $StartUiPath).FullName
} else {
    $PdbPath = (Resolve-Path -LiteralPath $PdbPath).Path
}

$projectName = "StartUI-$($identity.sha256.Substring(0, 12))"
$projectFile = Join-Path $WorkspaceRoot "$projectName.gpr"
$binaryRoot = Join-Path $WorkspaceRoot 'binary'
$binaryCopy = Join-Path $binaryRoot 'StartUI.dll'
$pdbCopy = Join-Path $binaryRoot $identity.pdb.name
$scriptRoot = Join-Path $PSScriptRoot 'ghidra'

if ($RecreateProject -and (Test-Path -LiteralPath $WorkspaceRoot)) {
    $resolvedWorkspace = [IO.Path]::GetFullPath($WorkspaceRoot)
    $resolvedTemp = [IO.Path]::GetFullPath($env:TEMP)
    if (-not $resolvedWorkspace.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to delete a Ghidra workspace outside the Windows temp directory: $resolvedWorkspace"
    }
    Remove-Item -LiteralPath $resolvedWorkspace -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $WorkspaceRoot, $binaryRoot, (Split-Path -Parent $OutputPath) | Out-Null
Copy-Item -LiteralPath $StartUiPath -Destination $binaryCopy -Force
Copy-Item -LiteralPath $PdbPath -Destination $pdbCopy -Force

$arguments = @($WorkspaceRoot, $projectName)
if (Test-Path -LiteralPath $projectFile) {
    $arguments += @('-process', 'StartUI.dll', '-noanalysis')
} else {
    $arguments += @('-import', $binaryCopy)
}
$arguments += @('-scriptPath', $scriptRoot, '-postScript', 'TargetDecompile.java', $OutputPath)
$arguments += $Needle

& $headless @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Ghidra headless analysis failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $OutputPath)) {
    throw "Ghidra did not create the expected output: $OutputPath"
}

$manifestPath = "$OutputPath.manifest.json"
$manifest = [ordered]@{
    schemaVersion = 1
    source = $identity
    pdb = [ordered]@{
        path = $PdbPath
        sizeBytes = (Get-Item -LiteralPath $PdbPath).Length
        sha256 = (Get-FileHash -LiteralPath $PdbPath -Algorithm SHA256).Hash
    }
    ghidra = [ordered]@{
        home = (Resolve-Path -LiteralPath $GhidraHome).Path
        version = ((Select-String -LiteralPath (Join-Path $GhidraHome 'Ghidra\application.properties') -Pattern '^application.version=(.+)$').Matches[0].Groups[1].Value)
        buildDate = ((Select-String -LiteralPath (Join-Path $GhidraHome 'Ghidra\application.properties') -Pattern '^application.build.date=(.+)$').Matches[0].Groups[1].Value)
        analyzeHeadlessSha256 = (Get-FileHash -LiteralPath $headless -Algorithm SHA256).Hash
    }
    projectName = $projectName
    script = 'tools/reverse/ghidra/TargetDecompile.java'
    needles = $Needle
    output = [ordered]@{
        path = (Resolve-Path -LiteralPath $OutputPath).Path
        sizeBytes = (Get-Item -LiteralPath $OutputPath).Length
        sha256 = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash
    }
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Get-Item -LiteralPath $OutputPath, $manifestPath
