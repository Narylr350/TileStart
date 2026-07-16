param(
    [string]$PriPath = 'C:\Windows\SystemResources\Windows.UI.ShellCommon\Windows.UI.ShellCommon.pri',
    [string]$WorkspaceRoot = (Join-Path $env:TEMP 'TileStart\reverse\startui-xbf'),
    [string]$MakePriPath,
    [string]$XbfToolsCommit = 'dbeadcd75f30fb8dea3109039e0082854cb9a89d',
    [switch]$Recreate
)

$ErrorActionPreference = 'Stop'

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

$PriPath = (Resolve-Path -LiteralPath $PriPath).Path
$workspace = [IO.Path]::GetFullPath($WorkspaceRoot)
$temp = [IO.Path]::GetFullPath($env:TEMP)
if (-not $workspace.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Workspace must be under the Windows temp directory: $workspace"
}
if ($Recreate -and (Test-Path -LiteralPath $workspace)) {
    Remove-Item -LiteralPath $workspace -Recurse -Force
}

if ([string]::IsNullOrWhiteSpace($MakePriPath)) {
    $MakePriPath = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter makepri.exe -Recurse -File |
        Where-Object FullName -Match '\\x64\\makepri\.exe$' |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if ([string]::IsNullOrWhiteSpace($MakePriPath) -or -not (Test-Path -LiteralPath $MakePriPath)) {
    throw 'Windows SDK makepri.exe (x64) was not found.'
}

$dumpPath = Join-Path $workspace 'shellcommon-dump.xml'
$xbfRoot = Join-Path $workspace 'xbf'
$xamlRoot = Join-Path $workspace 'xaml'
$toolsRoot = Join-Path $env:TEMP 'TileStart\tools\XbfTools'
New-Item -ItemType Directory -Force -Path $workspace, $xbfRoot, $xamlRoot, (Split-Path $toolsRoot) | Out-Null

& $MakePriPath dump /if $PriPath /of $dumpPath /dt Detailed /o
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $dumpPath)) {
    throw "makepri dump failed with exit code $LASTEXITCODE"
}

[xml]$dump = Get-Content -LiteralPath $dumpPath -Raw
$resources = @($dump.SelectNodes("//*[local-name()='NamedResource'][contains(@uri, '/Files/StartUI/') and substring(@uri, string-length(@uri) - 3) = '.xbf']"))
if ($resources.Count -eq 0) {
    throw 'No embedded StartUI XBF resources were found in the PRI dump.'
}

$extracted = foreach ($resource in $resources) {
    $base64 = $resource.SelectSingleNode(".//*[local-name()='Base64Value']")
    if ($null -eq $base64) { continue }
    $relative = $resource.uri.Substring($resource.uri.IndexOf('/Files/StartUI/') + '/Files/StartUI/'.Length)
    $path = Join-Path $xbfRoot ($relative -replace '/', '\')
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    [IO.File]::WriteAllBytes($path, [Convert]::FromBase64String($base64.InnerText.Trim()))
    [pscustomobject]@{ relativePath = $relative; path = $path }
}

if (-not (Test-Path -LiteralPath (Join-Path $toolsRoot '.git'))) {
    git clone --filter=blob:none https://github.com/chausner/XbfTools.git $toolsRoot
    if ($LASTEXITCODE -ne 0) { throw 'Failed to clone XbfTools.' }
}
git -C $toolsRoot fetch --quiet origin $XbfToolsCommit
git -C $toolsRoot checkout --quiet --detach $XbfToolsCommit
if ($LASTEXITCODE -ne 0) { throw "Failed to checkout XbfTools commit $XbfToolsCommit." }
$actualCommit = (git -C $toolsRoot rev-parse HEAD).Trim()
if ($actualCommit -ne $XbfToolsCommit) { throw "Unexpected XbfTools commit: $actualCommit" }

Push-Location $toolsRoot
try {
    dotnet build '.\xbf2xaml\xbf2xaml.csproj' -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Failed to build xbf2xaml.' }
} finally {
    Pop-Location
}
$converter = Get-ChildItem (Join-Path $toolsRoot 'xbf2xaml\bin\Release') -Filter xbf2xaml.dll -Recurse -File |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($converter)) { throw 'xbf2xaml.dll was not produced.' }

$converted = foreach ($item in $extracted) {
    $output = Join-Path $xamlRoot ([IO.Path]::ChangeExtension($item.relativePath, '.xaml') -replace '/', '\')
    New-Item -ItemType Directory -Force -Path (Split-Path $output) | Out-Null
    & dotnet $converter $item.path -o $output | Out-Host
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) {
        throw "xbf2xaml failed for $($item.relativePath)."
    }
    [ordered]@{
        relativePath = $item.relativePath
        xbfSha256 = Get-Sha256 $item.path
        xamlSha256 = Get-Sha256 $output
    }
}

$manifestPath = Join-Path $workspace 'manifest.json'
$manifest = [ordered]@{
    schemaVersion = 1
    generatedAt = [DateTimeOffset]::Now.ToString('o')
    pri = [ordered]@{ path = $PriPath; sha256 = Get-Sha256 $PriPath }
    makePri = [ordered]@{ path = $MakePriPath; fileVersion = (Get-Item $MakePriPath).VersionInfo.FileVersion }
    xbfTools = [ordered]@{ repository = 'https://github.com/chausner/XbfTools'; commit = $actualCommit; converterSha256 = Get-Sha256 $converter }
    extractedCount = $extracted.Count
    convertedCount = $converted.Count
    resources = @($converted)
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8

[pscustomobject]@{
    WorkspaceRoot = $workspace
    XbfRoot = $xbfRoot
    XamlRoot = $xamlRoot
    ManifestPath = $manifestPath
    ConvertedCount = $converted.Count
}




