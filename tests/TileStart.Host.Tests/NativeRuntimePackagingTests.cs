using System.IO;
using System.Xml.Linq;

namespace TileStart.Host.Tests;

public sealed class NativeRuntimePackagingTests
{
    [Theory]
    [InlineData("TileStart.Injector.vcxproj")]
    [InlineData("TileStart.ShellHook.vcxproj")]
    [InlineData("TileStart.ShellProbe.vcxproj")]
    public void NativeProjectsStaticallyLinkTheVisualCppRuntime(string projectFile)
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "NativeProjects",
            projectFile));
        XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";
        var runtimeLibraries = document.Descendants(msbuild + "RuntimeLibrary").ToArray();

        Assert.Contains(runtimeLibraries, element =>
            (string?)element.Attribute("Condition") == "'$(Configuration)'=='Release'"
            && element.Value == "MultiThreaded");
        Assert.Contains(runtimeLibraries, element =>
            (string?)element.Attribute("Condition") == "'$(Configuration)'=='Debug'"
            && element.Value == "MultiThreadedDebug");
    }
}
