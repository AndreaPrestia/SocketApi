using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;

namespace SocketApi.Server.Tests;

public class TcpSslServerTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private const string Server = "localhost";
    private const int Port = 7110;
    private const string CertPath = "Output.pfx";
    private const string CertPassword = "Password.1";
    private const int Backlog = 110;

    public TcpSslServerTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var host = Host.CreateDefaultBuilder().AddSocketApi(CertPath, CertPassword, Port, Backlog)
            .Build();
        Task.Run(() => host.RunAsync());
    }

    /// <summary>
    /// This test evaluates if multiple clients can communicate concurrently
    /// </summary>
    /// <param name="clientCount"></param>
    [Theory]
    [Trait("Category", "Performance")]
    [InlineData(100)]
    public async Task MultipleClientsCanConnectAndCommunicateConcurrently(int clientCount)
    {
        var clients = new TcpSslClient[Port];

        for (var i = 0; i < clientCount; i++)
        {
            clients[i] = new TcpSslClient(Server, Port);
        }

        var tasks = new Task[clientCount];

        for (var i = 0; i < clientCount; i++)
        {
            var clientMessage = $"Hello from client {i}";
            var i1 = i;
            tasks[i] = Task.Run(async () =>
            {
                var response = await clients[i1].SendRequestAsync(clientMessage);
                Assert.Equal("RECEIVED", response);
            });
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// This test evaluates the server performance under load
    /// </summary>
    /// <param name="clientCount">How many clients must communicate</param>
    /// <param name="minMsAcceptableResponse">The minimum acceptable response time in ms</param>
    /// <param name="maxMsAcceptableResponse">The maximum acceptable response time in ms</param>
    [Theory]
    [Trait("Category", "Performance")]
    [InlineData(100, 0, 200)]
    public async Task TestServerPerformanceUnderLoad(int clientCount, int minMsAcceptableResponse, int maxMsAcceptableResponse)
    {
        var clients = new TcpSslClient[clientCount];

        for (var i = 0; i < clientCount; i++)
        {
            clients[i] = new TcpSslClient(Server, Port);
        }

        var tasks = new Task[clientCount];
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < clientCount; i++)
        {
            var i1 = i;
            tasks[i] = Task.Run(async () =>
            {
                var response = await clients[i1].SendRequestAsync("Load test message");
                Assert.Equal("RECEIVED", response);
            });
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        var averageTimeMs = stopwatch.Elapsed.TotalMilliseconds / clientCount;
        _testOutputHelper.WriteLine($"Average response time per client: {averageTimeMs} ms");

        // Assert that the average time is within an acceptable threshold
        Assert.InRange(averageTimeMs, minMsAcceptableResponse, maxMsAcceptableResponse);
    }
}