using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TileStart.Host;

public static class ShellIconLoader
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiPidl = 0x000000008;
    private const uint SiigbfBiggerSizeOk = 0x00000001;
    private const uint SiigbfIconOnly = 0x00000004;
    private static readonly Guid ShellItemImageFactoryId = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");
    private static readonly string[] ImageExtensions = [".bmp", ".gif", ".ico", ".jpeg", ".jpg", ".png", ".svg"];

    public static ImageSource? Load(string displayName)
    {
        var loadedImage = LoadImage(displayName);
        if (loadedImage is not null)
        {
            return loadedImage;
        }

        var shortcutTarget = ResolveShortcutTargetWithoutIcon(displayName);
        if (!string.IsNullOrWhiteSpace(shortcutTarget)
            && !shortcutTarget.Equals(displayName, StringComparison.OrdinalIgnoreCase))
        {
            var targetIcon = Load(shortcutTarget);
            if (targetIcon is not null)
            {
                return targetIcon;
            }
        }

        nint itemIdList = 0;
        try
        {
            if (SHParseDisplayName(displayName, 0, out itemIdList, 0, out _) != 0 || itemIdList == 0)
            {
                return null;
            }

            var shellItemImage = LoadShellItemImage(itemIdList, 64);
            if (shellItemImage is not null)
            {
                return IconImageNormalizer.NormalizeShellIcon(shellItemImage);
            }

            var fileInfo = new ShellFileInfo();
            if (SHGetFileInfo(itemIdList, 0, ref fileInfo, (uint)Marshal.SizeOf<ShellFileInfo>(), ShgfiPidl | ShgfiIcon | ShgfiLargeIcon) == 0 || fileInfo.Icon == 0)
            {
                return null;
            }

            try
            {
                var image = Imaging.CreateBitmapSourceFromHIcon(fileInfo.Icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                image.Freeze();
                return IconImageNormalizer.NormalizeShellIcon(image);
            }
            finally
            {
                DestroyIcon(fileInfo.Icon);
            }
        }
        finally
        {
            if (itemIdList != 0)
            {
                Marshal.FreeCoTaskMem(itemIdList);
            }
        }
    }

    public static ImageSource? LoadImage(string path)
    {
        var extension = Path.GetExtension(path);
        if (!File.Exists(path) || !ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return SvgIconLoader.Load(path);
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 64;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? ResolveShortcutTargetWithoutIcon(string displayName)
    {
        if (!File.Exists(displayName)
            || !Path.GetExtension(displayName).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        object? shell = null;
        object? shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            shell = shellType is null ? null : Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return null;
            }

            shortcut = ((dynamic)shell).CreateShortcut(displayName);
            dynamic shortcutApi = shortcut;
            var iconLocation = shortcutApi.IconLocation as string ?? string.Empty;
            var separator = iconLocation.LastIndexOf(',');
            var iconPath = separator >= 0 ? iconLocation[..separator] : iconLocation;
            if (!string.IsNullOrWhiteSpace(iconPath.Trim().Trim('"')))
            {
                return null;
            }

            var targetPath = shortcutApi.TargetPath as string;
            return !string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath)
                ? targetPath
                : null;
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Shortcut icon target resolution failed: shortcut={displayName}, error={exception.Message}");
            return null;
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private static ImageSource? LoadShellItemImage(nint itemIdList, int size)
    {
        IShellItemImageFactory? factory = null;
        nint bitmap = 0;
        try
        {
            var interfaceId = ShellItemImageFactoryId;
            if (SHCreateItemFromIDList(itemIdList, ref interfaceId, out factory) != 0 || factory is null)
            {
                return null;
            }

            if (factory.GetImage(new NativeSize(size, size), SiigbfBiggerSizeOk | SiigbfIconOnly, out bitmap) != 0 || bitmap == 0)
            {
                return null;
            }

            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            if (bitmap != 0)
            {
                DeleteObject(bitmap);
            }

            if (factory is not null)
            {
                Marshal.FinalReleaseComObject(factory);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize(int width, int height)
    {
        public readonly int Width = width;
        public readonly int Height = height;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, uint flags, out nint bitmap);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHCreateItemFromIDList(nint itemIdList, ref Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? factory);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHParseDisplayName(string name, nint bindingContext, out nint itemIdList, uint attributes, out uint attributesOut);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(nint path, uint fileAttributes, ref ShellFileInfo fileInfo, uint fileInfoSize, uint flags);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint value);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint icon);
}
