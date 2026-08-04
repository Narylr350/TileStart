[CmdletBinding()]
param(
    [ValidateSet('Paths', 'Summary', 'Validate', 'Apply')]
    [string]$Action = 'Summary',
    [string]$InputPath,
    [string]$OutputPath,
    [string]$DataDirectory = (Join-Path $env:LOCALAPPDATA 'TileStart'),
    [string]$HostPath,
    [switch]$NoRestart,
    [switch]$SkipHostLifecycle
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$layoutPath = Join-Path $DataDirectory 'layout.json'
$preferencePath = Join-Path $DataDirectory 'ai-layout-preferences.json'

function Read-JsonDocument([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "File not found: $Path"
    }

    Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Get-TileSpan([string]$Size) {
    switch ($Size) {
        'Small' { return @(1, 1) }
        'Medium' { return @(2, 2) }
        'Wide' { return @(4, 2) }
        'Large' { return @(4, 4) }
        default { return $null }
    }
}

function Add-TileCollectionErrors(
    [object[]]$Tiles,
    [int]$Columns,
    [Nullable[int]]$Rows,
    [string]$Owner,
    [System.Collections.Generic.HashSet[string]]$TileIds,
    [System.Collections.Generic.List[string]]$Errors,
    [bool]$AllowFolders
) {
    $occupied = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($tile in $Tiles) {
        $tileId = [string]$tile.Id
        if ([string]::IsNullOrWhiteSpace($tileId)) {
            $Errors.Add("$Owner contains a tile without Id.")
        }
        elseif (-not $TileIds.Add($tileId)) {
            $Errors.Add("Duplicate tile Id: $tileId")
        }

        $span = Get-TileSpan ([string]$tile.Size)
        if ($null -eq $span) {
            $Errors.Add("$Owner tile '$($tile.Name)' has unsupported size '$($tile.Size)'.")
            continue
        }

        $column = [int]$tile.Column
        $row = [int]$tile.Row
        $columnSpan = [int]$span[0]
        $rowSpan = [int]$span[1]
        if ($column -lt 0 -or $row -lt 0 -or $column + $columnSpan -gt $Columns) {
            $Errors.Add("$Owner tile '$($tile.Name)' is outside the horizontal bounds.")
            continue
        }
        if ($null -ne $Rows -and $row + $rowSpan -gt [int]$Rows) {
            # PowerShell 会把非空 Nullable<int> 解包为普通 Int32，不能再访问 .Value。
            $Errors.Add("$Owner tile '$($tile.Name)' is outside the fixed group height.")
            continue
        }

        for ($y = $row; $y -lt $row + $rowSpan; $y++) {
            for ($x = $column; $x -lt $column + $columnSpan; $x++) {
                $cell = "$x,$y"
                if (-not $occupied.Add($cell)) {
                    $Errors.Add("$Owner has overlapping tiles at cell $cell.")
                }
            }
        }

        $children = @($tile.FolderTiles)
        if ($children.Count -eq 0) {
            continue
        }
        if (-not $AllowFolders -or -not [bool]$tile.IsTileFolder) {
            $Errors.Add("$Owner tile '$($tile.Name)' contains invalid nested items.")
            continue
        }
        if ($children | Where-Object { [bool]$_.IsTileFolder }) {
            $Errors.Add("$Owner folder '$($tile.Name)' contains a nested folder.")
        }
        Add-TileCollectionErrors $children 8 $null "$Owner folder '$($tile.Name)'" $TileIds $Errors $false
    }
}

function Get-ValidationErrors([object]$Layout) {
    $errors = [System.Collections.Generic.List[string]]::new()
    if ([int]$Layout.Version -ne 2) {
        $errors.Add("Unsupported layout Version '$($Layout.Version)'; expected 2.")
    }

    $groupIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $tileIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $outerCells = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($group in @($Layout.Groups)) {
        $groupId = [string]$group.Id
        if ([string]::IsNullOrWhiteSpace($groupId)) {
            $errors.Add('A group is missing Id.')
        }
        elseif (-not $groupIds.Add($groupId)) {
            $errors.Add("Duplicate group Id: $groupId")
        }

        $widthUnits = [int]$group.WidthUnits
        $heightUnits = [int]$group.HeightUnits
        $groupColumn = [int]$group.GroupColumn
        $groupRow = [int]$group.GroupRow
        if ($widthUnits -lt 1 -or $widthUnits -gt 8) {
            $errors.Add("Group '$($group.Name)' has invalid WidthUnits $widthUnits.")
        }
        if ($heightUnits -lt 0 -or $heightUnits -gt 8) {
            $errors.Add("Group '$($group.Name)' has invalid HeightUnits $heightUnits.")
        }
        if ($groupColumn -lt 0 -or $groupRow -lt 0) {
            $errors.Add("Group '$($group.Name)' has invalid outer cell ($groupColumn,$groupRow).")
        }

        for ($x = $groupColumn; $x -lt $groupColumn + $widthUnits; $x++) {
            $cell = "$x,$groupRow"
            if (-not $outerCells.Add($cell)) {
                $errors.Add("Outer groups overlap at cell $cell.")
            }
        }

        $rows = if ($heightUnits -eq 0) { $null } else { [Nullable[int]]($heightUnits * 2) }
        Add-TileCollectionErrors @($group.Tiles) ($widthUnits * 2) $rows "Group '$($group.Name)'" $tileIds $errors $true
    }

    return $errors
}

function Convert-TileSummary([object]$Tile) {
    [ordered]@{
        id = [string]$Tile.Id
        name = [string]$Tile.Name
        target = [string]$Tile.LaunchTarget
        size = [string]$Tile.Size
        column = [int]$Tile.Column
        row = [int]$Tile.Row
        isFolder = [bool]$Tile.IsTileFolder
        folderItems = @($Tile.FolderTiles | ForEach-Object { Convert-TileSummary $_ })
    }
}

function Get-LayoutSummary([object]$Layout) {
    $preferences = if (Test-Path -LiteralPath $preferencePath) {
        Read-JsonDocument $preferencePath
    }
    else {
        $null
    }

    [ordered]@{
        layoutPath = $layoutPath
        preferencePath = $preferencePath
        preferences = $preferences
        groups = @($Layout.Groups | Sort-Object GroupRow, GroupColumn | ForEach-Object {
            [ordered]@{
                id = [string]$_.Id
                name = [string]$_.Name
                groupColumn = [int]$_.GroupColumn
                groupRow = [int]$_.GroupRow
                widthUnits = [int]$_.WidthUnits
                heightUnits = [int]$_.HeightUnits
                tiles = @($_.Tiles | Sort-Object Row, Column | ForEach-Object { Convert-TileSummary $_ })
            }
        })
    }
}

function Write-JsonResult([object]$Value) {
    $json = $Value | ConvertTo-Json -Depth 100
    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        $json
        return
    }

    $parent = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [IO.File]::WriteAllText($OutputPath, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    [ordered]@{ written = (Resolve-Path -LiteralPath $OutputPath).Path } | ConvertTo-Json
}

if ($Action -eq 'Paths') {
    Write-JsonResult ([ordered]@{ layoutPath = $layoutPath; preferencePath = $preferencePath })
    exit 0
}

$sourcePath = if ([string]::IsNullOrWhiteSpace($InputPath)) { $layoutPath } else { $InputPath }
$layout = Read-JsonDocument $sourcePath
$errors = @(Get-ValidationErrors $layout)

if ($Action -eq 'Validate') {
    Write-JsonResult ([ordered]@{ valid = $errors.Count -eq 0; errors = $errors; path = $sourcePath })
    if ($errors.Count -gt 0) { exit 1 }
    exit 0
}

if ($Action -eq 'Summary') {
    if ($errors.Count -gt 0) {
        throw "Layout validation failed: $($errors -join '; ')"
    }
    Write-JsonResult (Get-LayoutSummary $layout)
    exit 0
}

if ($errors.Count -gt 0) {
    throw "Layout validation failed: $($errors -join '; ')"
}

$runningHost = $null
$restartPath = $HostPath
if (-not $SkipHostLifecycle) {
    $runningHost = Get-Process TileStart.Host -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $runningHost -and [string]::IsNullOrWhiteSpace($restartPath)) {
        $restartPath = $runningHost.Path
    }
    if ($null -ne $runningHost) {
        & $runningHost.Path --shutdown | Out-Null
        $runningHost | Wait-Process -Timeout 10
    }
}

New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null
$backupPath = $null
if (Test-Path -LiteralPath $layoutPath) {
    $backupDirectory = Join-Path $DataDirectory 'layout-backups'
    New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
    $backupPath = Join-Path $backupDirectory ("layout-{0}.json" -f ([Guid]::NewGuid().ToString('N')))
    Copy-Item -LiteralPath $layoutPath -Destination $backupPath
}

$candidateJson = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
$temporaryPath = $layoutPath + '.tmp'
[IO.File]::WriteAllText($temporaryPath, $candidateJson, [Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $temporaryPath -Destination $layoutPath -Force

$restarted = $false
if (-not $SkipHostLifecycle -and -not $NoRestart -and -not [string]::IsNullOrWhiteSpace($restartPath)) {
    Start-Process -FilePath $restartPath -WindowStyle Hidden
    $restarted = $true
}

Write-JsonResult ([ordered]@{
    applied = $true
    layoutPath = $layoutPath
    backupPath = $backupPath
    hostRestarted = $restarted
})
