using System.Collections.Concurrent;

namespace SocketApi;

public class ConnectionRegistry : IConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, ActiveConnection> _connections = new();

    public void Register(Guid connectionId, ActiveConnection connection)
    {
        _connections[connectionId] = connection;
    }

    public bool Remove(Guid connectionId)
    {
        return _connections.TryRemove(connectionId, out _);
    }

    public bool TryGet(Guid connectionId, out ActiveConnection? connection)
    {
        return _connections.TryGetValue(connectionId, out connection);
    }

    public IReadOnlyCollection<ActiveConnection> GetAll()
    {
        return _connections.Values.ToList().AsReadOnly();
    }

    public int RemoveStale(TimeSpan heartbeatTimeout)
    {
        var cutoff = DateTime.UtcNow - heartbeatTimeout;
        var stale = _connections.Where(kvp => kvp.Value.LastHeartbeat < cutoff).ToList();
        var count = 0;

        foreach (var kvp in stale)
        {
            if (_connections.TryRemove(kvp.Key, out var connection))
            {
                _ = connection.DisposeAsync();
                count++;
            }
        }

        return count;
    }
}
