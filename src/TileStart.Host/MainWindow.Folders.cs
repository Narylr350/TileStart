using TileStart.Host.Applications;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host;

public partial class MainWindow
{
    private Task ToggleAppFolderAsync(AppEntry folder) =>
        _tileWorkspaceController.ToggleAppFolderAsync(folder);

    private Task ToggleTileFolderAsync(TileGroup group, TileItem folder) =>
        _tileWorkspaceController.ToggleTileFolderAsync(group, folder);
}