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
            await sslStream.AuthenticateAsServerAsync(_certificate, clientCertificateRequired: false, SslProtocols.Tls12, checkCertificateRevocation: true);

            var buffer = BufferManager.RentBuffer();
            var bytesRead = await sslStream.ReadAsync(buffer, cancellationToken);

            var message = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            _logger.LogDebug("Received: {message}", message);

            var response = System.Text.Encoding.UTF8.GetBytes("RECEIVED");
            await sslStream.WriteAsync(response, cancellationToken);
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
}