using System.Collections.Concurrent;

namespace SocketApi.Server;

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