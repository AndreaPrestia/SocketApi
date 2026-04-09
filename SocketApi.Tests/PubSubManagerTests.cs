namespace SocketApi.Tests;

public class PubSubManagerTests
{
    [Fact]
    public void Subscribe_ValidInput_ReturnsGuid()
    {
        var manager = new PubSubManager();
        var target = $"sub-{Guid.NewGuid()}";

        var id = manager.Subscribe(target, "127.0.0.1", 5000);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public void Subscribe_EmptyTarget_ThrowsInvalidOperation()
    {
        var manager = new PubSubManager();

        Assert.Throws<InvalidOperationException>(() =>
            manager.Subscribe("", "127.0.0.1", 5000));
    }

    [Fact]
    public void Subscribe_WhitespaceTarget_ThrowsInvalidOperation()
    {
        var manager = new PubSubManager();

        Assert.Throws<InvalidOperationException>(() =>
            manager.Subscribe("   ", "127.0.0.1", 5000));
    }

    [Fact]
    public void Subscribe_InvalidIp_ThrowsInvalidOperation()
    {
        var manager = new PubSubManager();
        var target = $"sub-{Guid.NewGuid()}";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            manager.Subscribe(target, "not-an-ip", 5000));

        Assert.Contains("not-an-ip", ex.Message);
    }

    [Fact]
    public void Subscribe_MultipleTimes_ReturnsDifferentIds()
    {
        var manager = new PubSubManager();
        var target = $"sub-{Guid.NewGuid()}";

        var id1 = manager.Subscribe(target, "127.0.0.1", 5000);
        var id2 = manager.Subscribe(target, "127.0.0.1", 5001);

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void UnSubscribe_ExistingSubscription_ReturnsTrue()
    {
        var manager = new PubSubManager();
        var target = $"unsub-{Guid.NewGuid()}";
        var id = manager.Subscribe(target, "127.0.0.1", 5000);

        var result = manager.UnSubscribe(id, target);

        Assert.True(result);
    }

    [Fact]
    public void UnSubscribe_NonExistentId_ReturnsFalse()
    {
        var manager = new PubSubManager();
        var target = $"unsub-{Guid.NewGuid()}";
        manager.Subscribe(target, "127.0.0.1", 5000);

        var result = manager.UnSubscribe(Guid.NewGuid(), target);

        Assert.False(result);
    }

    [Fact]
    public void UnSubscribe_NonExistentTarget_ReturnsFalse()
    {
        var manager = new PubSubManager();

        var result = manager.UnSubscribe(Guid.NewGuid(), $"missing-{Guid.NewGuid()}");

        Assert.False(result);
    }

    [Fact]
    public void UnSubscribe_SameIdTwice_ReturnsFalsOnSecondCall()
    {
        var manager = new PubSubManager();
        var target = $"unsub-{Guid.NewGuid()}";
        var id = manager.Subscribe(target, "127.0.0.1", 5000);

        var first = manager.UnSubscribe(id, target);
        var second = manager.UnSubscribe(id, target);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void UnSubscribe_DoesNotAffectOtherSubscriptions()
    {
        var manager = new PubSubManager();
        var target = $"unsub-{Guid.NewGuid()}";
        var id1 = manager.Subscribe(target, "127.0.0.1", 5000);
        var id2 = manager.Subscribe(target, "127.0.0.1", 5001);

        manager.UnSubscribe(id1, target);
        
        // id2 should still be unsubscribable
        var result = manager.UnSubscribe(id2, target);
        Assert.True(result);
    }

    [Fact]
    public void Publish_EmptyTarget_ReturnsZero()
    {
        var manager = new PubSubManager();

        var count = manager.Publish("", "payload");

        Assert.Equal(0, count);
    }

    [Fact]
    public void Publish_NullPayload_ReturnsZero()
    {
        var manager = new PubSubManager();
        var target = $"pub-{Guid.NewGuid()}";
        manager.Subscribe(target, "127.0.0.1", 5000);

        var count = manager.Publish(target, null);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Publish_WhitespacePayload_ReturnsZero()
    {
        var manager = new PubSubManager();
        var target = $"pub-{Guid.NewGuid()}";
        manager.Subscribe(target, "127.0.0.1", 5000);

        var count = manager.Publish(target, "   ");

        Assert.Equal(0, count);
    }

    [Fact]
    public void Publish_NoSubscribers_ReturnsZero()
    {
        var manager = new PubSubManager();

        var count = manager.Publish($"pub-{Guid.NewGuid()}", "hello");

        Assert.Equal(0, count);
    }
}
