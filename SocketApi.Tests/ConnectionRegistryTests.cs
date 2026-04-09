namespace SocketApi.Tests;

public class ConnectionRegistryTests
{
    [Fact]
    public void Register_AndTryGet_ReturnsConnection()
    {
        var registry = new ConnectionRegistry();
        var id = Guid.NewGuid();
        var connection = CreateFakeConnection(id);

        registry.Register(id, connection);
        var found = registry.TryGet(id, out var result);

        Assert.True(found);
        Assert.Same(connection, result);
    }

    [Fact]
    public void TryGet_NonExistent_ReturnsFalse()
    {
        var registry = new ConnectionRegistry();

        var found = registry.TryGet(Guid.NewGuid(), out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void Remove_ExistingConnection_ReturnsTrue()
    {
        var registry = new ConnectionRegistry();
        var id = Guid.NewGuid();
        registry.Register(id, CreateFakeConnection(id));

        var removed = registry.Remove(id);

        Assert.True(removed);
        Assert.False(registry.TryGet(id, out _));
    }

    [Fact]
    public void Remove_NonExistent_ReturnsFalse()
    {
        var registry = new ConnectionRegistry();

        Assert.False(registry.Remove(Guid.NewGuid()));
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        var registry = new ConnectionRegistry();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        registry.Register(id1, CreateFakeConnection(id1));
        registry.Register(id2, CreateFakeConnection(id2));

        var all = registry.GetAll();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetAll_Empty_ReturnsEmpty()
    {
        var registry = new ConnectionRegistry();

        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void Register_SameId_OverwritesPrevious()
    {
        var registry = new ConnectionRegistry();
        var id = Guid.NewGuid();
        var first = CreateFakeConnection(id);
        var second = CreateFakeConnection(id);

        registry.Register(id, first);
        registry.Register(id, second);

        registry.TryGet(id, out var result);
        Assert.Same(second, result);
    }

    private static ActiveConnection CreateFakeConnection(Guid id)
    {
        // Create a minimal ActiveConnection for testing registry logic.
        // SslStream/Socket are not used in these tests.
        return new ActiveConnection(id, null!, null!);
    }
}
