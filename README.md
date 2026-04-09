# SocketApi

A lightweight TCP/SSL message broker for .NET — combining RPC and Pub/Sub in a single binary protocol.

## What is it for?

SocketApi is a minimal server library that lets .NET applications exchange messages over persistent TCP/SSL connections using a simple pipe-separated protocol encoded with MessagePack.

It covers two patterns in one connection:

- **RPC** (request → response): register a handler, call it by name, get a result back
- **Pub/Sub** (publish → push): subscribe to topics with MQTT-style wildcards, receive messages pushed by the server

This makes it useful when you need real-time bidirectional communication without the overhead of HTTP, gRPC or a full message broker like RabbitMQ.

### Concrete use cases

- **IoT edge gateway** — sensors publish telemetry to hierarchical topics (`sensors/building-a/floor-2/temperature`), applications subscribe with wildcards (`sensors/building-a/#`). QoS 1 guarantees delivery, heartbeat detects offline devices
- **Real-time notifications** — a backend publishes events, dashboards and mobile apps receive push messages on the same persistent connection
- **Microservice IPC** — RPC for synchronous request-response, Pub/Sub for async events, one TCP connection for both
- **Command & control** — send commands to remote agents with reliable delivery (QoS 1 with retry backoff)
- **Prototyping** — a working message broker in one line of configuration, zero external infrastructure

### What it's not for

- Horizontal scaling (single instance, no clustering)
- Large file transfers (1 MB default message limit)
- Exactly-once delivery (QoS 1 = at-least-once)
- Multi-tenant isolation

## Requirements

- [.NET 8](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8/overview)
- An X.509 certificate for TLS

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| MessagePack | 3.1.3 | Binary serialization |
| Microsoft.Extensions.Hosting.Abstractions | 8.0.1 | `IHostedService` integration |
| Microsoft.Extensions.Logging.Abstractions | 8.0.2 | Logging |

## Protocol

Binary MessagePack over TCP/TLS 1.2. Pipe-separated fields:

```
operation|target|payload|messageId|qos
```

| Field | Required | Description |
|-------|----------|-------------|
| `operation` | Yes | `Call`, `Pub`, `Sub`, `UnSub`, `Ack`, `Heartbeat`, `Info`, `Ping` |
| `target` | Yes | Handler name (RPC) or topic (Pub/Sub) |
| `payload` | No | Operation-specific data |
| `messageId` | No | UUID for QoS acknowledgment |
| `qos` | No | `0` = fire-and-forget (default), `1` = at-least-once with retry |

Fields 4-5 are optional for backward compatibility.

### Topic wildcards (MQTT-style)

| Pattern | Matches |
|---------|---------|
| `sensors/temperature` | Exact match only |
| `sensors/*` | `sensors/temperature`, `sensors/humidity` (single level) |
| `sensors/#` | `sensors/temperature`, `sensors/floor-1/humidity` (all levels) |

## Quick start

```csharp
var host = Host.CreateDefaultBuilder(args)
    .AddSocketApi(port: 8443, certificate: new X509Certificate2("cert.pfx", "password"))
    .Build();

// Register an RPC handler
Router.Operation("login", request =>
    string.Equals("admin:secret", request?.Payload)
        ? Task.FromResult(OperationResult.Ok("Logged in"))
        : Task.FromResult(OperationResult.Ko("Invalid credentials")));

await host.RunAsync();
```

### Configuration parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `port` | `int` | — | TCP port to listen on |
| `certificate` | `X509Certificate2` | — | TLS certificate |
| `maxRequestLength` | `long` | 1 MB | Maximum request size in bytes |
| `maxResponseLength` | `long` | 1 MB | Maximum response size in bytes |
| `backlog` | `int` | 100 | TCP listen queue length |
| `heartbeatTimeoutSeconds` | `int` | 30 | Seconds before a connection without heartbeat is removed |

## Architecture

```
Client TCP/SSL ──► AcceptLoop ──► HandleClientAsync (persistent connection)
                                      │
                                ParseCustomProtocol
                                      │
                     ┌────────────────┼──────────────┐
                     ▼                ▼              ▼
                   Call            Pub/Sub       Heartbeat
                     │                │
                  Router       PubSubManager
                                      │
                              TopicMatcher (wildcards)
                                      │
                              QoS 0: fire-and-forget
                              QoS 1: retry with backoff
```

- **Persistent connections**: one TCP/SSL connection handles multiple operations in a loop
- **Thread-safe writes**: each connection uses a `SemaphoreSlim` to serialize SSL writes
- **Graceful shutdown**: drains active connections with a 10-second timeout before force-closing
- **Heartbeat**: background task removes stale connections after configurable timeout

## API reference

### OperationRequest

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `Operation` | The operation enum value |
| `Target` | `string` | Handler name or topic |
| `Payload` | `string?` | Request data |
| `Origin` | `string?` | Connection ID (set by server) |
| `MessageId` | `string?` | Message ID for QoS acknowledgment |
| `Qos` | `int` | Quality of service level |

### OperationResult

| Property | Type | Description |
|----------|------|-------------|
| `Success` | `bool` | Whether the operation succeeded |
| `Payload` | `object?` | Response data |

Errors are caught automatically — failed operations return `OperationResult.Ko` with the exception in `Payload`.

## Tests

```bash
dotnet test SocketApi.Tests --filter "FullyQualifiedName!~TcpSslServerTests"
```

80 unit tests covering all components. Integration tests (`TcpSslServerTests`) require a valid certificate file.

## License

See [LICENSE.txt](LICENSE.txt).
