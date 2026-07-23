using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
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

    [Fact]
    public void NormalizeShellIconCropsLargeTransparentPadding()
    {
        var pixels = new byte[64 * 64 * 4];
        for (var y = 24; y < 40; y++)
        {
            for (var x = 24; x < 40; x++)
            {
                pixels[(y * 64 + x) * 4 + 3] = byte.MaxValue;
            }
        }

        var source = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, pixels, 64 * 4);

        var normalized = Assert.IsAssignableFrom<BitmapSource>(IconImageNormalizer.NormalizeShellIcon(source));

        Assert.Equal(16, normalized.PixelWidth);
        Assert.Equal(16, normalized.PixelHeight);
        Assert.True(normalized.IsFrozen);
    }

    [Fact]
    public void NormalizeShellIconRejectsFullyTransparentBitmap()
    {
        var pixels = new byte[32 * 32 * 4];
        var source = BitmapSource.Create(32, 32, 96, 96, PixelFormats.Bgra32, null, pixels, 32 * 4);

        Assert.Null(IconImageNormalizer.NormalizeShellIcon(source));
    }

    [Fact]
    public async Task LoadUsesShortcutTargetIconWhenIconLocationIsEmpty()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"TileStart-ShortcutIcon-{Guid.NewGuid():N}");
        var shortcutPath = Path.Combine(directory, "Notepad.lnk");
        var targetPath = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        Directory.CreateDirectory(directory);
        try
        {
            await CreateShortcutAsync(shortcutPath, targetPath);

            var expected = Assert.IsAssignableFrom<BitmapSource>(ShellIconLoader.Load(targetPath));
            var actual = Assert.IsAssignableFrom<BitmapSource>(ShellIconLoader.Load(shortcutPath));

            Assert.Equal(GetPixels(expected), GetPixels(actual));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static Task CreateShortcutAsync(string shortcutPath, string targetPath)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            object? shell = null;
            object? shortcut = null;
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
                shell = Activator.CreateInstance(shellType);
                shortcut = ((dynamic)shell!).CreateShortcut(shortcutPath);
                dynamic shortcutApi = shortcut;
                shortcutApi.TargetPath = targetPath;
                shortcutApi.IconLocation = ",0";
                shortcutApi.Save();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
            finally
            {
                ReleaseComObject(shortcut);
                ReleaseComObject(shell);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static byte[] GetPixels(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}