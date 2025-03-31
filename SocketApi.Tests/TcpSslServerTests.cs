using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MessagePack;
using Microsoft.Extensions.Hosting;

namespace SocketApi.Tests;

public class TcpSslServerTests
{
    private const int Port = 7110;
    private const string CertPath = "Output.pfx";
    private const string CertPassword = "Password.1";
    private const int Backlog = 110;
    private const long MaxRequestLength = 1024 * 1024;
    private const long MaxResponseLength = 1024 * 1024;

    public TcpSslServerTests()
    {
        var host = Host.CreateDefaultBuilder().AddSocketApi(Port, new X509Certificate2(CertPath, CertPassword),
                MaxRequestLength, MaxResponseLength, Backlog)
            .Build();
        
        Router.Operation("login", request => !string.IsNullOrWhiteSpace(request?.Content) && string.Equals("username:password", request.Content) ? Task.FromResult(OperationResult.Ok("Logged in!")) : Task.FromResult(OperationResult.Ko("Missing credentials")));

        Router.Operation("submit", request => Task.FromResult(request != null ? OperationResult.Ok($"Data submitted: {request.Content}") : OperationResult.Ko("No data provided")));

        Router.Operation("performance", async request =>
        {
            await Task.Delay(50); // Simulate processing delay
            return OperationResult.Ok($"Performance test completed with data {request?.Content}");
        });
        
        Router.Operation("max-response-length", _ =>
        {
            var sb = new StringBuilder();

            const char character = 'A';
            
            for (var i = 0; i < MaxResponseLength; i++)
            {
                sb.Append(character);
            }

            return Task.FromResult(OperationResult.Ok(sb.ToString()));
        });

        Task.Run(() => host.RunAsync());
    }

    [Fact]
    public async Task TestGetRoute_WithParameters_ReturnsExpectedResponse()
    {
        // Arrange
        var request = "login|username:password";
        var expectedResponse = "Logged in!";

        // Act
        var response = await SendRequestAndReceiveResponse(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Content);
        Assert.Equal(expectedResponse, response.Content.ToString());
    }

    [Fact]
    public async Task TestRoute_WithRequest_ReturnsExpectedResponse()
    {
        // Arrange
        var request = "submit|Hello World";
        var expectedResponse = "Data submitted: Hello World";

        // Act
        var response = await SendRequestAndReceiveResponse(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Content);
        Assert.Equal(expectedResponse, response.Content.ToString());
    }

    [Fact]
    public async Task TestMissingCredentials_ReturnsErrorMessage()
    {
        // Arrange
        var request = "login";
        var expectedResponse = "Missing credentials";

        // Act
        var response = await SendRequestAndReceiveResponse(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.Content);
        Assert.Equal(expectedResponse, response.Content.ToString());
    }
    
    [Fact]
    public async Task TestMaxRequestLength_ReturnsErrorMessage()
    {
        // Arrange
        var sb = new StringBuilder();
        const char character = 'A';
        for (var i = 0; i < MaxRequestLength; i++)
        {
            sb.Append(character);
        }
        sb.Append('b');
        
        var expectedResponse = $"Max request length ({MaxRequestLength}) exceeded.";

        // Act
        var response = await SendRequestAndReceiveResponse(sb.ToString());

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.Content);
        Assert.Equal(expectedResponse, response.Content.ToString());
    }
    
    [Fact]
    public async Task TestInvalid_Request_ReturnsErrorMessage()
    {
        // Arrange
        var sb = new StringBuilder();
        const char character = 'A';
        for (var i = 0; i < MaxRequestLength - 300; i++)
        {
            sb.Append(character);
        }
        sb.Append('b');
        
        // Act
        var response = await SendRequestAndReceiveResponse(sb.ToString());

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.Content);
    }
    
    [Fact]
    public async Task TestMaxResponseLength_ReturnsErrorMessage()
    {
        // Arrange
        var request = "max-response-length";
        var expectedResponse = $"Max response length ({MaxResponseLength}) exceeded.";

        // Act
        var response = await SendRequestAndReceiveResponse(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.Content);
        Assert.Equal(expectedResponse, response.Content.ToString());
    }
    
    [Fact]
    public async Task Test_RequestNotFound_ReturnsErrorMessage()
    {
        // Arrange
        var request = "unknown";
        var expectedResponse = $"Operation '{request}' not found.";

        // Act
        var response = await SendRequestAndReceiveResponse(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.Content);
        Assert.Equal(expectedResponse, response.Content.ToString());
    }

    /// <summary>
    /// This test evaluates if multiple clients can communicate concurrently
    /// </summary>
    /// <param name="concurrentClients"></param>
    [Theory]
    [Trait("Category", "Performance")]
    [InlineData(10)]
    public async Task TestConcurrentRequests_MultipleConnections_Success(int concurrentClients)
    {
        var tasks = new List<Task<OperationResult>>();

        // Act
        for (var i = 0; i < concurrentClients; i++)
        {
            const string request = "login|username:password";
            tasks.Add(SendRequestAndReceiveResponse(request));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Content);
            Assert.Equal("Logged in!", response.Content.ToString());
        }
    }

    private async Task<OperationResult> SendRequestAndReceiveResponse(string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", Port);

        await using var networkStream = client.GetStream();
        await using var sslStream = new SslStream(networkStream, false, (_, _, _, _) => true);

        await sslStream.AuthenticateAsClientAsync("localhost");

        var requestBytes = MessagePackSerializer.Serialize(request);
        await sslStream.WriteAsync(requestBytes);

        var buffer = new byte[4096];
        _ = await sslStream.ReadAsync(buffer);

        return MessagePackSerializer.Deserialize<OperationResult>(buffer);
    }

    /// <summary>
    /// This test evaluates the server performance under load
    /// </summary>
    /// <param name="clientCount">How many clients must communicate</param>
    /// <param name="minMsAcceptableResponse">The minimum acceptable response time in ms</param>
    /// <param name="maxMsAcceptableResponse">The maximum acceptable response time in ms</param>
    [Theory]
    [Trait("Category", "Performance")]
    [InlineData(1000, 0, 200)] // 100 clients, 0 ms min, 200 ms max response time
    public async Task TestServerPerformanceUnderLoad(int clientCount, int minMsAcceptableResponse,
        int maxMsAcceptableResponse)
    {
        //Arrange
        var tasks = new List<Task<Tuple<OperationResult, long>>>(); // To store response time tasks

        for (var i = 0; i < clientCount; i++)
        {
            tasks.Add(MeasureResponseTimeAsync($"performance|Client Number: {i}"));
        }

        //Act
        var responses = await Task.WhenAll(tasks);

        //Assert
        var index = 0;
        foreach (var response in responses)
        {
            var data = $"Client Number: {index}";
            Assert.NotNull(response);
            Assert.NotNull(response.Item1);
            Assert.NotNull(response.Item1.Content);
            Assert.Equal($"Performance test completed with data {data}", response.Item1.Content.ToString());
            Assert.InRange(response.Item2, minMsAcceptableResponse, maxMsAcceptableResponse);
            index++;
        }
    }

    private async Task<Tuple<OperationResult, long>> MeasureResponseTimeAsync(string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", Port);

        await using var networkStream = client.GetStream();
        await using var sslStream = new SslStream(networkStream, false, (_, _, _, _) => true);

        await sslStream.AuthenticateAsClientAsync("localhost");

        var requestBytes = MessagePackSerializer.Serialize(request);
        var stopwatch = Stopwatch.StartNew();

        await sslStream.WriteAsync(requestBytes);

        var buffer = new byte[4096];
        _ = await sslStream.ReadAsync(buffer);

        stopwatch.Stop();

        var response = MessagePackSerializer.Deserialize<OperationResult>(buffer);

        return new Tuple<OperationResult, long>(response, stopwatch.ElapsedMilliseconds);
    }
}