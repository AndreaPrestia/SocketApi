using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetworkStream = System.Net.Sockets.NetworkStream;

namespace SocketApi;

internal sealed class TcpSslServer : IHostedService
{
    private readonly int _port;
    private readonly int _backlog;
    private readonly long _maxRequestLength;
    private readonly long _maxResponseLength;
    private readonly CommandManager _commandManager = new(new PubSubManager());
    private readonly X509Certificate2 _certificate;
    private readonly ConcurrentDictionary<Guid, Task> _connectionTasks = new();
    private readonly ILogger<TcpSslServer> _logger;
    private CancellationTokenSource _cancellationTokenSource = new();
    private Socket? _listener;

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
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cancellationTokenSource.Token;
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listener.Bind(new IPEndPoint(IPAddress.Any, _port));
        _listener.Listen(_backlog);
        _logger.LogInformation("Server listening on port {port}.", _port);

        while (!token.IsCancellationRequested)
        {
            var clientSocket = await _listener.AcceptAsync(token);
            var connectionId = Guid.NewGuid();
            var connectionTask = HandleClientAsync(clientSocket, token);

            _connectionTasks[connectionId] = connectionTask;

            FireAndForget(connectionTask.ContinueWith(_ =>
            {
                _connectionTasks.TryRemove(connectionId, out var _);
                _logger.LogDebug($"Connection {connectionId} cleaned up.");
            }));
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down TCP server...");
        await _cancellationTokenSource.CancelAsync();
        _listener?.Dispose();
        await Task.WhenAll(_connectionTasks.Values);
        _cancellationTokenSource.Dispose();
        _logger.LogInformation("All connections cleaned up.");
    }

    private async Task HandleClientAsync(Socket clientSocket, CancellationToken cancellationToken)
    {
        var responseBytes = new byte[_maxResponseLength];
        await using var networkStream = new NetworkStream(clientSocket, ownsSocket: true);
        await using var sslStream = new SslStream(networkStream, false);

        try
        {
            await sslStream.AuthenticateAsServerAsync(_certificate, clientCertificateRequired: false,
                SslProtocols.Tls12, checkCertificateRevocation: true);

            var buffer = new byte[_maxRequestLength];
            var bytesRead = await sslStream.ReadAsync(buffer, cancellationToken);

            if (bytesRead == _maxRequestLength && networkStream.DataAvailable)
            {
                responseBytes =
                    MessagePackSerializer.Serialize(
                        OperationResult.Ko($"Max request length ({_maxRequestLength}) exceeded."),
                        cancellationToken: cancellationToken);
            }
            else
            {
                var remoteEndPoint = clientSocket.RemoteEndPoint?.ToString();
                var (operation, target, body) = ParseCustomProtocol(buffer.Take(bytesRead).ToArray());

                var result = await _commandManager.ManageAsync(OperationRequest.From(operation, target, body, remoteEndPoint));

                responseBytes = MessagePackSerializer.Serialize(result, cancellationToken: cancellationToken);

                if (responseBytes.Length > _maxResponseLength)
                {
                    responseBytes =
                        MessagePackSerializer.Serialize(
                            OperationResult.Ko($"Max response length ({_maxResponseLength}) exceeded."),
                            cancellationToken: cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", ex.Message);
            responseBytes =
                MessagePackSerializer.Serialize(OperationResult.Ko(ex.Message), cancellationToken: cancellationToken);
        }
        finally
        {
            await sslStream.WriteAsync(responseBytes,
                cancellationToken);
            await sslStream.FlushAsync(cancellationToken);
            clientSocket.Dispose();
        }
    }

    private (Operation operation, string target, string payload) ParseCustomProtocol(
        byte[] requestBytes)
    {
        var requestText = MessagePackSerializer.Deserialize<string>(requestBytes);
        var content = requestText.Split("|");

        if (content.Length < 2)
            throw new FormatException($"Invalid protocol format. Expected 'operation|target[|payload]', got: '{requestText}'");

        var operation = Enum.Parse<Operation>(content[0], ignoreCase: true);
        var target = content[1];
        var body = content.Length > 2 ? content[2] : string.Empty;
        return (operation, target, body);
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