namespace SocketApi.Server;

public static class Router
{
    private static readonly Dictionary<string, Func<OperationRequest?, Func<OperationResult, Task>, Task>> Operations = new();

    public static void RegisterOperation(string operation, Func<OperationRequest?, Func<OperationResult, Task>, Task> action)
    {
        Operations[operation] = action;
    }

    public static async Task RouteRequestAsync(string operation, OperationRequest request, Func<OperationResult, Task> response)
    {
        if (Operations.TryGetValue(operation, out var action))
        {
            try
            {
                await action(request, response);
            }
            catch (Exception ex)
            {
                await response(new OperationResult() { Success = false, Content = ex.Message });
            }
        }
        else
        {
            await response(new OperationResult() { Success = false, Content = $"Operation {operation} not found." });
        }
    }
}