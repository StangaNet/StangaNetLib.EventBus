using StangaNetLib.EventBus.Abstractions;
using StangaNetLib.EventBus.Events;

namespace StangaNetLib.EventBus.Implementations;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IEventBusSubscriptionManager"/>.
/// Used by both the InMemory and RabbitMQ event bus backends.
/// </summary>
internal sealed class InMemoryEventBusSubscriptionManager : IEventBusSubscriptionManager
{
    private readonly Dictionary<string, List<Type>> _handlers = new();
    private readonly Dictionary<string, Type> _eventTypes = new();
    private readonly object _sync = new();

    private EventHandler<string>? _onEventRemovedHandler;

    /// <inheritdoc/>
    event EventHandler<string>? IEventBusSubscriptionManager.OnEventRemoved
    {
        add => _onEventRemovedHandler += value;
        remove => _onEventRemovedHandler -= value;
    }

    /// <inheritdoc/>
    public void AddSubscription<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventName = GetEventName<TEvent>();
        var handlerType = typeof(THandler);

        lock (_sync)
        {
            _eventTypes.TryAdd(eventName, typeof(TEvent));

            if (!_handlers.TryGetValue(eventName, out var handlers))
            {
                handlers = [];
                _handlers[eventName] = handlers;
            }

            if (!handlers.Contains(handlerType))
                handlers.Add(handlerType);
        }
    }

    /// <inheritdoc/>
    public void RemoveSubscription<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventName = GetEventName<TEvent>();

        bool fireRemoved;
        lock (_sync)
        {
            if (!_handlers.TryGetValue(eventName, out var handlers))
                return;

            handlers.Remove(typeof(THandler));
            fireRemoved = handlers.Count == 0;

            if (fireRemoved)
            {
                _handlers.Remove(eventName);
                _eventTypes.Remove(eventName);
            }
        }

        if (fireRemoved)
            _onEventRemovedHandler?.Invoke(this, eventName);
    }

    /// <inheritdoc/>
    public bool HasSubscriptionsForEvent(string eventName)
    {
        lock (_sync)
            return _handlers.TryGetValue(eventName, out var h) && h.Count > 0;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Type> GetHandlerTypesForEvent(string eventName)
    {
        lock (_sync)
            return _handlers.TryGetValue(eventName, out var h) ? h.ToList().AsReadOnly() : [];
    }

    /// <inheritdoc/>
    public string GetEventName<TEvent>() where TEvent : IntegrationEvent => typeof(TEvent).Name;

    /// <inheritdoc/>
    public string GetEventName(Type eventType) => eventType.Name;

    /// <inheritdoc/>
    public Type? GetEventTypeByName(string eventName)
    {
        lock (_sync)
            return _eventTypes.TryGetValue(eventName, out var t) ? t : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAllEventNames()
    {
        lock (_sync)
            return _handlers.Keys.ToList().AsReadOnly();
    }
}
