using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using MessagePack;

namespace SocketApi;

public class PubSubManager
{
    private static readonly ConcurrentDictionary<string, List<Subscription>> Subscriptions = new();

    public Guid Subscribe(string target, string ipAddress, int port)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("No target provided");
        }
        
        var parsedIp = IPAddress.TryParse(ipAddress, out var ip);

        if (!parsedIp)
        {
            throw new InvalidOperationException($"Invalid {ipAddress} ip");
        }

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            IpAddress = ip,
            Port = port
        };

        Subscriptions.AddOrUpdate(target,
            _ => [subscription],
            (_, list) =>
            {
                lock (list) list.Add(subscription);
                return list;
            });

        return subscription.Id;
    }

    public bool UnSubscribe(Guid id, string target)
    {
        if (!Subscriptions.TryGetValue(target, out var subscriptions))
            return false;

        lock (subscriptions)
        {
            var removed = subscriptions.RemoveAll(x => x.Id == id);
            return removed > 0;
        }
    }

    public int Publish(string target, string? payload)
    {
        if (string.IsNullOrWhiteSpace(target)) return 0;

        if (string.IsNullOrWhiteSpace(payload)) return 0;

        if (!Subscriptions.TryGetValue(target, out var subscriptions)) return 0;
        lock (subscriptions)
        {
            foreach (var subscription in subscriptions.ToList())
            {
                try
                {
                    if (subscription.IpAddress == null)
                    {
                        throw new InvalidOperationException($"Client {subscription.Id} has no IpAddress");
                    }

                    var message = OperationResult.Ok($"SUB|{target}|{payload}");
                    var responseBytes = MessagePackSerializer.Serialize(message);
                    SendMessageToClient(subscription, responseBytes);
                }
                catch
                {
                    subscriptions.Remove(subscription);
                }
            }

            return subscriptions.Count;
        }
    }

    private void SendMessageToClient(Subscription subscription, byte[] message)
    {
        using var tcpClient = new TcpClient();
        tcpClient.Connect(subscription.IpAddress!, subscription.Port);

        using var networkStream = tcpClient.GetStream();
        using var sslStream = new SslStream(networkStream, false);

        sslStream.AuthenticateAsClient(subscription.IpAddress!.ToString(), null, SslProtocols.Tls12,
            checkCertificateRevocation: true);

        sslStream.Write(message);
        sslStream.Flush();
    }
}