namespace StangaNetLib.EventBus.Events;

/// <summary>
/// Base class for all integration events published through the <see cref="Abstractions.IEventBus"/>.
/// Subclass this to define domain events (e.g. <c>OrderPlacedEvent</c>, <c>UserRegisteredEvent</c>).
/// </summary>
public abstract class IntegrationEvent
{
    /// <summary>Unique identifier assigned when the event instance is created.</summary>
    public Guid EventId { get; }

    /// <summary>UTC timestamp at which the event was created.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Initialises a new integration event with a fresh <see cref="Guid"/> and the current UTC time.</summary>
    protected IntegrationEvent()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Initialises a new integration event with explicit values.
    /// Use this overload when reconstructing events from persistent storage or transport.
    /// </summary>
    /// <param name="eventId">The event identifier to assign.</param>
    /// <param name="occurredAt">The UTC timestamp at which the event originally occurred.</param>
    protected IntegrationEvent(Guid eventId, DateTimeOffset occurredAt)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
    }
}
