namespace SocketApi;

public interface IConnectionRegistry
{
    void Register(Guid connectionId, ActiveConnection connection);
    bool Remove(Guid connectionId);
    bool TryGet(Guid connectionId, out ActiveConnection? connection);
    IReadOnlyCollection<ActiveConnection> GetAll();
    int RemoveStale(TimeSpan heartbeatTimeout);
}
