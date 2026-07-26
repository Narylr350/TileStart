# AI-friendly layout management

TileStart keeps the complete runtime layout in `%LOCALAPPDATA%\TileStart\layout.json`.
The layout remains user-controlled: TileStart does not impose a category, ordering, capacity, or overflow-folder policy.

Use `scripts\Manage-Layout.ps1` when an AI or local automation needs to inspect or replace the layout safely.

## Commands

```powershell
# Print the authoritative paths.
powershell -ExecutionPolicy Bypass -File scripts\Manage-Layout.ps1 -Action Paths

# Produce a concise, AI-readable summary without dropping full tile IDs or targets.
powershell -ExecutionPolicy Bypass -File scripts\Manage-Layout.ps1 -Action Summary -OutputPath "$env:TEMP\TileStart-layout-summary.json"

# Validate a candidate full layout before applying it.
powershell -ExecutionPolicy Bypass -File scripts\Manage-Layout.ps1 -Action Validate -InputPath "$env:TEMP\layout-candidate.json"

# Validate, back up the current layout, stop the running Host, atomically replace the file, and restart the same Host executable.
powershell -ExecutionPolicy Bypass -File scripts\Manage-Layout.ps1 -Action Apply -InputPath "$env:TEMP\layout-candidate.json"
```

`Apply` stores safety copies under `%LOCALAPPDATA%\TileStart\layout-backups`.
The candidate file uses the same versioned structure as `layout.json`, so custom tile appearance, commands, folders, and stable IDs are preserved.

## Local preferences

Optional personal organization preferences may be stored in:

```text
%LOCALAPPDATA%\TileStart\ai-layout-preferences.json
```

The tool includes that file in `Summary`, but neither the tool nor TileStart interprets it as a product default. It is guidance for the current user and AI only. Personal rules must remain optional, allow explicit exceptions, and must not be committed as TileStart defaults.
