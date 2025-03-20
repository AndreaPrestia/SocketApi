namespace SocketApi.Server;

public static class Router
{
    private static readonly Dictionary<string, Func<OperationRequest?, Task<OperationResult>>> Operations = new();

    public static void RegisterOperation(string operation, Func<OperationRequest?, Task<OperationResult>> action)
    {
        Operations[operation] = action;
    }

    public static async Task<OperationResult> RouteRequestAsync(string operation, OperationRequest request)
    {
        if (Operations.TryGetValue(operation, out var action))
        {
            try
            {
                return await action(request);
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Content = ex.Message };
            }
        }

        return new OperationResult { Success = false, Content = $"Operation {operation} not found." };
    }
}