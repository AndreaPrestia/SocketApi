using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SocketApi.Server;

internal sealed class TcpSslServer : IHostedService
{
    private readonly int _port;
    private readonly int _backlog;
    private readonly X509Certificate2 _certificate;
    private readonly ConcurrentBag<Task> _connectionTasks = new ConcurrentBag<Task>();
    private readonly ILogger<TcpSslServer> _logger;
    private CancellationToken _cancellationToken;

    internal TcpSslServer(int port, string certPath, string certPassword, int backlog, ILogger<TcpSslServer> logger)
    {
        _port = port;
        _logger = logger;
        _certificate = new X509Certificate2(certPath, certPassword);
        _backlog = backlog;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Any, _port));
        listener.Listen(_backlog);
        _logger.LogInformation("Server listening on port {port}.", _port);

        while (!_cancellationToken.IsCancellationRequested)
        {
            var clientSocket = await listener.AcceptAsync(_cancellationToken);
            var task = Task.Run(() => HandleClientAsync(clientSocket, _cancellationToken), _cancellationToken);
            _connectionTasks.Add(task);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down TCP server...");
        _cancellationToken = new CancellationToken(true);
        return Task.CompletedTask;
    }

    private async Task HandleClientAsync(Socket clientSocket, CancellationToken cancellationToken)
    {
        await using var networkStream = new NetworkStream(clientSocket, ownsSocket: true);
        await using var sslStream = new SslStream(networkStream, false);

        try
        {
            await sslStream.AuthenticateAsServerAsync(_certificate, clientCertificateRequired: false,
                SslProtocols.Tls12, checkCertificateRevocation: true);

            var buffer = new byte[4096];
            var bytesRead = await sslStream.ReadAsync(buffer, cancellationToken);

            var (method, route, parameters, body) = ParseCustomProtocol(buffer.Take(bytesRead).ToArray());
            _logger.LogDebug("Method: {method}, Route: {route}", method, route);

            async Task WriteResponse(string responseMessage)
            {
                var responseBytes = System.Text.Encoding.UTF8.GetBytes(responseMessage);
                await sslStream.WriteAsync(responseBytes, cancellationToken);
            }

            // Route the request based on parsed route
            await Router.RouteRequestAsync(route, parameters, body, WriteResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", ex.Message);
        }
        finally
        {
            clientSocket.Dispose();
        }
    }

    private (string method, string route, Dictionary<string, string> parameters, string body) ParseCustomProtocol(
        byte[] requestBytes)
    {
        var requestText = System.Text.Encoding.UTF8.GetString(requestBytes);
        var lines = requestText.Split(new[] { "\r\n" }, StringSplitOptions.None);

        // The first line will be the request line (e.g., "GET /route?param=value HTTP/1.1" or "POST /route HTTP/1.1")
        var requestLine = lines[0];
        var requestParts = requestLine.Split(' ');

        if (requestParts.Length < 2)
            throw new FormatException("Invalid request format");

        var method = requestParts[0]; // GET or POST
        var route = requestParts[1]; // Route with potential query parameters (for GET)

        Dictionary<string, string> parameters = new();
        var body = string.Empty;

        if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            // Parse route and query parameters for GET request
            var routeParts = route.Split('?');
            route = routeParts[0]; // Base route

            if (routeParts.Length > 1)
            {
                var queryParams = routeParts[1].Split('&');
                foreach (var param in queryParams)
                {
                    var keyValue = param.Split('=');
                    if (keyValue.Length == 2)
                        parameters[keyValue[0]] = keyValue[1];
                }
            }
        }
        else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            // Parse the Content-Length header to get the body
            var contentLength = 0;
            var bodyStarted = false;

            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i]))
                {
                    bodyStarted = true;
                    continue;
                }

                if (bodyStarted)
                {
                    body += lines[i];
                }
                else if (lines[i].StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(lines[i].Split(':')[1].Trim(), out contentLength);
                }
            }

            // Ensure body length matches Content-Length
            if (body.Length > contentLength)
                body = body.Substring(0, contentLength);
        }

        return (method, route, parameters, body);
    }
}