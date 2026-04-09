using System.Reflection;

namespace SocketApi;

public class CommandManager(PubSubManager manager)
{
    public async Task<OperationResult> ManageAsync(OperationRequest request)
    {
        return request.Name switch
        {
            Operation.Call => await Router.RouteRequestAsync(request.Target, request),
            Operation.Pub => Publish(request),
            Operation.Sub => Subscribe(request),
            Operation.UnSub => UnSubscribe(request),
            Operation.Info => OperationResult.Ok(
                $"SocketApi running on version: v.{Assembly.GetExecutingAssembly().GetName().Version}"),
            Operation.Ping => OperationResult.Ok("PONG"),
            _ => OperationResult.Ko($"Unknown operation '{request.Name}'")
        };
    }

    private OperationResult Publish(OperationRequest request)
    {
        var result = manager.Publish(request.Target, request.Payload);
        return result != -1 ? OperationResult.Ok(result) : OperationResult.Ko(result);
    }

    private OperationResult Subscribe(OperationRequest request)
    {
        var splitOrigin = request.Origin?.Split(':');

        if (splitOrigin is not { Length: 2 })
        {
            return OperationResult.Ko("Invalid origin");
        } 
        
        var ipAddress = splitOrigin[0];
        
        var portParsed = int.TryParse(splitOrigin[1], out var port);

        if (!portParsed)
        {
            return OperationResult.Ko("Invalid port");
        }
        
        var result = manager.Subscribe(request.Target, ipAddress, port);
        
        return OperationResult.Ok(result);
    }

    private OperationResult UnSubscribe(OperationRequest request)
    {
        var parsed = Guid.TryParse(request.Payload, out var id);

        if (!parsed)
        {
            return OperationResult.Ko($"Invalid id '{request.Payload}'");
        }

        var unsubResult = manager.UnSubscribe(id, request.Target);

        return unsubResult ? OperationResult.Ok("OK") : OperationResult.Ko("KO");
    }
}