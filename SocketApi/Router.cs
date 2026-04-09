using System.Collections.Concurrent;

namespace SocketApi;

public static class Router
{
    private static readonly ConcurrentDictionary<string, Func<OperationRequest?, Task<OperationResult>>> Targets = new();

    public static void Operation(string target, Func<OperationRequest?, Task<OperationResult>> action)
    {
        Targets[target] = action;
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