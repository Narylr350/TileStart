using System.Diagnostics;

namespace TileStart.Host;

public static class AppLauncher
{
    public static bool Launch(AppEntry app)
    {
        try
        {
            Process.Start(new ProcessStartInfo(app.LaunchTarget) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Unable to launch '{app.Name}' from '{app.LaunchTarget}': {exception}");
            return false;
        }
    }
}
