using MessagePack;

namespace SocketApi;

[MessagePackObject(AllowPrivate = true)]
public class OperationResult
{
    [Key(0)] public bool Success { get; }
    [Key(1)] public object? Payload { get; }

    internal OperationResult(bool success, object? payload)
    {
        Success = success;
        Payload = payload;
    }
    
    public static OperationResult Ok(object? payload) => new(true, payload);
    
    public static OperationResult Ko(object? payload) => new(false, payload);
}