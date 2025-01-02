﻿using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;

namespace SocketApi.Server.Tests;

public class TcpSslServerTests
{
    private const int Port = 7110;
    private const string CertPath = "Output.pfx";
    private const string CertPassword = "Password.1";
    private const int Backlog = 110;

    public TcpSslServerTests()
    {
        var host = Host.CreateDefaultBuilder().AddSocketApi(CertPath, CertPassword, Port, Backlog)
            .Build();
        Router.RegisterRoute("/login", async (parameters, _, writeResponse) =>
        {
            if (parameters.TryGetValue("username", out var username) &&
                parameters.TryGetValue("password", out var _))
            {
                await writeResponse($"Logged in as {username}");
            }
            else
            {
                await writeResponse("Missing credentials");
            }
        });

        Router.RegisterRoute("/submit", async (_, data, writeResponse) =>
        {
            if (!string.IsNullOrEmpty(data))
            {
                await writeResponse($"Data submitted: {data}");
            }
            else
            {
                await writeResponse("No data provided");
            }
        });

        Router.RegisterRoute("/performance", async (_, data, writeResponse) =>
        {
            await Task.Delay(50); // Simulate processing delay
            await writeResponse($"Performance test completed with data {data}");
        });

        Task.Run(() => host.RunAsync());
    }

    [Fact]
    public async Task TestGetRoute_WithParameters_ReturnsExpectedResponse()
    {
        // Arrange
        var request = "/login?username=TestUser&password=1234\r\n";
        var expectedResponse = "Logged in as TestUser";

        // Act
        var response = await SendRequestAndReceiveResponse(request);

        // Assert
        Assert.Equal(expectedResponse, response);
    }

    [Fact]
    public async Task TestPostRoute_WithBody_ReturnsExpectedResponse()
    {
        // Arrange
        var request = "/submit\r\nHello World";
        var expectedResponse = "Data submitted: Hello World";

        // Act
        var response = await SendRequestAndReceiveResponse(request);

        // Assert
        Assert.Equal(expectedResponse, response);
    }

    [Fact]
    public async Task TestMissingCredentials_ReturnsErrorMessage()
    {
        // Arrange
        var request = "/login\r\n";
        var expectedResponse = "Missing credentials";

        // Act
        var response = await SendRequestAndReceiveResponse(request);

        // Assert
        Assert.Equal(expectedResponse, response);
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
        var tasks = new List<Task<string>>();

        // Act
        for (var i = 0; i < concurrentClients; i++)
        {
            const string request = "/login?username=concurrent&password=test\r\n";
            tasks.Add(SendRequestAndReceiveResponse(request));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            Assert.Equal("Logged in as concurrent", response);
        }
    }

    private async Task<string> SendRequestAndReceiveResponse(string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", Port);

        await using var networkStream = client.GetStream();
        await using var sslStream = new SslStream(networkStream, false, (_, _, _, _) => true);

        await sslStream.AuthenticateAsClientAsync("localhost");

        var requestBytes = Encoding.UTF8.GetBytes(request);
        await sslStream.WriteAsync(requestBytes);

        var buffer = new byte[4096];
        var bytesRead = await sslStream.ReadAsync(buffer);

        return Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
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
    public async Task TestServerPerformanceUnderLoad(int clientCount, int minMsAcceptableResponse, int maxMsAcceptableResponse)
    {
        //Arrange
        var tasks = new List<Task<Tuple<string, long>>>(); // To store response time tasks

        for (var i = 0; i < clientCount; i++)
        {
            tasks.Add(MeasureResponseTimeAsync($"/performance\r\nClient Number: {i}"));
        }

        //Act
        var responses = await Task.WhenAll(tasks);

        //Assert
        var index = 0;
        foreach (var response in responses)
        {
            var data = $"Client Number: {index}";
            Assert.Equal($"Performance test completed with data {data}", response.Item1);
            Assert.InRange(response.Item2, minMsAcceptableResponse, maxMsAcceptableResponse);
            index++;
        }
    }

    private async Task<Tuple<string, long>> MeasureResponseTimeAsync(string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", Port);

        await using var networkStream = client.GetStream();
        await using var sslStream = new SslStream(networkStream, false, (_, _, _, _) => true);

        await sslStream.AuthenticateAsClientAsync("localhost");

        var requestBytes = Encoding.UTF8.GetBytes(request);
        var stopwatch = Stopwatch.StartNew();

        await sslStream.WriteAsync(requestBytes);

        var buffer = new byte[4096];
        var bytesRead = await sslStream.ReadAsync(buffer);

        stopwatch.Stop();

        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

        return new Tuple<string, long>(response, stopwatch.ElapsedMilliseconds);
    }
}