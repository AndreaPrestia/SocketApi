namespace SocketApi;

public class OperationRequest
{
    public Operation Name { get; }
    public string Target { get; }
    public string? Payload { get; }
    public string? Origin { get; }
    public string? MessageId { get; }
    public int Qos { get; }

    private OperationRequest(Operation name, string target, string? payload, string? origin, string? messageId = null, int qos = 0)
    {
        Name = name;
        Target = target;
        Payload = payload;
        Origin = origin;
        MessageId = messageId;
        Qos = qos;
    }

    internal static OperationRequest From(Operation name, string target, string? payload, string? origin = null, string? messageId = null, int qos = 0)
        => new(name, target, payload, origin, messageId, qos);
}