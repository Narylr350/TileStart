using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace TileStart.Host.Utilities;

public static class DiagnosticLog
{
    private static readonly string DirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileStart");

    private static readonly string LogPath = Path.Combine(DirectoryPath, "TileStart.log");
    private static readonly ConcurrentQueue<string> PendingLines = new();
    private static readonly object FileSync = new();
    private static int _writerActive;

    public static void Write(string message)
    {
        PendingLines.Enqueue($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        StartWriter();
    }

    public static void Flush()
    {
        DrainQueue();
        SpinWait.SpinUntil(
            () => Volatile.Read(ref _writerActive) == 0 && PendingLines.IsEmpty,
            TimeSpan.FromSeconds(1));
    }

    private static void StartWriter()
    {
        if (Interlocked.CompareExchange(ref _writerActive, 1, 0) == 0)
        {
            ThreadPool.UnsafeQueueUserWorkItem(static _ => DrainQueue(), null);
        }
    }

    private static void DrainQueue()
    {
        while (true)
        {
            var batch = new StringBuilder();
            while (PendingLines.TryDequeue(out var line))
            {
                batch.Append(line);
            }

            if (batch.Length > 0)
            {
                try
                {
                    lock (FileSync)
                    {
                        Directory.CreateDirectory(DirectoryPath);
                        File.AppendAllText(LogPath, batch.ToString());
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            Interlocked.Exchange(ref _writerActive, 0);
            if (PendingLines.IsEmpty || Interlocked.CompareExchange(ref _writerActive, 1, 0) != 0)
            {
                return;
            }
        }
    }
}