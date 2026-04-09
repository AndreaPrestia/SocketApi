namespace SocketApi.Tests;

public class OperationResultTests
{
    [Fact]
    public void Ok_SetsSuccessTrue_AndPayload()
    {
        var result = OperationResult.Ok("hello");

        Assert.True(result.Success);
        Assert.Equal("hello", result.Payload);
    }

    [Fact]
    public void Ko_SetsSuccessFalse_AndPayload()
    {
        var result = OperationResult.Ko("error");

        Assert.False(result.Success);
        Assert.Equal("error", result.Payload);
    }

    [Fact]
    public void Ok_WithNullPayload_ReturnsSuccessWithNull()
    {
        var result = OperationResult.Ok(null);

        Assert.True(result.Success);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void Ko_WithException_StoresException()
    {
        var ex = new InvalidOperationException("fail");
        var result = OperationResult.Ko(ex);

        Assert.False(result.Success);
        Assert.IsType<InvalidOperationException>(result.Payload);
    }

    [Fact]
    public void Ok_WithIntPayload_StoresInt()
    {
        var result = OperationResult.Ok(42);

        Assert.True(result.Success);
        Assert.Equal(42, result.Payload);
    }
}
