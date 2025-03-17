namespace SocketApi.Server;

public static class Router
{
    private static readonly Dictionary<string, Func<string, Func<string, Task>, Task>> Operations = new();

    public static void RegisterOperation(string operation, Func<string, Func<string, Task>, Task> action)
    {
        Operations[operation] = action;
    }

    public static async Task RouteRequestAsync(string operation, string request, Func<string, Task> response)
    {
        if (Operations.TryGetValue(operation, out var action))
        {
            await action(request, response);
        }
        else
        {
           await response("Not Found");
        }
    }
}