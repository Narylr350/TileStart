[CmdletBinding()]
param(
    [long]$RunId,
    [switch]$KeepDownloads
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$artifactName = 'TileStart-local-hotfix'
$installerName = 'TileStart-Setup-win-x64.exe'
$metadataName = 'LOCAL-HOTFIX.json'
$checksumName = 'SHA256SUMS.txt'
$installedDirectory = Join-Path $env:ProgramFiles 'TileStart'
$installedHost = Join-Path $installedDirectory 'TileStart.Host.exe'

function Invoke-Gh([string[]]$Arguments)
{
    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "gh failed with exit code ${LASTEXITCODE}: gh $($Arguments -join ' ')"
    }

    return $output
}

function Wait-ForHostExit
{
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (Get-Process -Name 'TileStart.Host' -ErrorAction SilentlyContinue)
    {
        if ([DateTime]::UtcNow -ge $deadline)
        {
            throw 'TileStart.Host did not exit within 15 seconds.'
        }

        Start-Sleep -Milliseconds 200
    }
}

function Find-ArtifactFile([string]$Directory, [string]$Name)
{
    $matches = @(Get-ChildItem -LiteralPath $Directory -Recurse -File |
        Where-Object { $_.Name.Equals($Name, [StringComparison]::OrdinalIgnoreCase) })
    if ($matches.Count -ne 1)
    {
        throw "Expected exactly one $Name in the Local Hotfix artifact, found $($matches.Count)."
    }

    return $matches[0].FullName
}

Invoke-Gh @('auth', 'status') | Out-Null
$repository = (Invoke-Gh @('repo', 'view', '--json', 'nameWithOwner', '--jq', '.nameWithOwner')).Trim()
if ([string]::IsNullOrWhiteSpace($repository))
{
    throw 'Unable to resolve the current GitHub repository.'
}

if ($RunId -gt 0)
{
    $run = Invoke-Gh @(
        'run', 'view', $RunId.ToString(),
        '--repo', $repository,
        '--json', 'databaseId,headSha,conclusion,headBranch,url') | ConvertFrom-Json
}
else
{
    $runs = Invoke-Gh @(
        'run', 'list',
        '--repo', $repository,
        '--workflow', 'local-hotfix.yml',
        '--branch', 'main',
        '--status', 'success',
        '--limit', '1',
        '--json', 'databaseId,headSha,conclusion,headBranch,url') | ConvertFrom-Json
    $run = $runs | Select-Object -First 1
}

if ($null -eq $run -or $run.conclusion -ne 'success' -or $run.headBranch -ne 'main')
{
    throw 'No successful Local Hotfix run on main was found.'
}

$RunId = [long]$run.databaseId
$tempRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'TileStart\local-hotfix'))
$downloadDirectory = [IO.Path]::GetFullPath((Join-Path $tempRoot $RunId))
$rootPrefix = $tempRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $downloadDirectory.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase))
{
    throw "Hotfix download directory escaped the temp root: $downloadDirectory"
}

if (Test-Path -LiteralPath $downloadDirectory)
{
    Remove-Item -LiteralPath $downloadDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $downloadDirectory | Out-Null

$hostShouldBeRunning = $false
try
{
    Invoke-Gh @(
        'run', 'download', $RunId.ToString(),
        '--repo', $repository,
        '--name', $artifactName,
        '--dir', $downloadDirectory) | Out-Null

    $installerPath = Find-ArtifactFile $downloadDirectory $installerName
    $metadataPath = Find-ArtifactFile $downloadDirectory $metadataName
    $checksumPath = Find-ArtifactFile $downloadDirectory $checksumName

    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    if (([string]$metadata.runId -ne [string]$RunId) -or ([string]$metadata.commitSha -ne [string]$run.headSha))
    {
        throw 'Local Hotfix metadata does not match the selected workflow run.'
    }

    $checksumEntry = Get-Content -LiteralPath $checksumPath | ForEach-Object {
        $parts = $_.Trim() -split '\s+', 2
        if (($parts.Count -eq 2) -and ($parts[0] -match '^[0-9a-fA-F]{64}$') -and ($parts[1].TrimStart('*').Equals($installerName, [StringComparison]::OrdinalIgnoreCase)))
        {
            [PSCustomObject]@{ Hash = $parts[0]; Name = $parts[1].TrimStart('*') }
        }
    } | Select-Object -First 1
    if ($null -eq $checksumEntry)
    {
        throw "Checksum manifest does not contain $installerName."
    }

    $expectedHash = $checksumEntry.Hash
    $actualHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
    if (-not $actualHash.Equals($expectedHash, [StringComparison]::OrdinalIgnoreCase))
    {
        throw 'Local Hotfix installer SHA-256 verification failed.'
    }

    if (-not (Test-Path -LiteralPath $installedHost -PathType Leaf))
    {
        throw "Installed TileStart Host was not found: $installedHost"
    }

    & $installedHost --shutdown
    Wait-ForHostExit
    $hostShouldBeRunning = $true

    $installer = Start-Process -FilePath $installerPath `
        -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CLOSEAPPLICATIONS' `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    if ($installer.ExitCode -ne 0)
    {
        throw "Local Hotfix installer failed with exit code $($installer.ExitCode)."
    }

    $productVersion = (Get-Item -LiteralPath $installedHost).VersionInfo.ProductVersion
    if ($productVersion -ne [string]$metadata.informationalVersion)
    {
        throw "Installed ProductVersion mismatch. Expected $($metadata.informationalVersion), got $productVersion."
    }

    $stateDirectory = Join-Path $env:LOCALAPPDATA 'TileStart'
    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    Copy-Item -LiteralPath $metadataPath -Destination (Join-Path $stateDirectory 'local-hotfix.json') -Force

    Start-Process -FilePath $installedHost -WindowStyle Hidden
    Start-Sleep -Seconds 2
    $hostShouldBeRunning = $false
    $processes = @(Get-Process | Where-Object { $_.ProcessName -like 'TileStart*' })
    $unexpectedProcess = $processes | Where-Object {
        [string]::IsNullOrWhiteSpace($_.Path) -or
        (-not $_.Path.StartsWith($installedDirectory, [StringComparison]::OrdinalIgnoreCase))
    }
    if ($unexpectedProcess)
    {
        throw "A TileStart process is running outside the installed directory: $($unexpectedProcess.Path)"
    }

    foreach ($requiredProcess in @('TileStart.Host', 'TileStart.Injector'))
    {
        if ($requiredProcess -notin $processes.ProcessName)
        {
            throw "$requiredProcess is not running after the Local Hotfix installation."
        }
    }

    Write-Host "Installed TileStart local hotfix $($metadata.informationalVersion) from commit $($metadata.shortSha)."
    Write-Host "Workflow run: $($run.url)"
}
finally
{
    if ($hostShouldBeRunning -and (Test-Path -LiteralPath $installedHost -PathType Leaf))
    {
        Start-Process -FilePath $installedHost -WindowStyle Hidden
    }

    if (-not $KeepDownloads -and (Test-Path -LiteralPath $downloadDirectory))
    {
        Remove-Item -LiteralPath $downloadDirectory -Recurse -Force
    }
}
