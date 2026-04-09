# SocketApi

A lightweight TCP/SSL message broker for .NET — combining RPC and Pub/Sub in a single binary protocol.

## What is it for?

SocketApi lets .NET applications exchange messages over persistent TCP/SSL connections using a pipe-separated protocol encoded with MessagePack. One connection handles both RPC (request → response) and Pub/Sub (publish → push to subscribers).

**Use cases**: IoT edge gateways, real-time notifications, microservice IPC, command & control, prototyping event-driven systems — anywhere you need low-latency bidirectional messaging without HTTP overhead or external infrastructure.

## Dependencies

| Package | Version |
|---------|---------|
| MessagePack | 3.1.3 |
| Microsoft.Extensions.Hosting.Abstractions | 8.0.1 |
| Microsoft.Extensions.Logging.Abstractions | 8.0.2 |

## Protocol

```
operation|target|payload|messageId|qos
```

8 built-in operations: `Call`, `Pub`, `Sub`, `UnSub`, `Ack`, `Heartbeat`, `Info`, `Ping`.
MQTT-style wildcard topics: `*` (single level), `#` (multi level).
QoS 0 (fire-and-forget) and QoS 1 (at-least-once with retry backoff).

## Quick start

```csharp
var host = Host.CreateDefaultBuilder(args)
    .AddSocketApi(port: 8443, certificate: new X509Certificate2("cert.pfx", "password"))
    .Build();

Router.Operation("login", request =>
    string.Equals("admin:secret", request?.Payload)
        ? Task.FromResult(OperationResult.Ok("Logged in"))
        : Task.FromResult(OperationResult.Ko("Invalid credentials")));

await host.RunAsync();
```

## Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `port` | `int` | — | TCP port to listen on |
| `certificate` | `X509Certificate2` | — | TLS certificate |
| `maxRequestLength` | `long` | 1 MB | Maximum request size |
| `maxResponseLength` | `long` | 1 MB | Maximum response size |
| `backlog` | `int` | 100 | TCP listen queue length |
| `heartbeatTimeoutSeconds` | `int` | 30 | Stale connection timeout |

## API

### OperationRequest

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `Operation` | Operation enum value |
| `Target` | `string` | Handler name or topic |
| `Payload` | `string?` | Request data |
| `Origin` | `string?` | Connection ID (set by server) |
| `MessageId` | `string?` | Message ID for QoS ack |
| `Qos` | `int` | Quality of service level |

### OperationResult

| Property | Type | Description |
|----------|------|-------------|
| `Success` | `bool` | Whether the operation succeeded |
| `Payload` | `object?` | Response data |

Errors are caught automatically — failed operations return `OperationResult.Ko` with the exception.
