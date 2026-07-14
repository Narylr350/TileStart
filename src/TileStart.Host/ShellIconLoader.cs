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

    public static ImageSource? Load(string displayName)
    {
        nint itemIdList = 0;
        try
        {
            if (SHParseDisplayName(displayName, 0, out itemIdList, 0, out _) != 0 || itemIdList == 0)
            {
                return null;
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
                return image;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHParseDisplayName(string name, nint bindingContext, out nint itemIdList, uint attributes, out uint attributesOut);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(nint path, uint fileAttributes, ref ShellFileInfo fileInfo, uint fileInfoSize, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint icon);
}
