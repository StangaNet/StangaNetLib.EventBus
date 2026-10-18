using StangaNetLib.EventBus.Abstractions;

namespace StangaNetLib.EventBus.JobIntegration;

/// <summary>
/// Holds subscription actions collected by <c>AddEventTriggeredJob</c> calls.
/// Applied to the <see cref="IEventBus"/> by <see cref="EventTriggeredJobBootstrapper"/> at host startup.
/// </summary>
internal sealed class EventTriggeredJobRegistry
{
    public List<Action<IEventBus>> Subscriptions { get; } = [];
}
