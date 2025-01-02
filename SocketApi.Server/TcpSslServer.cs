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
    private readonly ConcurrentDictionary<Guid, Task> _connectionTasks = new();
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
            var connectionId = Guid.NewGuid();
            var connectionTask = HandleClientAsync(clientSocket, _cancellationToken);

            _connectionTasks[connectionId] = connectionTask;

            FireAndForget(connectionTask.ContinueWith(_ =>
            {
                _connectionTasks.TryRemove(connectionId, out _);
                _logger.LogDebug($"Connection {connectionId} cleaned up.");
            }, TaskContinuationOptions.OnlyOnRanToCompletion));
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down TCP server...");
        _cancellationToken = new CancellationToken(true);
        await Task.WhenAll(_connectionTasks.Values);
        _logger.LogInformation("All connections cleaned up.");
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

            var (route, parameters, body) = ParseCustomProtocol(buffer.Take(bytesRead).ToArray());
            _logger.LogDebug("Route: {route}", route);

            async Task WriteResponse(string responseMessage)
            {
                var responseBytes = System.Text.Encoding.UTF8.GetBytes(responseMessage);
                await sslStream.WriteAsync(responseBytes, cancellationToken);
            }

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

    private (string route, Dictionary<string, string> parameters, string body) ParseCustomProtocol(
        byte[] requestBytes)
    {
        var requestText = System.Text.Encoding.UTF8.GetString(requestBytes);
        var lines = requestText.Split(new[] { "\r\n" }, StringSplitOptions.None);

        var route = lines[0];

        var parameters = new Dictionary<string, string>();
        var body = string.Empty;

        var routeParts = route.Split('?');
        route = routeParts[0];

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

        for (var i = 1; i < lines.Length; i++)
        {
            body += lines[i];
        }

        return (route, parameters, body);
    }

    private void FireAndForget(Task task)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogWarning($"Task failed: {t.Exception?.GetBaseException().Message}");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}