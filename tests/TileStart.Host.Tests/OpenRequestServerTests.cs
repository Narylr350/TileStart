using System.IO.Pipes;
using System.Windows.Threading;
using TileStart.Host;

namespace TileStart.Host.Tests;

[Collection("Host pipe")]
public sealed class OpenRequestServerTests
{
    [Theory]
    [InlineData("OPEN", 1)]
    [InlineData("EXIT", 1)]
    [InlineData("NOPE", 0)]
    public async Task CommandsAreAcknowledgedWithoutWaitingForUiWork(string command, byte expectedResponse)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        HostRequest? queuedRequest = null;
        var server = new OpenRequestServer(request => queuedRequest = request, dispatcher);
        server.Start();

        try
        {
            using var client = new NamedPipeClientStream(".", "TileStart.Host", PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(timeout.Token);
            await client.WriteAsync(System.Text.Encoding.ASCII.GetBytes(command), timeout.Token);
            await client.FlushAsync(timeout.Token);

            var response = new byte[1];
            Assert.Equal(1, await client.ReadAsync(response, timeout.Token));
            Assert.Equal(expectedResponse, response[0]);
            Assert.Null(queuedRequest);
        }
        finally
        {
            await server.StopAsync();
        }
    }
}

[CollectionDefinition("Host pipe", DisableParallelization = true)]
public sealed class HostPipeCollection;
