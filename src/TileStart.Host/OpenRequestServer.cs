using System.IO;
using System.IO.Pipes;
using System.Windows.Threading;

namespace TileStart.Host;

public sealed class OpenRequestServer
{
    private const string PipeName = "TileStart.Host";
    private readonly Action<HostRequest> _handleRequest;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenTask;

    public OpenRequestServer(Action<HostRequest> handleRequest, Dispatcher dispatcher)
    {
        _handleRequest = handleRequest;
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
                await using var message = new MemoryStream();
                var buffer = new byte[4096];
                do
                {
                    var count = await pipe.ReadAsync(buffer, cancellationToken);
                    if (count == 0)
                    {
                        break;
                    }

                    await message.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
                while (!pipe.IsMessageComplete);

                var accepted = HostRequest.TryDecode(message.ToArray(), out var request);
                if (accepted)
                {
                    _ = _dispatcher.BeginInvoke(() => _handleRequest(request));
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
