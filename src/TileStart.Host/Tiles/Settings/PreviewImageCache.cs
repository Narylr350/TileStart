using System.IO;
using System.Security;
using System.Windows.Media;

namespace TileStart.Host.Tiles.Settings;

internal sealed class PreviewImageCache(Func<string, ImageSource?> loader)
{
    private string _path = string.Empty;
    private FileStamp _stamp;
    private ImageSource? _image;
    private bool _hasValue;

    public ImageSource? Load(string path)
    {
        path = path.Trim();
        var stamp = FileStamp.Read(path);
        if (_hasValue
            && _stamp == stamp
            && string.Equals(_path, path, StringComparison.OrdinalIgnoreCase))
        {
            return _image;
        }

        _path = path;
        _stamp = stamp;
        _image = loader(path);
        _hasValue = true;
        return _image;
    }

    private readonly record struct FileStamp(bool Exists, long Length, long LastWriteTimeUtcTicks)
    {
        public static FileStamp Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return default;
            }

            try
            {
                var file = new FileInfo(path);
                return file.Exists
                    ? new FileStamp(true, file.Length, file.LastWriteTimeUtc.Ticks)
                    : default;
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                                   or PathTooLongException or IOException
                                                   or UnauthorizedAccessException or SecurityException)
            {
                return default;
            }
        }
    }
}
