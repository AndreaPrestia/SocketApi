using System.Collections.Concurrent;

namespace SocketApi;

public static class Router
{
    private static readonly ConcurrentDictionary<string, Func<OperationRequest?, Task<OperationResult>>> Targets = new();
    private static PubSubManager? _pubSubManager;

    internal static void SetPubSubManager(PubSubManager manager) => _pubSubManager = manager;

    public static void Operation(string target, Func<OperationRequest?, Task<OperationResult>> action)
    {
        Targets[target] = action;
    }

    public static Task<int> Publish(string topic, string? payload)
    {
        if (_pubSubManager is null)
            throw new InvalidOperationException("PubSubManager is not initialized. Ensure the server has started.");
        return _pubSubManager.PublishAsync(topic, payload);
    }

    internal static async Task<OperationResult> RouteRequestAsync(string target, OperationRequest request)
    {
        if (!Targets.TryGetValue(target, out var action))
            return OperationResult.Ko($"Target '{target}' not found.");
        try
        {
            return await action(request);
        }
        catch (Exception ex)
        {
            return OperationResult.Ko(ex);
        }
    }
}