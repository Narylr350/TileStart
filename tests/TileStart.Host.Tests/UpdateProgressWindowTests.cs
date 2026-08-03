using System.IO;
using System.Xml.Linq;

namespace TileStart.Host.Tests;

public sealed class UpdateProgressWindowTests
{
    [Fact]
    public void UsesThemeProgressAndCancelableAction()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", "UpdateProgressWindow.xaml");
        var document = XDocument.Load(path);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var window = document.Root!;

        Assert.Equal("{StaticResource TileStartDialogWindowStyle}", (string?)window.Attribute("Style"));
        Assert.Equal("None", (string?)window.Attribute("WindowStyle"));
        Assert.Equal("False", (string?)window.Attribute("ShowInTaskbar"));
        Assert.Equal("False", window.Attributes()
            .Single(attribute => attribute.Name.LocalName.EndsWith("IsCloseAnimationEnabled", StringComparison.Ordinal))
            .Value);

        var progress = Assert.Single(document.Descendants(presentation + "ProgressBar"));
        Assert.Equal("DownloadProgress", (string?)progress.Attribute(x + "Name"));

        var cancel = Assert.Single(
            document.Descendants(presentation + "Button"),
            button => (string?)button.Attribute(x + "Name") == "CancelButton");
        Assert.Equal("{StaticResource TileStartButtonStyle}", (string?)cancel.Attribute("Style"));
        Assert.Equal("取消", (string?)cancel.Attribute("Content"));
    }
}
