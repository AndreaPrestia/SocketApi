using System.Reflection;

namespace SocketApi;

public class CommandManager(PubSubManager manager)
{
    public async Task<OperationResult> ManageAsync(OperationRequest request)
    {
        return request.Name switch
        {
            Operation.Call => await Router.RouteRequestAsync(request.Target, request),
            Operation.Pub => await PublishAsync(request),
            Operation.Sub => Subscribe(request),
            Operation.UnSub => UnSubscribe(request),
            Operation.Ack => Acknowledge(request),
            Operation.Heartbeat => OperationResult.Ok("OK"),
            Operation.Info => OperationResult.Ok(
                $"SocketApi running on version: v.{Assembly.GetExecutingAssembly().GetName().Version}"),
            Operation.Ping => OperationResult.Ok("PONG"),
            _ => OperationResult.Ko($"Unknown operation '{request.Name}'")
        };
    }

    private async Task<OperationResult> PublishAsync(OperationRequest request)
    {
        var result = await manager.PublishAsync(request.Target, request.Payload);
        return OperationResult.Ok(result);
    }

    private OperationResult Subscribe(OperationRequest request)
    {
        var connectionIdParsed = Guid.TryParse(request.Origin, out var connectionId);

        if (!connectionIdParsed)
        {
            return OperationResult.Ko("Invalid connection id");
        }

        var result = manager.Subscribe(request.Target, connectionId, request.Qos);
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

    private OperationResult Acknowledge(OperationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            return OperationResult.Ko("Missing message id");
        }

        manager.AcknowledgeMessage(request.MessageId);
        return OperationResult.Ok("OK");
    }
}