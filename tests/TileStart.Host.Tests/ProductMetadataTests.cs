using System.Diagnostics;

namespace TileStart.Host.Tests;

public sealed class ProductMetadataTests
{
    [Fact]
    public void HostPublishesTheFormalProductName()
    {
        var versionInfo = FileVersionInfo.GetVersionInfo(typeof(App).Assembly.Location);

        Assert.Equal("TileStart", versionInfo.FileDescription);
        Assert.Equal("TileStart", versionInfo.ProductName);
        Assert.Equal("Narylr350", versionInfo.CompanyName);
    }
}
