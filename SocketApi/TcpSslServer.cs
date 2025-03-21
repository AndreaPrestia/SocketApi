using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SocketApi;

internal sealed class TcpSslServer : IHostedService
{
    private readonly int _port;
    private readonly int _backlog;
    private readonly long _maxRequestLength;
    private readonly long _maxResponseLength;
    private readonly X509Certificate2 _certificate;
    private readonly ConcurrentDictionary<Guid, Task> _connectionTasks = new();
    private readonly ILogger<TcpSslServer> _logger;
    private CancellationToken _cancellationToken;

    internal TcpSslServer(int port, X509Certificate2 certificate, int backlog, long maxRequestLength,
        long maxResponseLength, ILogger<TcpSslServer> logger)
    {
        _port = port;
        _logger = logger;
        _certificate = certificate;
        _maxRequestLength = maxRequestLength;
        _maxResponseLength = maxResponseLength;
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
                _connectionTasks.TryRemove(connectionId, out var _);
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

            var buffer = new byte[_maxRequestLength];
            var bytesRead = await sslStream.ReadAsync(buffer, cancellationToken);

            if (sslStream.CanRead && networkStream.DataAvailable)
            {
                var maxLengthOperationResult =
                    OperationResult.Ko($"Max request length ({_maxResponseLength}) exceeded.");
                await sslStream.WriteAsync(
                    MessagePackSerializer.Serialize(maxLengthOperationResult, cancellationToken: cancellationToken),
                    cancellationToken);
            }
            else
            {
                
                var (route, body) = ParseCustomProtocol(buffer.Take(bytesRead).ToArray());
                _logger.LogDebug("Route: {route}", route);

                var result = await Router.RouteRequestAsync(route,
                    OperationRequest.From(route, clientSocket.RemoteEndPoint?.ToString(), body));

                var responseBytes = MessagePackSerializer.Serialize(result, cancellationToken: cancellationToken);

                if (responseBytes.Length > _maxResponseLength)
                {
                    var maxLengthOperationResult =
                        OperationResult.Ko($"Max response length ({_maxResponseLength}) exceeded.");
                    await sslStream.WriteAsync(
                        MessagePackSerializer.Serialize(maxLengthOperationResult, cancellationToken: cancellationToken),
                        cancellationToken);
                }
                else
                {
                    await sslStream.WriteAsync(responseBytes, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", ex.Message);
            var koResult = OperationResult.Ko(ex.Message);
            await sslStream.WriteAsync(MessagePackSerializer.Serialize(koResult, cancellationToken: cancellationToken),
                cancellationToken);
        }
        finally
        {
            await sslStream.FlushAsync(cancellationToken);
            clientSocket.Dispose();
        }
    }

    private (string operation, string request) ParseCustomProtocol(
        byte[] requestBytes)
    {
        var requestText = MessagePackSerializer.Deserialize<string>(requestBytes);
        var content = requestText.Split("|");
        var operation = content[0];
        var body = content.Length > 1 ? content[1] : string.Empty;
        return (operation, body);
    }

    private void FireAndForget(Task? task)
    {
        task?.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogWarning($"Task failed: {t.Exception?.GetBaseException().Message}");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}