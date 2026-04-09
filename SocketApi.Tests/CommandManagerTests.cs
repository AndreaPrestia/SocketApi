namespace SocketApi.Tests;

public class CommandManagerTests
{
    private static CommandManager CreateManager() => new(new PubSubManager(new ConnectionRegistry()));

    [Fact]
    public async Task ManageAsync_Ping_ReturnsPong()
    {
        var cm = CreateManager();
        var request = OperationRequest.From(Operation.Ping, "", null);

        var result = await cm.ManageAsync(request);

        Assert.True(result.Success);
        Assert.Equal("PONG", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Info_ReturnsVersion()
    {
        var cm = CreateManager();
        var request = OperationRequest.From(Operation.Info, "", null);

        var result = await cm.ManageAsync(request);

        Assert.True(result.Success);
        Assert.Contains("SocketApi running on version", result.Payload?.ToString());
    }

    [Fact]
    public async Task ManageAsync_Call_RoutesToRegisteredHandler()
    {
        var cm = CreateManager();
        var target = $"cmd-call-{Guid.NewGuid()}";
        Router.Operation(target, req => Task.FromResult(OperationResult.Ok($"received:{req?.Payload}")));

        var request = OperationRequest.From(Operation.Call, target, "test-data");
        var result = await cm.ManageAsync(request);

        Assert.True(result.Success);
        Assert.Equal("received:test-data", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Call_UnknownTarget_ReturnsKo()
    {
        var cm = CreateManager();
        var target = $"cmd-missing-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.Call, target, null);
        var result = await cm.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Contains(target, result.Payload?.ToString());
    }

    [Fact]
    public async Task ManageAsync_Sub_ValidConnectionId_ReturnsSubscriptionId()
    {
        var cm = CreateManager();
        var target = $"cmd-sub-{Guid.NewGuid()}";
        var connId = Guid.NewGuid();

        var request = OperationRequest.From(Operation.Sub, target, null, connId.ToString());
        var result = await cm.ManageAsync(request);

        Assert.True(result.Success);
        Assert.IsType<Guid>(result.Payload);
        Assert.NotEqual(Guid.Empty, (Guid)result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Sub_NullOrigin_ReturnsKo()
    {
        var cm = CreateManager();
        var target = $"cmd-sub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.Sub, target, null, null);
        var result = await cm.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Equal("Invalid connection id", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Sub_InvalidOriginFormat_ReturnsKo()
    {
        var cm = CreateManager();
        var target = $"cmd-sub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.Sub, target, null, "not-a-guid");
        var result = await cm.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Equal("Invalid connection id", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Sub_WithQos_ReturnsSubscriptionId()
    {
        var cm = CreateManager();
        var target = $"cmd-sub-{Guid.NewGuid()}";
        var connId = Guid.NewGuid();

        var request = OperationRequest.From(Operation.Sub, target, null, connId.ToString(), qos: 1);
        var result = await cm.ManageAsync(request);

        Assert.True(result.Success);
        Assert.IsType<Guid>(result.Payload);
    }

    [Fact]
    public async Task ManageAsync_UnSub_ValidGuid_AfterSubscribe_ReturnsOk()
    {
        var cm = CreateManager();
        var target = $"cmd-unsub-{Guid.NewGuid()}";
        var connId = Guid.NewGuid();

        var subRequest = OperationRequest.From(Operation.Sub, target, null, connId.ToString());
        var subResult = await cm.ManageAsync(subRequest);
        var subscriptionId = (Guid)subResult.Payload!;

        var unsubRequest = OperationRequest.From(Operation.UnSub, target, subscriptionId.ToString());
        var result = await cm.ManageAsync(unsubRequest);

        Assert.True(result.Success);
        Assert.Equal("OK", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_UnSub_InvalidGuid_ReturnsKo()
    {
        var cm = CreateManager();
        var target = $"cmd-unsub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.UnSub, target, "not-a-guid");
        var result = await cm.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Contains("not-a-guid", result.Payload?.ToString());
    }

    [Fact]
    public async Task ManageAsync_UnSub_NonExistentSubscription_ReturnsKo()
    {
        var cm = CreateManager();
        var target = $"cmd-unsub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.UnSub, target, Guid.NewGuid().ToString());
        var result = await cm.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Equal("KO", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Pub_NoSubscribers_ReturnsOkWithZero()
    {
        var cm = CreateManager();
        var target = $"cmd-pub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.Pub, target, "hello");
        var result = await cm.ManageAsync(request);

        Assert.True(result.Success);
        Assert.Equal(0, result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Ack_ValidMessageId_ReturnsOk()
    {
        var cm = CreateManager();
        var request = OperationRequest.From(Operation.Ack, "", null, messageId: "msg-123");

        var result = await cm.ManageAsync(request);

        Assert.True(result.Success);
        Assert.Equal("OK", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Ack_MissingMessageId_ReturnsKo()
    {
        var cm = CreateManager();
        var request = OperationRequest.From(Operation.Ack, "", null);

        var result = await cm.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Equal("Missing message id", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Heartbeat_ReturnsOk()
    {
        var cm = CreateManager();
        var request = OperationRequest.From(Operation.Heartbeat, "", null);

        var result = await cm.ManageAsync(request);

        Assert.True(result.Success);
        Assert.Equal("OK", result.Payload);
    }
}
