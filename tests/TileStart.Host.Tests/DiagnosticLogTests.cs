using System.IO;
using System.Text;
using TileStart.Host.Utilities;

namespace TileStart.Host.Tests;

public sealed class DiagnosticLogTests
{
    [Fact]
    public void RotatesCurrentLogAndKeepsOnlyOnePreviousGeneration()
    {
        var root = Path.Combine(Path.GetTempPath(), $"TileStart-Log-{Guid.NewGuid():N}");
        var logPath = Path.Combine(root, "TileStart.log");
        var previousPath = Path.Combine(root, "TileStart.previous.log");

        try
        {
            DiagnosticLog.AppendBatchToFile(logPath, previousPath, "1234567890", 12);
            DiagnosticLog.AppendBatchToFile(logPath, previousPath, "abcd", 12);

            Assert.Equal("1234567890", File.ReadAllText(previousPath));
            Assert.Equal("abcd", File.ReadAllText(logPath));

            DiagnosticLog.AppendBatchToFile(logPath, previousPath, "efghijklm", 12);

            Assert.Equal("abcd", File.ReadAllText(previousPath));
            Assert.Equal("efghijklm", File.ReadAllText(logPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void KeepsUtf8TailWhenSingleBatchExceedsLimit()
    {
        var root = Path.Combine(Path.GetTempPath(), $"TileStart-Log-{Guid.NewGuid():N}");
        var logPath = Path.Combine(root, "TileStart.log");
        var previousPath = Path.Combine(root, "TileStart.previous.log");

        try
        {
            DiagnosticLog.AppendBatchToFile(logPath, previousPath, "旧内容-新内容", 10);

            var bytes = File.ReadAllBytes(logPath);
            Assert.True(bytes.Length <= 10);
            Assert.DoesNotContain('\uFFFD', Encoding.UTF8.GetString(bytes));
            Assert.EndsWith("内容", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void TrimsAlreadyOversizedCurrentLogDuringFirstRotation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"TileStart-Log-{Guid.NewGuid():N}");
        var logPath = Path.Combine(root, "TileStart.log");
        var previousPath = Path.Combine(root, "TileStart.previous.log");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(logPath, "0123456789-旧内容");

            DiagnosticLog.AppendBatchToFile(logPath, previousPath, "new", 10);

            var previousBytes = File.ReadAllBytes(previousPath);
            Assert.True(previousBytes.Length <= 10);
            Assert.DoesNotContain('\uFFFD', Encoding.UTF8.GetString(previousBytes));
            Assert.EndsWith("内容", Encoding.UTF8.GetString(previousBytes), StringComparison.Ordinal);
            Assert.Equal("new", File.ReadAllText(logPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void TrimsAlreadyOversizedPreviousLogWithoutWaitingForAnotherRotation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"TileStart-Log-{Guid.NewGuid():N}");
        var logPath = Path.Combine(root, "TileStart.log");
        var previousPath = Path.Combine(root, "TileStart.previous.log");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(logPath, "old");
            File.WriteAllText(previousPath, "0123456789-旧内容");

            DiagnosticLog.AppendBatchToFile(logPath, previousPath, "new", 10);

            var previousBytes = File.ReadAllBytes(previousPath);
            Assert.True(previousBytes.Length <= 10);
            Assert.DoesNotContain('\uFFFD', Encoding.UTF8.GetString(previousBytes));
            Assert.EndsWith("内容", Encoding.UTF8.GetString(previousBytes), StringComparison.Ordinal);
            Assert.Equal("oldnew", File.ReadAllText(logPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}