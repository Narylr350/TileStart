using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace TileStart.Host;

public static class CustomIconStore
{
    public const int MaximumSvgLength = 512 * 1024;
    private const int MaximumNetworkIconBytes = 5 * 1024 * 1024;
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static string IconDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TileStart",
        "icons");

    public static async Task<string> DownloadAsync(string sourceUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("请输入有效的 HTTP 或 HTTPS 图片地址。");
        }

        using var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaximumNetworkIconBytes)
        {
            throw new InvalidOperationException("图片不能超过 5 MB。");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (output.Length + read > MaximumNetworkIconBytes)
            {
                throw new InvalidOperationException("图片不能超过 5 MB。");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        Directory.CreateDirectory(IconDirectory);
        var extension = ResolveExtension(response.Content.Headers.ContentType?.MediaType, uri.AbsolutePath);
        var path = Path.Combine(IconDirectory, $"network-{Hash(sourceUrl)}{extension}");
        await File.WriteAllBytesAsync(path, output.ToArray(), cancellationToken);
        if (ShellIconLoader.LoadImage(path) is null)
        {
            File.Delete(path);
            throw new InvalidOperationException("下载内容不是受支持的图片或 SVG。");
        }

        return path;
    }

    public static string SaveSvg(string source)
    {
        ValidateSvg(source);
        Directory.CreateDirectory(IconDirectory);
        var path = Path.Combine(IconDirectory, $"svg-{Hash(source)}.svg");
        File.WriteAllText(path, source, new UTF8Encoding(false));
        if (SvgIconLoader.Load(path) is null)
        {
            File.Delete(path);
            throw new InvalidOperationException("SVG 无法渲染，请检查代码内容。");
        }

        return path;
    }

    internal static void ValidateSvg(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException("SVG 代码不能为空。");
        }

        if (source.Length > MaximumSvgLength)
        {
            throw new InvalidOperationException("SVG 代码不能超过 512 KB。");
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumSvgLength,
        };
        using var textReader = new StringReader(source);
        using var xmlReader = XmlReader.Create(textReader, settings);
        var document = new XmlDocument { XmlResolver = null };
        document.Load(xmlReader);
        if (document.DocumentElement is not { } root
            || !root.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("代码根元素必须是 SVG。");
        }

        foreach (XmlElement element in document.GetElementsByTagName("*"))
        {
            if (element.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase)
                || element.LocalName.Equals("foreignObject", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("SVG 不能包含脚本或嵌入式网页内容。");
            }

            foreach (XmlAttribute attribute in element.Attributes)
            {
                if (attribute.LocalName is not ("href" or "src"))
                {
                    continue;
                }

                var value = attribute.Value.Trim();
                if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("SVG 不能包含外部资源引用。");
                }
            }
        }
    }

    private static string ResolveExtension(string? mediaType, string path)
    {
        var mediaExtension = mediaType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/bmp" => ".bmp",
            "image/gif" => ".gif",
            "image/x-icon" or "image/vnd.microsoft.icon" => ".ico",
            "image/svg+xml" => ".svg",
            _ => string.Empty,
        };
        if (!string.IsNullOrEmpty(mediaExtension))
        {
            return mediaExtension;
        }

        var pathExtension = Path.GetExtension(path).ToLowerInvariant();
        return pathExtension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".ico" or ".svg"
            ? pathExtension
            : ".png";
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];
}
