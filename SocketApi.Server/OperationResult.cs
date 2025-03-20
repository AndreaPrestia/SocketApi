using MessagePack;

namespace SocketApi.Server;

[MessagePackObject]
public class OperationResult
{
    [Key(0)] public bool Success { get; set; }
    [Key(2)] public object? Content { get; set; }
}