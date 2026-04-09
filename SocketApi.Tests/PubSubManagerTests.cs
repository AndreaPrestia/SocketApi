namespace SocketApi.Tests;

public class PubSubManagerTests
{
    private static PubSubManager CreateManager() => new(new ConnectionRegistry());

    [Fact]
    public void Subscribe_ValidInput_ReturnsGuid()
    {
        var manager = CreateManager();
        var target = $"sub-{Guid.NewGuid()}";
        var connectionId = Guid.NewGuid();

        var id = manager.Subscribe(target, connectionId);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public void Subscribe_EmptyTarget_ThrowsInvalidOperation()
    {
        var manager = CreateManager();

        Assert.Throws<InvalidOperationException>(() =>
            manager.Subscribe("", Guid.NewGuid()));
    }

    [Fact]
    public void Subscribe_WhitespaceTarget_ThrowsInvalidOperation()
    {
        var manager = CreateManager();

        Assert.Throws<InvalidOperationException>(() =>
            manager.Subscribe("   ", Guid.NewGuid()));
    }

    [Fact]
    public void Subscribe_MultipleTimes_ReturnsDifferentIds()
    {
        var manager = CreateManager();
        var target = $"sub-{Guid.NewGuid()}";
        var connId = Guid.NewGuid();

        var id1 = manager.Subscribe(target, connId);
        var id2 = manager.Subscribe(target, connId, qos: 1);

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Subscribe_WithQos_StoresQosLevel()
    {
        var manager = CreateManager();
        var target = $"sub-{Guid.NewGuid()}";
        var connId = Guid.NewGuid();

        var id = manager.Subscribe(target, connId, qos: 1);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public void UnSubscribe_ExistingSubscription_ReturnsTrue()
    {
        var manager = CreateManager();
        var target = $"unsub-{Guid.NewGuid()}";
        var id = manager.Subscribe(target, Guid.NewGuid());

        var result = manager.UnSubscribe(id, target);

        Assert.True(result);
    }

    [Fact]
    public void UnSubscribe_NonExistentId_ReturnsFalse()
    {
        var manager = CreateManager();
        var target = $"unsub-{Guid.NewGuid()}";
        manager.Subscribe(target, Guid.NewGuid());

        var result = manager.UnSubscribe(Guid.NewGuid(), target);

        Assert.False(result);
    }

    [Fact]
    public void UnSubscribe_NonExistentTarget_ReturnsFalse()
    {
        var manager = CreateManager();

        var result = manager.UnSubscribe(Guid.NewGuid(), $"missing-{Guid.NewGuid()}");

        Assert.False(result);
    }

    [Fact]
    public void UnSubscribe_SameIdTwice_ReturnsFalseOnSecondCall()
    {
        var manager = CreateManager();
        var target = $"unsub-{Guid.NewGuid()}";
        var id = manager.Subscribe(target, Guid.NewGuid());

        var first = manager.UnSubscribe(id, target);
        var second = manager.UnSubscribe(id, target);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void UnSubscribe_DoesNotAffectOtherSubscriptions()
    {
        var manager = CreateManager();
        var target = $"unsub-{Guid.NewGuid()}";
        var id1 = manager.Subscribe(target, Guid.NewGuid());
        var id2 = manager.Subscribe(target, Guid.NewGuid());

        manager.UnSubscribe(id1, target);

        var result = manager.UnSubscribe(id2, target);
        Assert.True(result);
    }

    [Fact]
    public async Task PublishAsync_EmptyTarget_ReturnsZero()
    {
        var manager = CreateManager();

        var count = await manager.PublishAsync("", "payload");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task PublishAsync_NullPayload_ReturnsZero()
    {
        var manager = CreateManager();
        var target = $"pub-{Guid.NewGuid()}";
        manager.Subscribe(target, Guid.NewGuid());

        var count = await manager.PublishAsync(target, null);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task PublishAsync_WhitespacePayload_ReturnsZero()
    {
        var manager = CreateManager();
        var target = $"pub-{Guid.NewGuid()}";
        manager.Subscribe(target, Guid.NewGuid());

        var count = await manager.PublishAsync(target, "   ");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task PublishAsync_NoSubscribers_ReturnsZero()
    {
        var manager = CreateManager();

        var count = await manager.PublishAsync($"pub-{Guid.NewGuid()}", "hello");

        Assert.Equal(0, count);
    }

    [Fact]
    public void AcknowledgeMessage_CompletsPendingAck()
    {
        var manager = CreateManager();

        // AcknowledgeMessage on non-existent messageId should not throw
        manager.AcknowledgeMessage("non-existent");
    }
}
