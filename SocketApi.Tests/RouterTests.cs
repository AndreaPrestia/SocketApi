namespace SocketApi.Tests;

public class RouterTests
{
    [Fact]
    public async Task RouteRequestAsync_RegisteredTarget_ReturnsHandlerResult()
    {
        var target = $"router-test-{Guid.NewGuid()}";
        Router.Operation(target, _ => Task.FromResult(OperationResult.Ok("handled")));

        var request = OperationRequest.From(Operation.Call, target, "payload");
        var result = await Router.RouteRequestAsync(target, request);

        Assert.True(result.Success);
        Assert.Equal("handled", result.Payload);
    }

    [Fact]
    public async Task RouteRequestAsync_UnregisteredTarget_ReturnsKo()
    {
        var target = $"missing-{Guid.NewGuid()}";

        var request = OperationRequest.From(Operation.Call, target, null);
        var result = await Router.RouteRequestAsync(target, request);

        Assert.False(result.Success);
        Assert.Contains(target, result.Payload?.ToString());
    }

    [Fact]
    public async Task RouteRequestAsync_HandlerThrows_ReturnsKoWithException()
    {
        var target = $"throwing-{Guid.NewGuid()}";
        Router.Operation(target, _ => throw new InvalidOperationException("boom"));

        var request = OperationRequest.From(Operation.Call, target, null);
        var result = await Router.RouteRequestAsync(target, request);

        Assert.False(result.Success);
        Assert.IsType<InvalidOperationException>(result.Payload);
    }

    [Fact]
    public async Task RouteRequestAsync_AsyncHandler_AwaitsCorrectly()
    {
        var target = $"async-{Guid.NewGuid()}";
        Router.Operation(target, async req =>
        {
            await Task.Yield();
            return OperationResult.Ok($"async-{req?.Payload}");
        });

        var request = OperationRequest.From(Operation.Call, target, "data");
        var result = await Router.RouteRequestAsync(target, request);

        Assert.True(result.Success);
        Assert.Equal("async-data", result.Payload);
    }

    [Fact]
    public async Task Operation_OverwritesSameTarget()
    {
        var target = $"overwrite-{Guid.NewGuid()}";
        Router.Operation(target, _ => Task.FromResult(OperationResult.Ok("first")));
        Router.Operation(target, _ => Task.FromResult(OperationResult.Ok("second")));

        var request = OperationRequest.From(Operation.Call, target, null);
        var result = await Router.RouteRequestAsync(target, request);

        Assert.Equal("second", result.Payload);
    }

    [Fact]
    public async Task RouteRequestAsync_PassesRequestToHandler()
    {
        var target = $"passthrough-{Guid.NewGuid()}";
        OperationRequest? captured = null;
        Router.Operation(target, req =>
        {
            captured = req;
            return Task.FromResult(OperationResult.Ok("ok"));
        });

        var request = OperationRequest.From(Operation.Call, target, "my-payload", "10.0.0.1:8080");
        await Router.RouteRequestAsync(target, request);

        Assert.NotNull(captured);
        Assert.Equal("my-payload", captured!.Payload);
        Assert.Equal("10.0.0.1:8080", captured.Origin);
    }
}
