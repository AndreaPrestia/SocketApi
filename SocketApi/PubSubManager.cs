using System.Collections.Concurrent;
using MessagePack;

namespace SocketApi;

public class PubSubManager(IConnectionRegistry connectionRegistry)
{
    private readonly ConcurrentDictionary<string, List<Subscription>> _subscriptions = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingAcks = new();

    private const int MaxRetries = 3;
    private static readonly int[] RetryDelaysMs = [100, 200, 400, 800];

    public Guid Subscribe(string target, Guid connectionId, int qos = 0)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("No target provided");
        }

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            ConnectionId = connectionId,
            Qos = qos
        };

        _subscriptions.AddOrUpdate(target,
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
        if (!_subscriptions.TryGetValue(target, out var subscriptions))
            return false;

        lock (subscriptions)
        {
            var removed = subscriptions.RemoveAll(x => x.Id == id);
            return removed > 0;
        }
    }

    public async Task<int> PublishAsync(string target, string? payload)
    {
        if (string.IsNullOrWhiteSpace(target)) return 0;
        if (string.IsNullOrWhiteSpace(payload)) return 0;

        var matchingSubscriptions = GetMatchingSubscriptions(target);
        if (matchingSubscriptions.Count == 0) return 0;

        var delivered = 0;

        foreach (var subscription in matchingSubscriptions)
        {
            if (!connectionRegistry.TryGet(subscription.ConnectionId, out var connection) || connection == null)
            {
                RemoveSubscriptionById(subscription.Id);
                continue;
            }

            var messageId = Guid.NewGuid().ToString("N");
            var message = new SubscriptionMessage(messageId, target, payload, subscription.Qos);
            var messageBytes = MessagePackSerializer.Serialize(message);

            try
            {
                if (subscription.Qos == 0)
                {
                    await connection.WriteAsync(messageBytes);
                    delivered++;
                }
                else
                {
                    var acked = await DeliverWithRetryAsync(connection, messageBytes, messageId);
                    if (acked) delivered++;
                    else RemoveSubscriptionById(subscription.Id);
                }
            }
            catch
            {
                RemoveSubscriptionById(subscription.Id);
            }
        }

        return delivered;
    }

    public void AcknowledgeMessage(string messageId)
    {
        if (_pendingAcks.TryRemove(messageId, out var tcs))
        {
            tcs.TrySetResult(true);
        }
    }

    private async Task<bool> DeliverWithRetryAsync(ActiveConnection connection, byte[] messageBytes, string messageId)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingAcks[messageId] = tcs;

            try
            {
                await connection.WriteAsync(messageBytes);

                var delayMs = attempt < RetryDelaysMs.Length ? RetryDelaysMs[attempt] : RetryDelaysMs[^1];
                var acked = await Task.WhenAny(tcs.Task, Task.Delay(delayMs)) == tcs.Task;

                if (acked) return true;

                _pendingAcks.TryRemove(messageId, out _);
            }
            catch
            {
                _pendingAcks.TryRemove(messageId, out _);
                return false;
            }
        }

        return false;
    }

    private List<Subscription> GetMatchingSubscriptions(string topic)
    {
        var result = new List<Subscription>();

        foreach (var kvp in _subscriptions)
        {
            if (!TopicMatcher.Matches(kvp.Key, topic)) continue;

            lock (kvp.Value)
            {
                result.AddRange(kvp.Value);
            }
        }

        return result;
    }

    private void RemoveSubscriptionById(Guid subscriptionId)
    {
        foreach (var kvp in _subscriptions)
        {
            lock (kvp.Value)
            {
                kvp.Value.RemoveAll(s => s.Id == subscriptionId);
            }
        }
    }
}