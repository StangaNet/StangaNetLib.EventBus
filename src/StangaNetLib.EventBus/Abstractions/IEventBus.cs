using StangaNetLib.Core.Common;
using StangaNetLib.EventBus.Events;

namespace StangaNetLib.EventBus.Abstractions;

/// <summary>
/// Publish/subscribe integration event bus.
/// Publish an event to dispatch it to all registered handlers; use <see cref="Subscribe{TEvent,THandler}"/>
/// to register handler types before publishing.
/// </summary>
/// <example>
/// Register handlers and publish:
/// <code>
/// eventBus.Subscribe&lt;OrderPlacedEvent, OrderPlacedHandler&gt;();
///
/// var result = await eventBus.PublishAsync(new OrderPlacedEvent(orderId), cancellationToken);
/// if (result.IsFailure)
///     logger.LogWarning("Event dispatch failed: {Error}", result.Error);
/// </code>
/// </example>
public interface IEventBus
{
    /// <summary>
    /// Publishes <paramref name="event"/> to all registered handlers for <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The integration event type to publish.</typeparam>
    /// <param name="event">The event instance to dispatch. Must not be null.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// A successful <see cref="Result"/> when all handlers completed without errors;
    /// <c>EventBus.NoHandlers</c> when no handlers are registered for <typeparamref name="TEvent"/>;
    /// <c>EventBus.HandlersFailed</c> when one or more handlers returned a failure result;
    /// <c>EventBus.NotConnected</c> (RabbitMQ backend only) when the bus has not been started.
    /// </returns>
    Task<Result> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent;

    /// <summary>
    /// Registers <typeparamref name="THandler"/> to receive events of type <typeparamref name="TEvent"/>.
    /// The handler is resolved from DI on each publish call, so it may be registered with any lifetime.
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
    /// <typeparam name="THandler">The handler implementation to register.</typeparam>
    void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>;

    /// <summary>
    /// Removes <typeparamref name="THandler"/> from the handlers registered for <typeparamref name="TEvent"/>.
    /// </summary>
    void Unsubscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>;
}
