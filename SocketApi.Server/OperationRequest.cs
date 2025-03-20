namespace SocketApi.Server;

public class OperationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Origin { get; set; }
    public string? Content { get; set; }
}