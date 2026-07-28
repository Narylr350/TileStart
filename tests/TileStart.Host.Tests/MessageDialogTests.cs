using System.IO;
using System.Xml.Linq;

namespace TileStart.Host.Tests;

public sealed class MessageDialogTests
{
    [Fact]
    public void UserPromptMaintenanceSurfaceUsesTheTileStartDialog()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestData", "HostSource");
        var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("MessageBox.Show", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MessageBoxButton", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MessageBoxImage", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MessageBoxResult", source, StringComparison.Ordinal);
        }

        Assert.Contains(files, file => Path.GetFileName(file) == "TileStartMessageDialog.xaml.cs");
    }

    [Fact]
    public void MessageDialogUsesTheSharedThemeAndClearPrimarySecondaryActions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", "TileStartMessageDialog.xaml");
        var document = XDocument.Load(path);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var window = document.Root!;

        Assert.Equal("{StaticResource TileStartDialogWindowStyle}", (string?)window.Attribute("Style"));
        Assert.Equal("None", (string?)window.Attribute("WindowStyle"));
        Assert.Equal("False", (string?)window.Attribute("ShowInTaskbar"));

        var buttons = document.Descendants(presentation + "Button").ToArray();
        var primary = Assert.Single(buttons, button => (string?)button.Attribute(x + "Name") == "PrimaryButton");
        var secondary = Assert.Single(buttons, button => (string?)button.Attribute(x + "Name") == "SecondaryButton");
        Assert.Equal("{StaticResource TileStartPrimaryButtonStyle}", (string?)primary.Attribute("Style"));
        Assert.Equal("True", (string?)primary.Attribute("IsDefault"));
        Assert.Equal("True", (string?)secondary.Attribute("IsCancel"));

        var titleLayout = Assert.Single(
            document.Descendants(presentation + "Grid"),
            element => (string?)element.Attribute(x + "Name") == "TitleLayout");
        var contentLayout = Assert.Single(
            document.Descendants(presentation + "Grid"),
            element => (string?)element.Attribute(x + "Name") == "ContentLayout");
        Assert.Equal("Center", (string?)titleLayout.Attribute("VerticalAlignment"));
        Assert.Equal("Center", (string?)contentLayout.Attribute("VerticalAlignment"));
    }

    [Fact]
    public void ApplicationFoldersDoNotExposeRegularAppContextActions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", "MainWindow.xaml");
        var source = File.ReadAllText(path);

        Assert.Contains("<DataTrigger Binding=\"{Binding IsFolder}\" Value=\"True\">", source,
            StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"ContextMenuService.IsEnabled\" Value=\"False\" />", source,
            StringComparison.Ordinal);
    }
}
