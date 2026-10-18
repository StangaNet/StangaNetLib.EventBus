using StangaNetLib.EventBus.Events;

namespace StangaNetLib.EventBus.Abstractions;

/// <summary>
/// Tracks event handler subscriptions on behalf of an <see cref="IEventBus"/> implementation.
/// The subscription manager is the source of truth for which handler types are registered per event type.
/// </summary>
public interface IEventBusSubscriptionManager
{
    /// <summary>
    /// Raised when the last registered handler for an event type is removed, supplying the event name.
    /// Implementations of <see cref="IEventBus"/> can use this to unbind transport-level routing.
    /// </summary>
    event EventHandler<string>? OnEventRemoved;

    /// <summary>Returns <c>true</c> when at least one handler is registered for <paramref name="eventName"/>.</summary>
    bool HasSubscriptionsForEvent(string eventName);

    /// <summary>Returns all handler types currently registered for <paramref name="eventName"/>.</summary>
    IReadOnlyList<Type> GetHandlerTypesForEvent(string eventName);

    /// <summary>
    /// Registers <typeparamref name="THandler"/> as a handler for events of type <typeparamref name="TEvent"/>.
    /// Duplicate registrations of the same handler type are silently ignored.
    /// </summary>
    void AddSubscription<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>;

    /// <summary>
    /// Removes <typeparamref name="THandler"/> from the registered handlers for <typeparamref name="TEvent"/>.
    /// Raises <see cref="OnEventRemoved"/> if this was the last handler for the event type.
    /// </summary>
    void RemoveSubscription<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>;

    /// <summary>Returns the canonical string name for event type <typeparamref name="TEvent"/> (the type's simple name).</summary>
    string GetEventName<TEvent>() where TEvent : IntegrationEvent;

    /// <summary>Returns the canonical string name for the given event <paramref name="eventType"/>.</summary>
    string GetEventName(Type eventType);

    /// <summary>
    /// Resolves the <see cref="Type"/> registered under <paramref name="eventName"/>,
    /// or <c>null</c> when the event name is not known.
    /// </summary>
    Type? GetEventTypeByName(string eventName);

    /// <summary>Returns all event names that currently have at least one registered handler.</summary>
    IReadOnlyList<string> GetAllEventNames();
}
