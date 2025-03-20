namespace SocketApi;

public static class Router
{
    private static readonly Dictionary<string, Func<OperationRequest?, Task<OperationResult>>> Operations = new();

    public static void Operation(string operation, Func<OperationRequest?, Task<OperationResult>> action)
    {
        Operations[operation] = action;
    }

    internal static async Task<OperationResult> RouteRequestAsync(string operation, OperationRequest request)
    {
        if (Operations.TryGetValue(operation, out var action))
        {
            try
            {
                return await action(request);
            }
            catch (Exception ex)
            {
                return OperationResult.Ko(ex);
            }
        }

        return OperationResult.Ko($"Operation {operation} not found.");
    }
}