# SocketApi

A light-weight communication protocol and server written over TCP totally written in .NET.

The **SocketApi** project contains the logic for communication on a simple binary protocol that uses **MessagePack** as encoding.

**Requirements**

- [.NET 8](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8/overview)
- [MessagePack](https://msgpack.org/)
- [mTLS](https://www.cloudflare.com/learning/access-management/what-is-mutual-tls/)

**Dependencies**

- Microsoft.Extensions.Hosting.Abstractions 8.0.1
- MessagePack 3.1.3

**Protocol**

It is very simple and runs on top of TCP over SSL.

```
operation|this is the payload to sent
```

It is pipe (**|**) separated and is composed of **operation** and eventual **payload**. Nothing less, nothing more.

**Authentication**

The authentication is made with mTLS. Obviously every communication must be processed over SSL.

**Usage of SocketApi**

Code example:

```
var builder = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Debug);
    })
    .AddSocketApi(443, new X5092Certificate("certPath", "certPassword"), 1024 * 1024 * 1024, 1024 * 1024 * 1024, 100);

var host = builder.Build();

await host.RunAsync();
```

The example above initialize the **SocketApi** server in a console application, with console logging provider using the method **AddSocketApi**.

The parameters of the method are:

| Parameter         | Type             | Context                                                      |
|-------------------|------------------|--------------------------------------------------------------|
| port              | int              | The port where the application should listen.                |
| certificate       | X5092Certificate | It's the certificate to use.                                 |
| maxRequestLength  | long             | The maximum length of the request. Default at 1024 * 1024.  |
| maxResponseLength | long             | The maximum length of the response. Default at 1024 * 1024. |
| backlog           | int              | The maximum length of the connections queue. Default at 100. |

**Expose an operation**

To expose an operation the **Router** class must be used.

```
Router.Operation("submit", request =>
{
    if (request != null)
    {
        return Task.FromResult(OperationResult.Ok($"Data submitted: {request.Content}"));
    }

    return Task.FromResult(OperationResult.Ko("No data provided"));
});
```
The example above exposes a **submit** operation and returns an **OperationResult.Ok**.

***Classes***

****OperationRequest****

This class represents the request context.
 
| Property  | Type   | Context                                              |
|-----------|--------|------------------------------------------------------|
| Operation | string | Contains the operation name.                         |
| Content   | string | Contains the eventual payload sent to the operation. |
| Origin    | string | Contains the IP address of the caller.               |

****OperationResponse****

This class represents the response context.

| Property | Type   | Context                                                                |
|----------|--------|------------------------------------------------------------------------|
| Operation | string | Contains the operation name.                                           |
| Success  | bool   | Contains the informations if an operation has been successfull or not. |
| Content  | object | Contains the eventual content for the operation fullfillment.          |

***Error management***

Every error that will occur is intercepted by default from **SocketApi** 
and will be returned an **OperationResponse** containing the **Success** property set to false 
and **Content** one populated with the **Exception** occurred.

| Property  | Type   | Context                                              |
|-----------|--------|------------------------------------------------------|
| Operation | string | Contains the operation name.                         |
| Content   | string | Contains the eventual payload sent to the operation. |
| Origin    | string | Contains the IP address of the caller.               |

**How can I test it?**

This repository provides a test project where you can find tests for **SocketApi**.

The project name is **SockerApi.Tests**.
