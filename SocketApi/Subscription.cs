using System.Net;
using System.Net.Sockets;

namespace SocketApi;

public class Subscription
{
    public Guid Id { get; init; }
    public IPAddress? IpAddress { get; init; }
    public int Port { get; init; }
}