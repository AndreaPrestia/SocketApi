namespace SocketApi;

public class OperationRequest
{
    public Operation Name { get; }
    public string Target { get; }
    public string? Payload { get; }
    public string? Origin { get; }

    private OperationRequest(Operation name, string target, string? payload, string? origin)
    {
        Name = name;
        Target = target;
        Payload = payload;
        Origin = origin;
    }

    internal static OperationRequest From(Operation name, string target, string? payload, string? origin = null) => new(name, target, payload, origin);
}