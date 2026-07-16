param(
    [string]$EvidenceManifestPath = (Join-Path $env:TEMP 'TileStart\reverse\startui-evidence.txt.manifest.json'),
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
 'StartUI::AppItemViewModel::LogoBackground::[StartUI::IAppItemViewModel]::get')
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
  [ordered]@{ fact='App-list logo and logo-background getters return independently stored ImageSource and Brush properties; a universal fixed gray backplate is not implied.'; anchors=@('StartUI::AppItemViewModel::Logo::[StartUI::IAppItemViewModel]::get','StartUI::AppItemViewModel::LogoBackground::[StartUI::IAppItemViewModel]::get'); confidence='high-selection-source-unresolved' })
 missingRequestedSymbols = @($wanted | Where-Object { $_ -notin $matched.name })
 unresolved = @('semantic name of tile image resource kind raw value 5','exact asset-source precedence','per-state small-logo dimensions/margin/stretch','logo-background selection and transparent conditions','AppItemViewModel realization/logo-type symbols under their requested names')
}
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Get-Item -LiteralPath $OutputPath

