namespace SocketApi;

public class Subscription
{
    public Guid Id { get; init; }
    public Guid ConnectionId { get; init; }
    public int Qos { get; init; }
}