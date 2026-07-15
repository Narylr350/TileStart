[CmdletBinding()]
param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$packageRoot = Join-Path $artifactsRoot 'package'
$publishDirectory = Join-Path $packageRoot 'TileStart'
$portableArchive = Join-Path $packageRoot 'TileStart-portable-win-x64.zip'
$artifactsFullPath = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$packageFullPath = [IO.Path]::GetFullPath($packageRoot)
if (-not $packageFullPath.StartsWith($artifactsFullPath, [StringComparison]::OrdinalIgnoreCase))
{
    throw "Package directory must stay under artifacts: $packageFullPath"
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild)
{
    throw 'MSBuild was not found.'
}

& $msbuild (Join-Path $repoRoot 'TileStart.sln') /restore /p:Configuration=Release /p:Platform=x64 /m /v:minimal
if ($LASTEXITCODE -ne 0)
{
    throw "MSBuild failed with exit code $LASTEXITCODE."
}

if (Test-Path $packageRoot)
{
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory | Out-Null

& dotnet publish (Join-Path $repoRoot 'src\TileStart.Host\TileStart.Host.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --source 'https://api.nuget.org/v3/index.json' `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$nativeDirectory = Join-Path $artifactsRoot 'Release\x64'
Copy-Item (Join-Path $nativeDirectory 'TileStart.Injector.exe') $publishDirectory
Copy-Item (Join-Path $nativeDirectory 'TileStart.ShellHook.dll') $publishDirectory

Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $portableArchive -CompressionLevel Optimal
Write-Host "Portable package: $portableArchive"

if ($SkipInstaller)
{
    return
}

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc)
{
    throw 'Inno Setup 6 was not found. Install it or run this script with -SkipInstaller.'
}

& $iscc (Join-Path $repoRoot 'installer\TileStart.iss')
if ($LASTEXITCODE -ne 0)
{
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}
