using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SocketApi.Server.Tests;

public class TcpSslClient
{
    private readonly string _server;
    private readonly int _port;
    private readonly ConcurrentBag<TcpClient> _clientPool = new ConcurrentBag<TcpClient>();

    public TcpSslClient(string server, int port)
    {
        _server = server;
        _port = port;
    }

    public async Task<string> SendRequestAsync(string message)
    {
        var client = await GetClientAsync();
        await using var sslStream = new SslStream(client.GetStream(), false, ValidateServerCertificate!, null);
        await sslStream.AuthenticateAsClientAsync(_server);
            
        // Write message
        var data = Encoding.UTF8.GetBytes(message);
        await sslStream.WriteAsync(data, 0, data.Length);

        // Read response
        var buffer = BufferManager.RentBuffer();
        var bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length);
        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        ReturnClient(client);
        return response;
    }

    private async Task<TcpClient> GetClientAsync()
    {
        if (!_clientPool.TryTake(out var client))
        {
            client = new TcpClient();
            await client.ConnectAsync(_server, _port);
        }
        return client;
    }

    private void ReturnClient(TcpClient client)
    {
        _clientPool.Add(client);  // Reuse client for connection pooling
    }

    private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        return true; // For testing purposes, you may want to validate in production
    }
}

internal static class BufferManager
{
    private const int BufferSize = 4096;
    private static readonly ConcurrentBag<byte[]> BufferPool = new();

    public static byte[] RentBuffer()
    {
        if (!BufferPool.TryTake(out var buffer))
        {
            buffer = new byte[BufferSize];
        }
        return buffer;
    }
}