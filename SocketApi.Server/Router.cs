namespace SocketApi.Server;

public static class Router
{
    private static readonly Dictionary<string, Func<Dictionary<string, string>, string, Func<string, Task>, Task>> Routes = new();

    public static void RegisterRoute(string route, Func<Dictionary<string, string>, string, Func<string, Task>, Task> action)
    {
        Routes[route] = action;
    }

    public static async Task RouteRequestAsync(string route, Dictionary<string, string> parameters, string body, Func<string, Task> writeResponse)
    {
        if (Routes.TryGetValue(route, out var action))
        {
            await action(parameters, body, writeResponse);
        }
        else
        {
           await writeResponse("Not Found");
        }
    }
}