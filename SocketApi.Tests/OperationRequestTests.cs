namespace SocketApi.Tests;

public class OperationRequestTests
{
    [Fact]
    public void From_SetsAllProperties()
    {
        var request = OperationRequest.From(Operation.Call, "login", "data", "127.0.0.1:5000");

        Assert.Equal(Operation.Call, request.Name);
        Assert.Equal("login", request.Target);
        Assert.Equal("data", request.Payload);
        Assert.Equal("127.0.0.1:5000", request.Origin);
    }

    [Fact]
    public void From_WithNullPayload_SetsPayloadNull()
    {
        var request = OperationRequest.From(Operation.Ping, "target", null);

        Assert.Equal(Operation.Ping, request.Name);
        Assert.Equal("target", request.Target);
        Assert.Null(request.Payload);
        Assert.Null(request.Origin);
    }

    [Fact]
    public void From_WithoutOrigin_DefaultsToNull()
    {
        var request = OperationRequest.From(Operation.Sub, "events", "payload");

        Assert.Null(request.Origin);
    }

    [Theory]
    [InlineData(Operation.Call)]
    [InlineData(Operation.Pub)]
    [InlineData(Operation.Sub)]
    [InlineData(Operation.UnSub)]
    [InlineData(Operation.Info)]
    [InlineData(Operation.Ping)]
    public void From_AllOperationTypes_ArePreserved(Operation operation)
    {
        var request = OperationRequest.From(operation, "t", "p");

        Assert.Equal(operation, request.Name);
    }
}
