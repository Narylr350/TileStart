namespace TileStart.Host.Shell;

[Flags]
internal enum WinKeyAction
{
    None = 0,
    Suppress = 1,
    OpenTileStart = 2,
    InjectWinDown = 4,
    InjectCurrentKeyDown = 8,
    InjectWinUp = 16,
}