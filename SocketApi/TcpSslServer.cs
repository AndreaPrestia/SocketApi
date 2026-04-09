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
    private readonly TimeSpan _heartbeatTimeout;
    private readonly CommandManager _commandManager;
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly X509Certificate2 _certificate;
    private readonly ConcurrentDictionary<Guid, Task> _connectionTasks = new();
    private readonly ILogger<TcpSslServer> _logger;
    private CancellationTokenSource _cancellationTokenSource = new();
    private Socket? _listener;
    private Task? _heartbeatTask;

    internal TcpSslServer(int port, X509Certificate2 certificate, int backlog, long maxRequestLength,
        long maxResponseLength, TimeSpan heartbeatTimeout, IConnectionRegistry connectionRegistry,
        ILogger<TcpSslServer> logger)
    {
        _port = port;
        _logger = logger;
        _certificate = certificate;
        _maxRequestLength = maxRequestLength;
        _maxResponseLength = maxResponseLength;
        _heartbeatTimeout = heartbeatTimeout;
        _backlog = backlog;
        _connectionRegistry = connectionRegistry;
        _commandManager = new CommandManager(new PubSubManager(connectionRegistry));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cancellationTokenSource.Token;
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listener.Bind(new IPEndPoint(IPAddress.Any, _port));
        _listener.Listen(_backlog);
        _logger.LogInformation("Server listening on port {port}.", _port);

        _heartbeatTask = RunHeartbeatCleanupAsync(token);

        while (!token.IsCancellationRequested)
        {
            var clientSocket = await _listener.AcceptAsync(token);
            var connectionId = Guid.NewGuid();
            var connectionTask = HandleClientAsync(clientSocket, connectionId, token);

            _connectionTasks[connectionId] = connectionTask;

            FireAndForget(connectionTask.ContinueWith(_ =>
            {
                _connectionTasks.TryRemove(connectionId, out _);
                _connectionRegistry.Remove(connectionId);
                _logger.LogDebug("Connection {connectionId} cleaned up.", connectionId);
            }));
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down TCP server...");
        await _cancellationTokenSource.CancelAsync();
        _listener?.Dispose();
        if (_heartbeatTask != null)
            await _heartbeatTask;
        await Task.WhenAll(_connectionTasks.Values);
        _cancellationTokenSource.Dispose();
        _logger.LogInformation("All connections cleaned up.");
    }

    private async Task HandleClientAsync(Socket clientSocket, Guid connectionId, CancellationToken cancellationToken)
    {
        await using var networkStream = new NetworkStream(clientSocket, ownsSocket: true);
        await using var sslStream = new SslStream(networkStream, false);

        await sslStream.AuthenticateAsServerAsync(_certificate, clientCertificateRequired: false,
            SslProtocols.Tls12, checkCertificateRevocation: true);

        var connection = new ActiveConnection(connectionId, sslStream, clientSocket);
        _connectionRegistry.Register(connectionId, connection);

        var buffer = new byte[_maxRequestLength];

        while (!cancellationToken.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                bytesRead = await sslStream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0) break; // Client disconnected
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                break; // Connection lost
            }

            byte[] responseBytes;
            try
            {
                if (bytesRead == _maxRequestLength && networkStream.DataAvailable)
                {
                    responseBytes = MessagePackSerializer.Serialize(
                        OperationResult.Ko($"Max request length ({_maxRequestLength}) exceeded."),
                        cancellationToken: cancellationToken);
                }
                else
                {
                    var parsed = ParseCustomProtocol(buffer.AsSpan(0, bytesRead).ToArray());

                    if (parsed.operation == Operation.Heartbeat)
                        connection.RefreshHeartbeat();

                    var request = OperationRequest.From(
                        parsed.operation, parsed.target, parsed.payload,
                        connectionId.ToString(), parsed.messageId, parsed.qos);

                    var result = await _commandManager.ManageAsync(request);

                    responseBytes = MessagePackSerializer.Serialize(result, cancellationToken: cancellationToken);

                    if (responseBytes.Length > _maxResponseLength)
                    {
                        responseBytes = MessagePackSerializer.Serialize(
                            OperationResult.Ko($"Max response length ({_maxResponseLength}) exceeded."),
                            cancellationToken: cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error: {error}", ex.Message);
                responseBytes = MessagePackSerializer.Serialize(
                    OperationResult.Ko(ex.Message), cancellationToken: cancellationToken);
            }

            try
            {
                await connection.WriteAsync(responseBytes, cancellationToken);
            }
            catch
            {
                break; // Cannot write — connection lost
            }
        }

        _connectionRegistry.Remove(connectionId);
        _logger.LogDebug("Client {connectionId} disconnected.", connectionId);
    }

    private (Operation operation, string target, string payload, string? messageId, int qos) ParseCustomProtocol(
        byte[] requestBytes)
    {
        var requestText = MessagePackSerializer.Deserialize<string>(requestBytes);
        var content = requestText.Split("|");

        if (content.Length < 2)
            throw new FormatException($"Invalid protocol format. Expected 'operation|target[|payload[|messageId[|qos]]]', got: '{requestText}'");

        var operation = Enum.Parse<Operation>(content[0], ignoreCase: true);
        var target = content[1];
        var payload = content.Length > 2 ? content[2] : string.Empty;
        var messageId = content.Length > 3 && !string.IsNullOrEmpty(content[3]) ? content[3] : null;
        var qos = content.Length > 4 && int.TryParse(content[4], out var q) ? q : 0;
        return (operation, target, payload, messageId, qos);
    }

    private async Task RunHeartbeatCleanupAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_heartbeatTimeout, cancellationToken);
                var removed = _connectionRegistry.RemoveStale(_heartbeatTimeout);
                if (removed > 0)
                    _logger.LogInformation("Removed {count} stale connections.", removed);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void FireAndForget(Task? task)
    {
        task?.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogWarning("Task failed: {error}", t.Exception?.GetBaseException().Message);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}