using System.Diagnostics;

namespace TileStart.Host;

public static class AppUninstaller
{
    public static bool CanUninstall(AppEntry app) =>
        !app.IsFolder && !string.IsNullOrWhiteSpace(app.LaunchTarget);

    public static bool CanUninstall(TileItem tile) => tile.TargetType == TileTargetType.Application;

    internal static string SettingsUri(string appUserModelId)
    {
        var separator = appUserModelId.IndexOf('!');
        return separator > 0
            ? $"ms-settings:appsfeatures-app?PFN={Uri.EscapeDataString(appUserModelId[..separator])}"
            : "ms-settings:appsfeatures";
    }

    public static bool Open(AppEntry app) => OpenSettings(SettingsUri(app.AppUserModelId));

    public static bool Open(TileItem tile, IReadOnlyList<AppEntry> apps)
    {
        var app = apps.FirstOrDefault(candidate =>
            candidate.LaunchTarget.Equals(tile.LaunchTarget, StringComparison.OrdinalIgnoreCase));
        return OpenSettings(SettingsUri(app?.AppUserModelId ?? string.Empty));
    }

    private static bool OpenSettings(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Open uninstall settings failed: uri={uri}, error={exception}");
            return false;
        }
    }
}
