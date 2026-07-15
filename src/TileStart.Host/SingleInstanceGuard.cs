using System.IO;
using System.IO.Pipes;

namespace TileStart.Host;

public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Local\\TileStart.Host";
    private const string PipeName = "TileStart.Host";
    private static readonly byte[] OpenCommand = "OPEN"u8.ToArray();
    private static readonly byte[] ExitCommand = "EXIT"u8.ToArray();
    private readonly Mutex _mutex;

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(true, MutexName, out var isPrimaryInstance);
        IsPrimaryInstance = isPrimaryInstance;
    }

    public bool IsPrimaryInstance { get; }

    public static void NotifyPrimaryInstance(bool requestShutdown = false)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            pipe.Connect(250);
            pipe.Write(requestShutdown ? ExitCommand : OpenCommand);
            pipe.Flush();
        }
        catch (IOException)
        {
        }
        catch (TimeoutException)
        {
        }
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
