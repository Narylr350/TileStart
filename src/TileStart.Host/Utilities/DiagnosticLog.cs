using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace TileStart.Host.Utilities;

public static class DiagnosticLog
{
    internal const long MaximumLogBytes = 4 * 1024 * 1024;

    public static string DirectoryPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileStart");

    public static string LogPath { get; } = Path.Combine(DirectoryPath, "TileStart.log");
    internal static string PreviousLogPath { get; } = Path.Combine(DirectoryPath, "TileStart.previous.log");
    private static readonly ConcurrentQueue<string> PendingLines = new();
    private static readonly object FileSync = new();
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
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
                        AppendBatchToFile(LogPath, PreviousLogPath, batch.ToString(), MaximumLogBytes);
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

    internal static void AppendBatchToFile(
        string logPath,
        string previousLogPath,
        string content,
        long maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(previousLogPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        TrimOversizedFile(previousLogPath, maximumBytes);
        var boundedContent = KeepUtf8Tail(content, maximumBytes);
        var contentBytes = Utf8WithoutBom.GetByteCount(boundedContent);
        if (File.Exists(logPath) && new FileInfo(logPath).Length + contentBytes > maximumBytes)
        {
            // 只保留一代旧日志，既限制磁盘占用，也保留刚越过边界前的故障上下文。
            RotateCurrentLog(logPath, previousLogPath, maximumBytes);
        }

        File.AppendAllText(logPath, boundedContent, Utf8WithoutBom);
    }

    private static void RotateCurrentLog(string logPath, string previousLogPath, long maximumBytes)
    {
        var length = new FileInfo(logPath).Length;
        if (length <= maximumBytes)
        {
            File.Move(logPath, previousLogPath, overwrite: true);
            return;
        }

        WriteFileTail(logPath, previousLogPath, maximumBytes);
        File.Delete(logPath);
    }

    private static void TrimOversizedFile(string path, long maximumBytes)
    {
        if (!File.Exists(path) || new FileInfo(path).Length <= maximumBytes)
        {
            return;
        }

        var temporaryPath = path + ".trim";
        try
        {
            WriteFileTail(path, temporaryPath, maximumBytes);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void WriteFileTail(string sourcePath, string destinationPath, long maximumBytes)
    {
        var maximumTailBytes = checked((int)maximumBytes);
        var tail = new byte[maximumTailBytes];
        using (var source = new FileStream(
                   sourcePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            source.Seek(-maximumTailBytes, SeekOrigin.End);
            source.ReadExactly(tail);
        }

        var start = 0;
        while (start < tail.Length && (tail[start] & 0xC0) == 0x80)
        {
            start++;
        }

        File.WriteAllBytes(destinationPath, tail.AsSpan(start).ToArray());
    }

    private static string KeepUtf8Tail(string content, long maximumBytes)
    {
        var bytes = Utf8WithoutBom.GetBytes(content);
        if (bytes.LongLength <= maximumBytes)
        {
            return content;
        }

        var start = bytes.Length - checked((int)maximumBytes);
        while (start < bytes.Length && (bytes[start] & 0xC0) == 0x80)
        {
            start++;
        }

        return Utf8WithoutBom.GetString(bytes, start, bytes.Length - start);
    }
}