using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class CustomIconStoreTests
{
    [Fact]
    public void ValidSvgRootIsAccepted()
    {
        CustomIconStore.ValidateSvg("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M0 0h24v24H0z\"/></svg>");
    }

    [Fact]
    public void ValidSvgCanRenderAsFrozenWpfImage()
    {
        var image = SvgIconLoader.LoadText(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#0078D7\" d=\"M2 2h20v20H2z\"/></svg>");

        Assert.NotNull(image);
        Assert.True(image.IsFrozen);
    }

    [Theory]
    [InlineData("<html></html>")]
    [InlineData("<svg><script>alert(1)</script></svg>")]
    [InlineData("<svg><image href=\"https://example.com/icon.png\"/></svg>")]
    [InlineData("<!DOCTYPE svg><svg></svg>")]
    public void UnsafeOrInvalidSvgIsRejected(string source)
    {
        Assert.ThrowsAny<Exception>(() => CustomIconStore.ValidateSvg(source));
    }
}
