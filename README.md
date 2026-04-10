# SocketApi

A lightweight TCP/SSL message broker built in .NET — combining RPC and Pub/Sub in a single binary protocol. Clients can be written in any language that supports TCP sockets and MessagePack.

## What is it for?

SocketApi is a minimal server library built in .NET that exposes a language-agnostic protocol over persistent TCP/SSL connections. Any client — Python, Node.js, Go, Rust, Java, C++ — can connect using a simple pipe-separated text protocol and MessagePack for binary serialization.

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

### RPC (request → response)

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

Client sends `Call|login|admin:secret` → receives `{Success: true, Payload: "Logged in"}`.

### Pub/Sub (publish → push)

Pub/Sub is entirely protocol-driven — clients subscribe and publish via the same connection:

```
Client A:  Sub|sensors/temperature||msg-001|1     → subscribes with QoS 1
Client B:  Pub|sensors/temperature|22.5           → publishes to topic
Client A:  ← receives push {MessageId, Topic: "sensors/temperature", Payload: "22.5", Qos: 1}
Client A:  Ack|<messageId>                        → acknowledges delivery (QoS 1)
```

Wildcard subscriptions work the same way:

```
Client A:  Sub|sensors/#                          → subscribes to all sensor topics (QoS 0)
Client B:  Pub|sensors/floor-1/humidity|65        → publishes
Client A:  ← receives push for "sensors/floor-1/humidity"
```

To unsubscribe, the client sends `UnSub|sensors/#|<subscriptionId>` using the ID returned by `Sub`.

### Configuration parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `port` | `int` | — | TCP port to listen on |
| `certificate` | `X509Certificate2` | — | TLS certificate |
| `maxRequestLength` | `long` | 1 MB | Maximum request size in bytes |
| `maxResponseLength` | `long` | 1 MB | Maximum response size in bytes |
| `backlog` | `int` | 100 | TCP listen queue length |
| `heartbeatTimeoutSeconds` | `int` | 30 | Seconds before a connection without heartbeat is removed |

## Client examples

The protocol is language-agnostic. Any client that speaks TCP/TLS and MessagePack can connect. Requests are MessagePack-encoded strings with pipe-separated fields; responses are MessagePack maps.

### Python

```bash
pip install msgpack
```

```python
import socket, ssl, msgpack

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE  # dev only — use proper CA in production

sock = ctx.wrap_socket(
    socket.create_connection(("localhost", 8443)),
    server_hostname="localhost")

unpacker = msgpack.Unpacker(raw=False)

# RPC call
sock.sendall(msgpack.packb("Call|login|admin:secret"))
unpacker.feed(sock.recv(4096))
for response in unpacker:
    print(response)  # {0: True, 1: "Logged in"}

# Subscribe to a topic with wildcard
sock.sendall(msgpack.packb("Sub|sensors/#||msg-001|1"))
unpacker.feed(sock.recv(4096))
for response in unpacker:
    print(response)  # {0: True, 1: "Subscribed"}

# Receive push messages
while True:
    unpacker.feed(sock.recv(4096))
    for msg in unpacker:
        if isinstance(msg[0], str):  # SubscriptionMessage
            print(f"Topic: {msg[1]}, Payload: {msg[2]}")
            if msg[3] > 0:  # QoS 1 → send Ack
                sock.sendall(msgpack.packb(f"Ack|{msg[0]}"))
                unpacker.feed(sock.recv(4096))
```

### Node.js

```bash
npm install @msgpack/msgpack
```

```javascript
const tls = require("tls");
const { encode, Decoder } = require("@msgpack/msgpack");

const sock = tls.connect(8443, "localhost", { rejectUnauthorized: false });
const decoder = new Decoder();

sock.on("secureConnect", () => {
  // RPC call
  sock.write(Buffer.from(encode("Call|login|admin:secret")));

  // Subscribe to a topic with wildcard
  setTimeout(() => {
    sock.write(Buffer.from(encode("Sub|sensors/#||msg-001|1")));
  }, 100);
});

sock.on("data", (chunk) => {
  const msg = decoder.decode(chunk);
  if (typeof msg[0] === "boolean") {
    console.log("RPC:", msg);        // { 0: true, 1: "Logged in" }
  } else {
    console.log("Push:", msg);       // { 0: msgId, 1: topic, 2: payload, 3: qos }
    if (msg[3] > 0)
      sock.write(Buffer.from(encode(`Ack|${msg[0]}`)));
  }
});
```

### Go

```bash
go get github.com/vmihailenco/msgpack/v5
```

```go
package main

import (
    "crypto/tls"
    "fmt"
    "github.com/vmihailenco/msgpack/v5"
)

func main() {
    conn, _ := tls.Dial("tcp", "localhost:8443",
        &tls.Config{InsecureSkipVerify: true})
    defer conn.Close()

    // Send RPC
    req, _ := msgpack.Marshal("Call|login|admin:secret")
    conn.Write(req)

    // Read response
    dec := msgpack.NewDecoder(conn)
    var resp map[int]interface{}
    dec.Decode(&resp)
    fmt.Println(resp) // map[0:true 1:Logged in]

    // Subscribe
    sub, _ := msgpack.Marshal("Sub|sensors/#||msg-001|1")
    conn.Write(sub)
    dec.Decode(&resp)

    // Receive push messages
    for {
        var msg map[int]interface{}
        dec.Decode(&msg)
        fmt.Printf("Topic: %s, Payload: %s\n", msg[1], msg[2])
        if qos, ok := msg[3].(int8); ok && qos > 0 {
            ack, _ := msgpack.Marshal(fmt.Sprintf("Ack|%s", msg[0]))
            conn.Write(ack)
            dec.Decode(&resp)
        }
    }
}
```

### C

```bash
# Dependencies: OpenSSL, msgpack-c
# Debian/Ubuntu: apt install libssl-dev libmsgpack-dev
# macOS: brew install openssl msgpack-c
```

```c
#include <openssl/ssl.h>
#include <msgpack.h>
#include <string.h>
#include <unistd.h>
#include <netdb.h>

static void send_request(SSL *ssl, const char *text) {
    msgpack_sbuffer sbuf;
    msgpack_packer pk;
    msgpack_sbuffer_init(&sbuf);
    msgpack_packer_init(&pk, &sbuf, msgpack_sbuffer_write);
    msgpack_pack_str(&pk, strlen(text));
    msgpack_pack_str_body(&pk, text, strlen(text));
    SSL_write(ssl, sbuf.data, sbuf.size);
    msgpack_sbuffer_destroy(&sbuf);
}

int main(void) {
    SSL_CTX *ctx = SSL_CTX_new(TLS_client_method());
    int fd = socket(AF_INET, SOCK_STREAM, 0);

    struct hostent *host = gethostbyname("localhost");
    struct sockaddr_in addr = {
        .sin_family = AF_INET,
        .sin_port = htons(8443),
    };
    memcpy(&addr.sin_addr, host->h_addr, host->h_length);
    connect(fd, (struct sockaddr *)&addr, sizeof(addr));

    SSL *ssl = SSL_new(ctx);
    SSL_set_fd(ssl, fd);
    SSL_connect(ssl);

    /* RPC call */
    send_request(ssl, "Call|login|admin:secret");

    /* Read response */
    char buf[4096];
    int n = SSL_read(ssl, buf, sizeof(buf));
    msgpack_unpacked result;
    msgpack_unpacked_init(&result);
    msgpack_unpack_next(&result, buf, n, NULL);
    msgpack_object_print(stdout, result.data);  /* {0: true, 1: "Logged in"} */
    printf("\n");
    msgpack_unpacked_destroy(&result);

    /* Subscribe to a topic */
    send_request(ssl, "Sub|sensors/#||msg-001|1");
    n = SSL_read(ssl, buf, sizeof(buf));
    /* Read sub confirmation... */

    /* Receive push messages */
    while ((n = SSL_read(ssl, buf, sizeof(buf))) > 0) {
        msgpack_unpacked msg;
        msgpack_unpacked_init(&msg);
        msgpack_unpack_next(&msg, buf, n, NULL);
        msgpack_object_print(stdout, msg.data);
        printf("\n");
        /* If QoS > 0, send Ack with the MessageId */
        msgpack_unpacked_destroy(&msg);
    }

    SSL_shutdown(ssl);
    SSL_free(ssl);
    close(fd);
    SSL_CTX_free(ctx);
    return 0;
}
```

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
