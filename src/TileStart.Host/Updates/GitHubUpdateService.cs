using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TileStart.Host.Updates;

public enum UpdatePackageKind
{
    Installer,
    PortableArchive,
}

public sealed record GitHubReleaseAsset(string Name, Uri DownloadUri);

public sealed record GitHubReleaseInfo(
    string TagName,
    Version Version,
    Uri ReleasePage,
    GitHubReleaseAsset Installer,
    GitHubReleaseAsset PortableArchive,
    GitHubReleaseAsset Checksums);

public sealed record DownloadedUpdate(UpdatePackageKind Kind, string Path, Version Version);

public sealed class GitHubUpdateService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/Narylr350/TileStart/releases/latest";
    private const string InstallerAssetName = "TileStart-Setup-win-x64.exe";
    private const string PortableAssetName = "TileStart-portable-win-x64.zip";
    private const string ChecksumAssetName = "SHA256SUMS.txt";
    private const long MaximumPackageBytes = 200L * 1024 * 1024;
    private const int MaximumChecksumBytes = 64 * 1024;
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static Version CurrentVersion => NormalizeVersion(
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

    public async Task<GitHubReleaseInfo> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseRelease(json);
    }

    public async Task<DownloadedUpdate> DownloadAsync(
        GitHubReleaseInfo release,
        bool installedCopy,
        CancellationToken cancellationToken = default)
    {
        var package = SelectPackage(release, installedCopy);
        var checksumText = await DownloadTextAsync(release.Checksums.DownloadUri, MaximumChecksumBytes,
            cancellationToken);
        var expectedHash = ReadExpectedSha256(checksumText, package.Asset.Name);
        var directory = Path.Combine(Path.GetTempPath(), "TileStart", "updates", $"v{release.Version}");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, package.Asset.Name);
        await DownloadFileAsync(package.Asset.DownloadUri, destination, MaximumPackageBytes, cancellationToken);

        string actualHash;
        await using (var stream = File.OpenRead(destination))
        {
            actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        }
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destination);
            throw new InvalidDataException("下载文件的 SHA-256 校验失败，已删除该文件。");
        }

        return new DownloadedUpdate(package.Kind, destination, release.Version);
    }

    public static bool IsInstalledCopy(string? executablePath)
    {
        var directory = string.IsNullOrWhiteSpace(executablePath) ? null : Path.GetDirectoryName(executablePath);
        return directory is not null && File.Exists(Path.Combine(directory, "unins000.exe"));
    }

    internal static bool IsNewer(Version current, Version available) =>
        NormalizeVersion(available) > NormalizeVersion(current);

    internal static (UpdatePackageKind Kind, GitHubReleaseAsset Asset) SelectPackage(
        GitHubReleaseInfo release,
        bool installedCopy) =>
        installedCopy
            ? (UpdatePackageKind.Installer, release.Installer)
            : (UpdatePackageKind.PortableArchive, release.PortableArchive);

    internal static GitHubReleaseInfo ParseRelease(string json)
    {
        var release = JsonSerializer.Deserialize<ReleaseResponse>(json)
                      ?? throw new InvalidDataException("GitHub Release 响应为空。");
        var versionText = release.TagName?.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(versionText, out var version) || version.Build < 0)
        {
            throw new InvalidDataException($"无法识别 Release 版本：{release.TagName}");
        }

        var releasePage = RequireHttpsUri(release.HtmlUrl, "Release 页面");
        var assets = release.Assets ?? [];
        return new GitHubReleaseInfo(
            release.TagName!,
            NormalizeVersion(version),
            releasePage,
            RequireAsset(assets, InstallerAssetName),
            RequireAsset(assets, PortableAssetName),
            RequireAsset(assets, ChecksumAssetName));
    }

    internal static string ReadExpectedSha256(string manifest, string fileName)
    {
        foreach (var line in manifest.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(' ');
            if (separator <= 0)
            {
                continue;
            }

            var hash = line[..separator].Trim();
            var candidate = line[(separator + 1)..].Trim().TrimStart('*');
            if (candidate.Equals(fileName, StringComparison.OrdinalIgnoreCase)
                && hash.Length == 64
                && hash.All(Uri.IsHexDigit))
            {
                return hash;
            }
        }

        throw new InvalidDataException($"校验文件中缺少 {fileName} 的 SHA-256。");
    }

    private static GitHubReleaseAsset RequireAsset(IEnumerable<ReleaseAssetResponse> assets, string name)
    {
        var asset = assets.SingleOrDefault(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"GitHub Release 缺少资产：{name}");
        return new GitHubReleaseAsset(name, RequireHttpsUri(asset.BrowserDownloadUrl, name));
    }

    private static Uri RequireHttpsUri(string? value, string description)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{description}的下载地址无效。");
        }

        return uri;
    }

    private static async Task<string> DownloadTextAsync(Uri uri, int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 0 and var length && length > maximumBytes)
        {
            throw new InvalidDataException("校验文件超过允许大小。");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        await CopyWithLimitAsync(input, output, maximumBytes, cancellationToken);
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }

    private static async Task DownloadFileAsync(Uri uri, string destination, long maximumBytes,
        CancellationToken cancellationToken)
    {
        var temporaryPath = destination + ".download";
        try
        {
            using var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > 0 and var length && length > maximumBytes)
            {
                throw new InvalidDataException("更新包超过允许大小。");
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous);
            await CopyWithLimitAsync(input, output, maximumBytes, cancellationToken);
            File.Move(temporaryPath, destination, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task CopyWithLimitAsync(Stream input, Stream output, long maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return;
            }

            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException("下载内容超过允许大小。");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static Version NormalizeVersion(Version version) => new(
        Math.Max(0, version.Major),
        Math.Max(0, version.Minor),
        Math.Max(0, version.Build),
        Math.Max(0, version.Revision));

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TileStart-UpdateChecker");
        return client;
    }

    private sealed class ReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("assets")]
        public ReleaseAssetResponse[]? Assets { get; init; }
    }

    private sealed class ReleaseAssetResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}
