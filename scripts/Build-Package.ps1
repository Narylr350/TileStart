[CmdletBinding()]
param(
    [switch]$SkipInstaller,
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$packageRoot = Join-Path $artifactsRoot 'package'
$publishDirectory = Join-Path $packageRoot 'TileStart'
$portableArchive = Join-Path $packageRoot 'TileStart-portable-win-x64.zip'
$checksumFile = Join-Path $packageRoot 'SHA256SUMS.txt'
$nugetSource = 'https://api.nuget.org/v3/index.json'
$hostProject = Join-Path $repoRoot 'src\TileStart.Host\TileStart.Host.csproj'
$artifactsFullPath = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$packageFullPath = [IO.Path]::GetFullPath($packageRoot)
if (-not $packageFullPath.StartsWith($artifactsFullPath, [StringComparison]::OrdinalIgnoreCase))
{
    throw "Package directory must stay under artifacts: $packageFullPath"
}

if ([string]::IsNullOrWhiteSpace($Version))
{
    [xml]$project = Get-Content -LiteralPath $hostProject -Raw
    $Version = [string]$project.Project.PropertyGroup.Version
}
if ($Version -notmatch '^\d+\.\d+\.\d+$')
{
    throw "Version must use major.minor.patch format: $Version"
}
$assemblyVersion = "$Version.0"

function Write-Checksums([string[]]$Paths)
{
    $lines = foreach ($path in $Paths)
    {
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash *$(Split-Path $path -Leaf)"
    }
    [IO.File]::WriteAllLines($checksumFile, $lines, [Text.UTF8Encoding]::new($false))
    Write-Host "Checksums: $checksumFile"
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild)
{
    throw 'MSBuild was not found.'
}

& $msbuild (Join-Path $repoRoot 'TileStart.sln') /restore /p:RestoreSources=$nugetSource /p:Configuration=Release /p:Platform=x64 /p:Version=$Version /p:AssemblyVersion=$assemblyVersion /p:FileVersion=$assemblyVersion /p:InformationalVersion=$Version /m /v:minimal
if ($LASTEXITCODE -ne 0)
{
    throw "MSBuild failed with exit code $LASTEXITCODE."
}

if (Test-Path $packageRoot)
{
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory | Out-Null

& dotnet publish $hostProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --source $nugetSource `
    -p:Version=$Version `
    -p:AssemblyVersion=$assemblyVersion `
    -p:FileVersion=$assemblyVersion `
    -p:InformationalVersion=$Version `
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
    Write-Checksums @($portableArchive)
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

& $iscc "/DAppVersion=$Version" (Join-Path $repoRoot 'installer\TileStart.iss')
if ($LASTEXITCODE -ne 0)
{
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $artifactsRoot 'installer\TileStart-Setup-win-x64.exe'
Write-Checksums @($installerPath, $portableArchive)
