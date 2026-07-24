using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;
using Canvas = System.Windows.Controls.Canvas;
using ContextMenu = System.Windows.Controls.ContextMenu;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using ItemsControl = System.Windows.Controls.ItemsControl;
using MenuItem = System.Windows.Controls.MenuItem;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using TileStart.Host.Applications;
using TileStart.Host.Icons;
using TileStart.Host.Shell;
using TileStart.Host.Windowing;
using TileStart.Host.Navigation;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Tiles.Layout;
using TileStart.Host.Tiles.DragDrop;
using TileStart.Host.Tiles.Settings;
using TileStart.Host.Persistence;
using TileStart.Host.Utilities;
using TileStart.Host.Tiles.Folders;

namespace TileStart.Host;

public partial class MainWindow
{
    private void StartContextMenu_Opened(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.StartContextMenu_Opened(sender, e);

    private void SubmenuPopup_Opened(object? sender, EventArgs e) =>
        _tileWorkspaceController.SubmenuPopup_Opened(sender, e);

    private void SubmenuPopup_Closed(object? sender, EventArgs e) =>
        _tileWorkspaceController.SubmenuPopup_Closed(sender, e);

    private void StartContextMenu_Closed(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.StartContextMenu_Closed(sender, e);

    private void PinAppToStart_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.PinAppToStart_Click(sender, e);

    private void UnpinAppFromStart_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.UnpinAppFromStart_Click(sender, e);

    private void RemoveCustomApp_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.RemoveCustomApp_Click(sender, e);

    private void TileButton_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.TileButton_Click(sender, e);

    private void TileSettings_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.TileSettings_Click(sender, e);

    private void UnpinTile_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.UnpinTile_Click(sender, e);

    private void DissolveFolder_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.DissolveFolder_Click(sender, e);

    private void ResizeTile_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.ResizeTile_Click(sender, e);

    private void OpenTileFileLocation_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.OpenTileFileLocation_Click(sender, e);

    private void UninstallApp_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.UninstallApp_Click(sender, e);

    private void UninstallTile_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.UninstallTile_Click(sender, e);

    private void PinAppToTaskbar_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.PinAppToTaskbar_Click(sender, e);

    private void PinTileToTaskbar_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.PinTileToTaskbar_Click(sender, e);

    private void RunTileAsAdministrator_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.RunTileAsAdministrator_Click(sender, e);

    private void AddCommandTile_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.AddCommandTile_Click(sender, e);

    private void GroupHeader_NameCommitted(object sender, EventArgs e) =>
        _tileWorkspaceController.GroupHeader_NameCommitted(sender, e);

    private void DeleteGroup_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.DeleteGroup_Click(sender, e);

    private void GroupSettings_Click(object sender, RoutedEventArgs e) =>
        _tileWorkspaceController.GroupSettings_Click(sender, e);
}
