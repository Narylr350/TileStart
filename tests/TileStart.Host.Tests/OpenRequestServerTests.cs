using System.IO;
using System.IO.Pipes;
using System.Windows.Threading;
using TileStart.Host;

namespace TileStart.Host.Tests;

[Collection("Host pipe")]
public sealed class OpenRequestServerTests
{
    [Fact]
    public async Task SecondaryInstanceWaitsForPrimaryPipeStartup()
    {
        var pipeName = $"TileStart.Host.Tests.{Guid.NewGuid():N}";
        var expected = new HostRequest(HostRequestKind.Open);
        var serverTask = Task.Run<HostRequest?>(async () =>
        {
            await Task.Delay(350);
            await using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync();
            await using var message = new MemoryStream();
            var buffer = new byte[256];
            do
            {
                var count = await pipe.ReadAsync(buffer);
                if (count == 0)
                {
                    break;
                }

                await message.WriteAsync(buffer.AsMemory(0, count));
            } while (!pipe.IsMessageComplete);

            return HostRequest.TryDecode(message.ToArray(), out var request) ? request : null;
        });

        var delivered = await Task.Run(() => SingleInstanceGuard.NotifyPrimaryInstance(
            expected,
            pipeName,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(25)));

        Assert.True(delivered);
        Assert.Equal(expected, await serverTask.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Theory]
    [InlineData("OPEN", 1)]
    [InlineData("EXIT", 1)]
    [InlineData("NOPE", 0)]
    public async Task CommandsAreAcknowledgedWithoutWaitingForUiWork(string command, byte expectedResponse)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        HostRequest? queuedRequest = null;
        var pipeName = $"TileStart.Host.Tests.{Guid.NewGuid():N}";
        var server = new OpenRequestServer(request => queuedRequest = request, dispatcher, pipeName);
        server.Start();

        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
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
