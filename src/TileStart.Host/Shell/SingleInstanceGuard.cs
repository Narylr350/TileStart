using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace TileStart.Host.Shell;

public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Local\\TileStart.Host";
    private const string PipeName = "TileStart.Host";
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan NotificationRetryDelay = TimeSpan.FromMilliseconds(50);
    private readonly Mutex _mutex;

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(true, MutexName, out var isPrimaryInstance);
        IsPrimaryInstance = isPrimaryInstance;
    }

    public bool IsPrimaryInstance { get; }

    public static bool NotifyPrimaryInstance(HostRequest request) =>
        NotifyPrimaryInstance(request, PipeName, NotificationTimeout, NotificationRetryDelay);

    internal static bool NotifyPrimaryInstance(
        HostRequest request,
        string pipeName,
        TimeSpan timeout,
        TimeSpan retryDelay)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < timeout)
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            try
            {
                var remaining = timeout - elapsed.Elapsed;
                var connectTimeout = (int)Math.Clamp(remaining.TotalMilliseconds, 1, 250);
                pipe.Connect(connectTimeout);
                pipe.Write(request.Encode());
                pipe.Flush();
                return true;
            }
            catch (TimeoutException)
            {
            }
            catch (IOException) when (!pipe.IsConnected)
            {
            }
            catch (IOException)
            {
                return false;
            }

            var delay = timeout - elapsed.Elapsed < retryDelay
                ? timeout - elapsed.Elapsed
                : retryDelay;
            if (delay > TimeSpan.Zero)
            {
                Thread.Sleep(delay);
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (IsPrimaryInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}