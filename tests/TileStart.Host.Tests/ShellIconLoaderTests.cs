using System.IO;
using System.Windows.Media.Imaging;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class ShellIconLoaderTests
{
    [Fact]
    public void LoadReturnsFrozenHighResolutionShellIcon()
    {
        var icon = ShellIconLoader.Load(Path.Combine(Environment.SystemDirectory, "notepad.exe"));

        var bitmap = Assert.IsAssignableFrom<BitmapSource>(icon);
        Assert.True(bitmap.IsFrozen);
        Assert.True(bitmap.PixelWidth >= 32);
        Assert.True(bitmap.PixelHeight >= 32);
    }

    [Fact]
    public void LoadReturnsNullForUnknownShellItem()
    {
        Assert.Null(ShellIconLoader.Load("TileStart.Does.Not.Exist.7D9D3D48"));
    }
}

