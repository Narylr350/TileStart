using System.IO;
using System.Xml.Linq;

namespace TileStart.Host.Tests;

public sealed class AnimatedImageLayeringTests
{
    [Theory]
    [InlineData("MainWindow.xaml")]
    [InlineData("TileSettingsWindow.xaml")]
    public void AnimatedAndStaticSourcesUseSeparateImageElements(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", fileName);
        var document = XDocument.Load(path);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var conflictingImages = document
            .Descendants(presentation + "Image")
            .Where(image => image.Attributes().Any(attribute => attribute.Name.LocalName == "AnimatedSource")
                            && image.Elements(presentation + "Image.Source").Any())
            .ToArray();

        Assert.Empty(conflictingImages);
    }
}
