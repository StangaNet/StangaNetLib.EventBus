# StangaNetLib.EventBus

[![.NET CI](https://github.com/StangaNet/StangaNetLib.EventBus/actions/workflows/main.yml/badge.svg)](https://github.com/StangaNet/StangaNetLib.EventBus/actions/workflows/main.yml)
![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4)

Publish/subscribe integration event bus with **InMemory** and **RabbitMQ** backends, plus optional event→job triggering via [`StangaNetLib.Jobs`](https://github.com/StangaNet/StangaNetLib.Jobs). Failures surface as `Result` from [`StangaNetLib.Core`](https://github.com/StangaNet/StangaNetLib.Core); optional per-event-type serialization uses `IKeyedLock` from [`StangaNetLib.Concurrency`](https://github.com/StangaNet/StangaNetLib.Concurrency).

## Installation

The package is hosted on **GitHub Packages** (pulls Core / Concurrency / Jobs from the same feed).

```xml
<!-- NuGet.config — source key must match your packageSourceCredentials -->
<configuration>
  <packageSources>
    <add key="StangaNet" value="https://nuget.pkg.github.com/StangaNet/index.json" />
  </packageSources>
</configuration>
```

```xml
<!-- .csproj -->
<PackageReference Include="StangaNetLib.EventBus" Version="1.0.0" />
```

See [CHANGELOG.md](./CHANGELOG.md) for release history.

**Dependencies**: `StangaNetLib.Core` 1.0.0, `StangaNetLib.Concurrency` 1.0.0, `StangaNetLib.Jobs` 1.0.0, `RabbitMQ.Client` 7.x, `Microsoft.Extensions.*` 8.x.

## Contents

| Namespace | Types | Purpose |
|---|---|---|
| `StangaNetLib.EventBus.Events` | `IntegrationEvent` | Base class for domain events |
| `StangaNetLib.EventBus.Abstractions` | `IEventBus`, `IEventHandler<TEvent>`, `IEventBusSubscriptionManager`, `IEventTriggeredJobHandler` | Contracts |
| `StangaNetLib.EventBus.Errors` | `EventBusErrors` | Well-known failures |
| `StangaNetLib.EventBus.Settings` | `EventBusSettings`, `RabbitMqSettings` | Configuration |
| `StangaNetLib.EventBus.Extensions` | `AddStangaNetLibEventBus`, `AddStangaNetLibEventBusWithRabbitMq`, `AddEventTriggeredJob` | DI |

## Quick Start

### Define an event and handler

```csharp
public sealed class OrderPlacedEvent : IntegrationEvent
{
    public string OrderId { get; }
    public OrderPlacedEvent(string orderId) => OrderId = orderId;
}

public sealed class SendOrderConfirmationHandler : IEventHandler<OrderPlacedEvent>
{
    public Task<Result> HandleAsync(OrderPlacedEvent @event, CancellationToken cancellationToken)
        => Task.FromResult(Result.Success());
}
```

### Wire up (in-memory)

```csharp
builder.Services.AddStangaNetLibEventBus(builder.Configuration);
builder.Services.AddTransient<SendOrderConfirmationHandler>();

var app = builder.Build();
app.Services.GetRequiredService<IEventBus>()
   .Subscribe<OrderPlacedEvent, SendOrderConfirmationHandler>();
```

Optional keyed-lock serialization (same event type):

```csharp
services.AddStangaNetLibConcurrency();
services.AddStangaNetLibEventBus();
```

### RabbitMQ

```csharp
builder.Services.AddStangaNetLibEventBusWithRabbitMq(builder.Configuration);
// Subscribe before the host starts so bindings are created during StartAsync
```

### Event-triggered jobs

```csharp
services.AddStangaNetLibJobs();
services.AddStangaNetLibEventBus();
services.AddEventTriggeredJob<OrderPlacedEvent, ProcessOrderJob, ProcessOrderArgs>();
```

## License

Apache-2.0 — see [LICENSE](./LICENSE) and [NOTICE](./NOTICE).
