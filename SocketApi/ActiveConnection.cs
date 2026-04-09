using System.Net.Security;
using System.Net.Sockets;

namespace SocketApi;

public class ActiveConnection : IAsyncDisposable
{
    public Guid Id { get; }
    public SslStream SslStream { get; }
    public Socket Socket { get; }
    public DateTime LastHeartbeat { get; private set; }
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    internal ActiveConnection(Guid id, SslStream sslStream, Socket socket)
    {
        Id = id;
        SslStream = sslStream;
        Socket = socket;
        LastHeartbeat = DateTime.UtcNow;
    }

    public void RefreshHeartbeat() => LastHeartbeat = DateTime.UtcNow;

    public async Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await SslStream.WriteAsync(data, cancellationToken);
            await SslStream.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await SslStream.DisposeAsync();
        Socket.Dispose();
        _writeLock.Dispose();
    }
}
