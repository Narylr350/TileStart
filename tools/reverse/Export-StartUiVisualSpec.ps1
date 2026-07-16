param(
    [string]$XbfWorkspace = (Join-Path $env:TEMP 'TileStart\reverse\startui-xbf'),
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\..\docs\reference\win10-start\specs'),
    [switch]$RefreshXbf
)

$ErrorActionPreference = 'Stop'

if ($RefreshXbf -or -not (Test-Path -LiteralPath (Join-Path $XbfWorkspace 'manifest.json'))) {
    & (Join-Path $PSScriptRoot 'Export-StartUiXbf.ps1') -WorkspaceRoot $XbfWorkspace -Recreate | Out-Host
}

$manifest = Get-Content -LiteralPath (Join-Path $XbfWorkspace 'manifest.json') -Raw | ConvertFrom-Json
$xamlRoot = Join-Path $XbfWorkspace 'xaml'
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

function Get-Source([string]$FileName) {
    $resource = $manifest.resources | Where-Object { ([IO.Path]::GetFileNameWithoutExtension($_.relativePath) + '.xaml') -eq $FileName } | Select-Object -First 1
    if ($null -eq $resource) { throw "No manifest entry found for $FileName" }
    [ordered]@{
        file = $FileName
        xbfResource = $resource.relativePath
        xbfSha256 = $resource.xbfSha256
        xamlSha256 = $resource.xamlSha256
        priSha256 = $manifest.pri.sha256
    }
}

function Get-Xaml([string]$FileName) {
    Get-Content -LiteralPath (Join-Path $xamlRoot $FileName) -Raw
}

function Get-RegexValue([string]$FileName, [string]$Pattern, [string]$Anchor, [scriptblock]$Convert = { param($value) [double]$value }) {
    $match = [regex]::Match((Get-Xaml $FileName), $Pattern, [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) { throw "Anchor '$Anchor' was not found in $FileName" }
    [ordered]@{
        value = & $Convert $match.Groups['value'].Value
        unit = 'DIP'
        evidence = 'compiled-xbf'
        anchor = $Anchor
        source = Get-Source $FileName
    }
}

function Get-Thickness([string]$FileName, [string]$Key) {
    Get-RegexValue $FileName ('<Thickness\s+x:Key="' + [regex]::Escape($Key) + '"\s+Value="(?<value>[^"]+)"') $Key {
        param($value) @($value.Split(',') | ForEach-Object { [double]$_ })
    }
}

function New-Spec([string]$Kind, [string]$Status) {
    [ordered]@{
        schemaVersion = 1
        kind = $Kind
        status = $Status
        target = [ordered]@{
            priSha256 = $manifest.pri.sha256
            xbfToolsCommit = $manifest.xbfTools.commit
        }
    }
}

$tileStyles = 'TileStyles.xaml'
$frame = New-Spec 'frame' 'verified'
$frame.metrics = [ordered]@{
    collapsedNavigationWidth = Get-RegexValue $tileStyles '<Double\s+x:Key="SplitViewCollapsedPaneLength"\s+Value="(?<value>[^"]+)"' 'SplitViewCollapsedPaneLength'
    navigationItemHeight = Get-RegexValue $tileStyles '<Double\s+x:Key="NavigationPaneItemHeight"\s+Value="(?<value>[^"]+)"' 'NavigationPaneItemHeight'
    allAppsWidth = Get-RegexValue $tileStyles '<Double\s+x:Key="Metrics_MFUAndAppsList_StartMenu_Width"\s+Value="(?<value>[^"]+)"' 'Metrics_MFUAndAppsList_StartMenu_Width'
    allAppsMargin = Get-Thickness $tileStyles 'Metrics_MFUAndAppsList_StartMenu_Margin'
}

$allApps = New-Spec 'all-apps' 'verified'
$allApps.metrics = [ordered]@{
    rowHeight = Get-RegexValue $tileStyles '<Double\s+x:Key="Metrics_AllAppsGridItem_Height_Stack"\s+Value="(?<value>[^"]+)"' 'Metrics_AllAppsGridItem_Height_Stack'
    groupHeaderHeight = Get-RegexValue $tileStyles '<Double\s+x:Key="Metrics_AllAppsGridHeader_Height_Stack"\s+Value="(?<value>[^"]+)"' 'Metrics_AllAppsGridHeader_Height_Stack'
    listPadding = Get-Thickness $tileStyles 'Metrics_AllAppsListView_Padding_Stack'
}

$alphabet = New-Spec 'alphabet-index' 'verified'
$alphabet.metrics = [ordered]@{
    cellSize = Get-RegexValue 'AllAppsFrame.xaml' 'ZoomedOutHeaderWidthAndHeight="(?<value>[^"]+)"' 'AllAppsPane.ZoomedOutHeaderWidthAndHeight'
    fontSize = Get-RegexValue 'AllAppsFrame.xaml' 'ZoomedOutHeaderFontSize="(?<value>[^"]+)"' 'AllAppsPane.ZoomedOutHeaderFontSize'
}

$tileContent = New-Spec 'tile-content' 'verified-layout-only'
$tileContent.metrics = [ordered]@{
    reservedBrandingSpace = Get-RegexValue $tileStyles '<Double\s+x:Key="TileReservedBrandingSpace"\s+Value="(?<value>[^"]+)"' 'TileReservedBrandingSpace'
    bottomAlignedTextMargin = Get-Thickness $tileStyles 'Metrics_Tile_TextBottomLeftAlignedElement_Margin'
    nestedPanelMargin = Get-RegexValue 'TileGridItemDataTemplates.xaml' '<TileGridNestedPanel\s+x:Name="NestedPanel"[^>]*Margin="(?<value>[^"]+)"' 'TileGridNestedPanel[NestedPanel].Margin' { param($value) @($value.Split(',') | ForEach-Object { [double]$_ }) }
    groupHeaderHeight = Get-RegexValue 'GroupHeaderControl.xaml' '<Grid\s+x:Name="ContentFrame"\s+Height="(?<value>[^"]+)"' 'GroupHeaderControl.ContentFrame.Height'
    groupTitleWrapping = Get-RegexValue 'GroupHeaderControl.xaml' '<TextBlock\s+x:Name="NameTextBlock"[^>]*TextWrapping="(?<value>[^"]+)"' 'GroupHeaderControl.NameTextBlock.TextWrapping' { param($value) $value }
}
$tileContent.notes = @('This spec confirms compiled layout resources only. Tile logo selection and rendering remain unresolved.')

$identity = & (Join-Path $PSScriptRoot 'Get-StartUiIdentity.ps1')
$icon = New-Spec 'icon-resolution' 'partial-unresolved'
$icon.target.startUiSha256 = $identity.sha256
$icon.target.startUiFileVersion = $identity.fileVersion
$icon.target.pdbSymbolKey = $identity.pdb.symbolKey
$icon.symbolAnchors = @(
    'TileViewModel::UpdateDisplayNameAndSmallLogoVisibility',
    'TileViewModel::LoadSmallLogo',
    'TileViewModel::GetDisplayNameToUse',
    'TileViewModel::SmallLogoHeight',
    'TileViewModel::SmallLogoWidth',
    'TileViewModel::SmallLogoMargin',
    'TileViewModel::SmallLogoStretch',
    'TileViewControl::UpdateVisualState',
    'AppItemViewModel::SetLogoType',
    'AppItemViewModel::Logo',
    'AppItemViewModel::LogoBackground',
    'AppItemViewModel::ResolvedLogoInfo',
    'AppItemViewModel::OnRealization'
)
$icon.unresolved = @('logo source selection precedence', 'background color selection', 'small-logo visibility conditions', 'stretch and margin state mapping')

@{
    'frame.json' = $frame
    'all-apps.json' = $allApps
    'alphabet-index.json' = $alphabet
    'tile-content.json' = $tileContent
    'icon-resolution.json' = $icon
}.GetEnumerator() | ForEach-Object {
    $_.Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputRoot $_.Key) -Encoding utf8
}

Get-ChildItem -LiteralPath $OutputRoot -Filter *.json | Sort-Object Name


