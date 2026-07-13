using System.IO;
using System.IO.Pipes;
using System.Windows.Threading;

namespace TileStart.Host;

public sealed class OpenRequestServer
{
    private const string PipeName = "TileStart.Host";
    private static readonly byte[] OpenCommand = "OPEN"u8.ToArray();
    private readonly MainWindow _window;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenTask;

    public OpenRequestServer(MainWindow window, Dispatcher dispatcher)
    {
        _window = window;
        _dispatcher = dispatcher;
    }

    public void Start()
    {
        _listenTask = ListenAsync(_cancellation.Token);
    }

    public async Task StopAsync()
    {
        _cancellation.Cancel();
        if (_listenTask is not null)
        {
            try
            {
                await _listenTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cancellation.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(PipeName,
                                                             PipeDirection.InOut,
                                                             1,
                                                             PipeTransmissionMode.Message,
                                                             PipeOptions.Asynchronous);
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
                var command = new byte[OpenCommand.Length];
                var received = 0;
                while (received < command.Length)
                {
                    var count = await pipe.ReadAsync(command.AsMemory(received), cancellationToken);
                    if (count == 0)
                    {
                        break;
                    }

                    received += count;
                }

                var accepted = received == OpenCommand.Length && command.SequenceEqual(OpenCommand);
                if (accepted)
                {
                    await _dispatcher.InvokeAsync(_window.ShowFromShell).Task;
                }

                await pipe.WriteAsync(new[] { accepted ? (byte)1 : (byte)0 }, cancellationToken);
                await pipe.FlushAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
            }
        }
    }
}
