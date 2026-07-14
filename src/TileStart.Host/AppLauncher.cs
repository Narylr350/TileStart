using System.Diagnostics;

namespace TileStart.Host;

public static class AppLauncher
{
    public static bool Launch(AppEntry app) => Launch(app.Name, app.LaunchTarget);

    public static bool Launch(string name, string launchTarget)
    {
        try
        {
            Process.Start(new ProcessStartInfo(launchTarget) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Unable to launch '{name}' from '{launchTarget}': {exception}");
            return false;
        }
    }
}
