param(
    [Parameter(Mandatory)]
    [string]$GhidraHome,

    [string]$OutputPath = (Join-Path $env:TEMP 'TileStart\reverse\startui-icon-deep-evidence.txt'),
    [switch]$RecreateProject
)

$ErrorActionPreference = 'Stop'

$needles = @(
    'TileViewModel::UpdateDisplayNameAndSmallLogoVisibility',
    'TileViewModel::LoadSmallLogo',
    'TileViewModel::GetDisplayNameToUse',
    'TileViewModel::SmallLogo',
    'TileViewControl::UpdateVisualState',
    'AppItemViewModel::Logo',
    'AppItemViewModel::LogoBackground',
    'GetImageResolverForTileItem',
    'GetTileImageResource',
    'GetLogoOptionsForTileBranding',
    'GetLogoOptionsForAppList',
    'GetAppItemLogoWidthAndHeight',
    'LogoLoaderUTM::LogoLoaderUTM',
    'AppItemMetrics::ToLogoType',
    'LogoType',
    'ResolvedLogoInfo',
    'OnRealization'
)

$invoke = Join-Path $PSScriptRoot 'Invoke-StartUiGhidra.ps1'
$export = Join-Path $PSScriptRoot 'Export-StartUiSymbolSpec.ps1'
& $invoke -GhidraHome $GhidraHome -Needle $needles -OutputPath $OutputPath -RecreateProject:$RecreateProject | Out-Host
& $export -EvidenceManifestPath "$OutputPath.manifest.json"
