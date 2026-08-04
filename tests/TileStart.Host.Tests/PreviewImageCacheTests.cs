using System.IO;
using System.Windows.Media;

namespace TileStart.Host.Tests;

public sealed class PreviewImageCacheTests
{
    [Fact]
    public void UnchangedFileReusesTheLoadedImage()
    {
        var path = CreateTemporaryFile("first");
        try
        {
            var loadCount = 0;
            var image = new DrawingImage();
            var cache = new PreviewImageCache(_ =>
            {
                loadCount++;
                return image;
            });

            Assert.Same(image, cache.Load(path));
            Assert.Same(image, cache.Load(path));
            Assert.Equal(1, loadCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FileContentChangeInvalidatesTheCachedImage()
    {
        var path = CreateTemporaryFile("first");
        try
        {
            var loadCount = 0;
            var cache = new PreviewImageCache(_ =>
            {
                loadCount++;
                return new DrawingImage();
            });

            var first = cache.Load(path);
            File.AppendAllText(path, "-changed");
            var second = cache.Load(path);

            Assert.NotSame(first, second);
            Assert.Equal(2, loadCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingFileAppearingInvalidatesTheCachedResult()
    {
        var path = Path.Combine(Path.GetTempPath(), $"TileStart-preview-{Guid.NewGuid():N}.png");
        var loadCount = 0;
        var cache = new PreviewImageCache(candidate =>
        {
            loadCount++;
            return File.Exists(candidate) ? new DrawingImage() : null;
        });

        try
        {
            Assert.Null(cache.Load(path));
            File.WriteAllText(path, "created");
            Assert.NotNull(cache.Load(path));
            Assert.Equal(2, loadCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTemporaryFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"TileStart-preview-{Guid.NewGuid():N}.png");
        File.WriteAllText(path, content);
        return path;
    }
}
