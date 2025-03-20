namespace SocketApi;

public class OperationRequest
{
    public string Name { get; }
    public string? Origin { get; }
    public string? Content { get; }

    private OperationRequest(string name, string? origin, string? content)
    {
        Name = name;
        Origin = origin;
        Content = content;
    }

    internal static OperationRequest From(string name, string? origin, string? content) => new(name, origin, content);
}