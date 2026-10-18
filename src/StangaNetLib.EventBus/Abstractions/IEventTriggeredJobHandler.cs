using StangaNetLib.EventBus.Events;

namespace StangaNetLib.EventBus.Abstractions;

/// <summary>
/// Marker interface identifying an <see cref="IEventHandler{TEvent}"/> that responds to an integration event
/// by enqueuing a background job. Use
/// <c>AddEventTriggeredJob&lt;TEvent, TJob, TArgs&gt;</c> to register implementations automatically via DI.
/// </summary>
/// <typeparam name="TEvent">The integration event type that triggers the background job.</typeparam>
public interface IEventTriggeredJobHandler<TEvent>
    where TEvent : IntegrationEvent
{
}
