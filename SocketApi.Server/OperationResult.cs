using MessagePack;

namespace SocketApi.Server;

[MessagePackObject(AllowPrivate = true)]
public class OperationResult
{
    [Key(0)] public bool Success { get; }
    [Key(1)] public object? Content { get; }

    internal OperationResult(bool success, object? content)
    {
        Success = success;
        Content = content;
    }
    
    public static OperationResult Ok(object? content) => new(true, content);
    
    public static OperationResult Ko(object? content) => new(false, content);
}