namespace SocketApi.Tests;

public class CommandManagerTests
{
    private readonly CommandManager _commandManager = new(new PubSubManager());

    [Fact]
    public async Task ManageAsync_Ping_ReturnsPong()
    {
        var request = OperationRequest.From(Operation.Ping, "", null);

        var result = await _commandManager.ManageAsync(request);

        Assert.True(result.Success);
        Assert.Equal("PONG", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Info_ReturnsVersion()
    {
        var request = OperationRequest.From(Operation.Info, "", null);

        var result = await _commandManager.ManageAsync(request);

        Assert.True(result.Success);
        Assert.Contains("SocketApi running on version", result.Payload?.ToString());
    }

    [Fact]
    public async Task ManageAsync_Call_RoutesToRegisteredHandler()
    {
        var target = $"cmd-call-{Guid.NewGuid()}";
        Router.Operation(target, req => Task.FromResult(OperationResult.Ok($"received:{req?.Payload}")));

        var request = OperationRequest.From(Operation.Call, target, "test-data");
        var result = await _commandManager.ManageAsync(request);

        Assert.True(result.Success);
        Assert.Equal("received:test-data", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Call_UnknownTarget_ReturnsKo()
    {
        var target = $"cmd-missing-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.Call, target, null);
        var result = await _commandManager.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Contains(target, result.Payload?.ToString());
    }

    [Fact]
    public async Task ManageAsync_Sub_ValidOrigin_ReturnsSubscriptionId()
    {
        var target = $"cmd-sub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.Sub, target, null, "127.0.0.1:9000");
        var result = await _commandManager.ManageAsync(request);

        Assert.True(result.Success);
        Assert.IsType<Guid>(result.Payload);
        Assert.NotEqual(Guid.Empty, (Guid)result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Sub_NullOrigin_ReturnsKo()
    {
        var target = $"cmd-sub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.Sub, target, null, null);
        var result = await _commandManager.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Equal("Invalid origin", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Sub_InvalidOriginFormat_ReturnsKo()
    {
        var target = $"cmd-sub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.Sub, target, null, "no-colon-here");
        var result = await _commandManager.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Equal("Invalid origin", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Sub_InvalidPort_ReturnsKo()
    {
        var target = $"cmd-sub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.Sub, target, null, "127.0.0.1:notaport");
        var result = await _commandManager.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Equal("Invalid port", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_UnSub_ValidGuid_AfterSubscribe_ReturnsOk()
    {
        var target = $"cmd-unsub-{Guid.NewGuid()}";

        // Subscribe first
        var subRequest = OperationRequest.From(Operation.Sub, target, null, "127.0.0.1:9000");
        var subResult = await _commandManager.ManageAsync(subRequest);
        var subscriptionId = (Guid)subResult.Payload!;

        // Unsubscribe
        var unsubRequest = OperationRequest.From(Operation.UnSub, target, subscriptionId.ToString());
        var result = await _commandManager.ManageAsync(unsubRequest);

        Assert.True(result.Success);
        Assert.Equal("OK", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_UnSub_InvalidGuid_ReturnsKo()
    {
        var target = $"cmd-unsub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.UnSub, target, "not-a-guid");
        var result = await _commandManager.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Contains("not-a-guid", result.Payload?.ToString());
    }

    [Fact]
    public async Task ManageAsync_UnSub_NonExistentSubscription_ReturnsKo()
    {
        var target = $"cmd-unsub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.UnSub, target, Guid.NewGuid().ToString());
        var result = await _commandManager.ManageAsync(request);

        Assert.False(result.Success);
        Assert.Equal("KO", result.Payload);
    }

    [Fact]
    public async Task ManageAsync_Pub_NoSubscribers_ReturnsOkWithZero()
    {
        var target = $"cmd-pub-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.Pub, target, "hello");
        var result = await _commandManager.ManageAsync(request);

        Assert.True(result.Success);
        Assert.Equal(0, result.Payload);
    }
}
