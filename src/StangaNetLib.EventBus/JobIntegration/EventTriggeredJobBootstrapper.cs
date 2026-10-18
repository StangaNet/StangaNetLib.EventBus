using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StangaNetLib.EventBus.Abstractions;

namespace StangaNetLib.EventBus.JobIntegration;

/// <summary>
/// Hosted service that applies all <c>AddEventTriggeredJob</c> subscriptions to the <see cref="IEventBus"/>
/// during host startup, before any messages are processed.
/// </summary>
internal sealed class EventTriggeredJobBootstrapper : IHostedService
{
    private readonly IEventBus _bus;
    private readonly EventTriggeredJobRegistry _registry;

    public EventTriggeredJobBootstrapper(IEventBus bus, IOptions<EventTriggeredJobRegistry> registry)
    {
        _bus = bus;
        _registry = registry.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var subscribe in _registry.Subscriptions)
            subscribe(_bus);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
