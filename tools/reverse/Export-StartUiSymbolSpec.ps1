param(
    [string]$EvidenceManifestPath = (Join-Path $env:TEMP 'TileStart\reverse\startui-icon-deep-evidence.txt.manifest.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\docs\reference\win10-start\specs\icon-resolution.json')
)
$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath $EvidenceManifestPath -Raw | ConvertFrom-Json
$evidencePath = $manifest.output.path
if ((Get-FileHash -LiteralPath $evidencePath -Algorithm SHA256).Hash -ne $manifest.output.sha256) { throw 'Ghidra evidence hash mismatch.' }
$text = Get-Content -LiteralPath $evidencePath -Raw
$functions = foreach ($match in [regex]::Matches($text, '(?m)^NAME: (?<name>.+)\r?\nENTRY: (?<entry>[0-9a-fA-F]+)\r?\nSIGNATURE: (?<signature>.+)$')) {
    $name = $match.Groups['name'].Value.Trim()
    if ($name -match 'adjustor\{|^StartUI::I') { continue }
    [ordered]@{
        name = $name
        rva = ('0x{0:X}' -f ([Convert]::ToUInt64($match.Groups['entry'].Value, 16) - 0x180000000L))
        signature = $match.Groups['signature'].Value.Trim()
    }
}
$wanted = @(
 'StartUI::TileViewModel::UpdateDisplayNameAndSmallLogoVisibility','StartUI::TileViewModel::LoadSmallLogo','StartUI::TileViewModel::GetDisplayNameToUse',
 'StartUI::TileViewModel::SmallLogoHeight::[StartUI::ITileViewModel]::get','StartUI::TileViewModel::SmallLogoWidth::[StartUI::ITileViewModel]::get',
 'StartUI::TileViewModel::SmallLogoMargin::[StartUI::ITileViewModel]::get','StartUI::TileViewModel::SmallLogoStretch::[StartUI::ITileViewModel]::get',
 'StartUI::TileViewControl::UpdateVisualState','StartUI::AppItemViewModel::Logo::[StartUI::IAppItemViewModel]::get',
 'StartUI::AppItemViewModel::LogoBackground::[StartUI::IAppItemViewModel]::get',
 'StartUI::GetImageResolverForTileItem','StartUI::TileData::GetTileImageResource','StartUI::AppItemMetrics::ToLogoType',
 'StartUI::AppItemViewModel::[StartUI::IAppItemLogoSource]::SetLogoType','StartUI::AppItemViewModel::[StartUI::IAppItemViewModel]::OnRealization',
 'StartUI::ThemeManager::GetLogoOptionsForTileBranding','StartUI::ThemeManager::GetLogoOptionsForAppList',
 'StartUI::AppItemMetrics::GetAppItemLogoWidthAndHeight','StartUI::LogoLoaderUTM::LogoLoaderUTM','StartUI::LogoLoaderUTM::CacheTypeFromLogoType',
 'StartUI::LogoLoaderUTM::RawLoadImageAndCacheAsyncUnifiedTile','StartUI::LogoLoaderUTM::[StartUI::IImageResolver]::TryLoadCachedImage',
 'StartUI::LogoLoaderUTM::[StartUI::IImageResolver]::LoadCachedImageAsync','StartUI::LogoLoaderUTM::CachedEntryExistsAndIsValid',
 'StartUI::LogoLoaderUTM::LoadFromCache','StartUI::ThemeManager::GetLogoOptionsForTileGrid')
$matched = @($functions | Where-Object { $_.name -in $wanted } | Sort-Object name)
$result = [ordered]@{
 schemaVersion = 2; kind = 'icon-resolution'; status = 'partial-symbol-verified'
 target = [ordered]@{ startUiSha256=$manifest.source.sha256; startUiFileVersion=$manifest.source.fileVersion; pdbSymbolKey=$manifest.source.pdb.symbolKey; pdbSha256=$manifest.pdb.sha256 }
 evidence = [ordered]@{ kind='symbol-guided-decompilation'; outputSha256=$manifest.output.sha256; script=$manifest.script; ghidraVersion=$manifest.ghidra.version; ghidraBuildDate=$manifest.ghidra.buildDate; ghidraAnalyzeHeadlessSha256=$manifest.ghidra.analyzeHeadlessSha256 }
 functions = $matched
 verifiedObservations = @(
  [ordered]@{ fact='Small-logo loading is invoked during realization, theme changes, and display-name/small-logo visibility refresh.'; anchors=@('StartUI::TileViewModel::LoadSmallLogo','StartUI::TileViewModel::UpdateDisplayNameAndSmallLogoVisibility'); confidence='high' },
  [ordered]@{ fact='The theme-aware path requests tile image resource kind raw value 5, obtains branding options from ThemeManager, and loads a bitmap asynchronously through LogoLoader.'; anchors=@('StartUI::TileViewModel::LoadSmallLogo'); confidence='high-enum-name-unresolved' },
  [ordered]@{ fact='The alternate path asks GetImageResolverForTileItem for a resolver, attempts a cached image first, then falls back to asynchronous cached-image loading.'; anchors=@('StartUI::TileViewModel::LoadSmallLogo'); confidence='high' },
  [ordered]@{ fact='SmallLogo height, width, margin, and stretch delegate to per-frame metrics/state objects rather than literal TileViewModel constants.'; anchors=@('StartUI::TileViewModel::SmallLogoHeight::[StartUI::ITileViewModel]::get','StartUI::TileViewModel::SmallLogoWidth::[StartUI::ITileViewModel]::get','StartUI::TileViewModel::SmallLogoMargin::[StartUI::ITileViewModel]::get','StartUI::TileViewModel::SmallLogoStretch::[StartUI::ITileViewModel]::get'); confidence='high-values-unresolved' },
  [ordered]@{ fact='App-list logo and logo-background getters return independently stored ImageSource and Brush properties; a universal fixed gray backplate is not implied.'; anchors=@('StartUI::AppItemViewModel::Logo::[StartUI::IAppItemViewModel]::get','StartUI::AppItemViewModel::LogoBackground::[StartUI::IAppItemViewModel]::get'); confidence='high-selection-source-unresolved' },
  [ordered]@{ fact='Changing AppItemLogoType triggers realization only when the requested type differs; raw values 1, 2, and 3 map to internal LogoType values 2, 3, and 4.'; anchors=@('StartUI::AppItemViewModel::[StartUI::IAppItemLogoSource]::SetLogoType','StartUI::AppItemMetrics::ToLogoType'); confidence='high-enum-names-unresolved' },
  [ordered]@{ fact='App-list realization has a theme-aware UnifiedTile/LogoLoader path and a legacy LogoLoaderUTM path; the theme-aware path uses ThemeManager app-list options and AppItemMetrics dimensions.'; anchors=@('StartUI::AppItemViewModel::[StartUI::IAppItemViewModel]::OnRealization','StartUI::ThemeManager::GetLogoOptionsForAppList','StartUI::AppItemMetrics::GetAppItemLogoWidthAndHeight','StartUI::LogoLoaderUTM::LogoLoaderUTM'); confidence='high' },
  [ordered]@{ fact='GetImageResolverForTileItem constructs LogoLoaderUTM unless a process-global resolver override is installed; asset precedence is therefore inside LogoLoaderUTM rather than this factory.'; anchors=@('StartUI::GetImageResolverForTileItem'); confidence='high' },
  [ordered]@{ fact='LogoLoaderUTM checks a size- and content-type-specific StartVisualCache entry first. A cache miss falls back to ITileDataVisual.GetLogoAsync and writes the resulting bitmap back to the visual cache.'; anchors=@('StartUI::LogoLoaderUTM::[StartUI::IImageResolver]::TryLoadCachedImage','StartUI::LogoLoaderUTM::[StartUI::IImageResolver]::LoadCachedImageAsync','StartUI::LogoLoaderUTM::CachedEntryExistsAndIsValid','StartUI::LogoLoaderUTM::RawLoadImageAndCacheAsyncUnifiedTile'); confidence='high' },
  [ordered]@{ fact='App-list theme-aware realization requests TileAssetImageSize raw value 0; tile branding small-logo loading requests raw value 5.'; anchors=@('StartUI::AppItemViewModel::[StartUI::IAppItemViewModel]::OnRealization','StartUI::TileViewModel::LoadSmallLogo','StartUI::TileData::GetTileImageResource'); confidence='high-enum-names-unresolved' })
 derivedMappings = [ordered]@{
  appItemLogoTypeToLogoType = @([ordered]@{appItemLogoType=1;logoType=2},[ordered]@{appItemLogoType=2;logoType=3},[ordered]@{appItemLogoType=3;logoType=4})
  logoTypeToVisualCacheContentType = @([ordered]@{logoType=0;contentType=1},[ordered]@{logoType=1;contentType=3},[ordered]@{logoType=2;contentType=5},[ordered]@{logoType=3;contentType=6},[ordered]@{logoType=4;contentType=6},[ordered]@{logoType=5;contentType=8})
  appListLogoSizeDip = [ordered]@{
   themeAware = @([ordered]@{appItemLogoType=1;image=16;layout=24},[ordered]@{appItemLogoType=2;image=24;layout=32},[ordered]@{appItemLogoType=3;image=24;layout=32})
   legacy = @([ordered]@{appItemLogoType=1;image=24;layout=24},[ordered]@{appItemLogoType=2;image=32;layout=32},[ordered]@{appItemLogoType=3;image=32;layout=32})
  }
  tileAssetImageSizeRaw = [ordered]@{ appList=0; tileBrandingSmallLogo=5 }
 }
 missingRequestedSymbols = @($wanted | Where-Object { $_ -notin $matched.name })
 unresolved = @('semantic names of raw AppItemLogoType/LogoType/TileAssetImageSize values','source precedence inside ITileDataVisual.GetLogoAsync beyond cache-first behavior','tile small-logo per-state dimensions/margin/stretch','logo-background selection and transparent conditions')
}
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Get-Item -LiteralPath $OutputPath

