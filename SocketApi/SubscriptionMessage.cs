using MessagePack;

namespace SocketApi;

[MessagePackObject(AllowPrivate = true)]
public class SubscriptionMessage
{
    [Key(0)] public string MessageId { get; }
    [Key(1)] public string Topic { get; }
    [Key(2)] public string? Payload { get; }
    [Key(3)] public int Qos { get; }

    internal SubscriptionMessage(string messageId, string topic, string? payload, int qos)
    {
        MessageId = messageId;
        Topic = topic;
        Payload = payload;
        Qos = qos;
    }
}
