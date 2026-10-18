using StangaNetLib.Core.Common;

namespace StangaNetLib.EventBus.Errors;

/// <summary>
/// Well-known <see cref="Error"/> instances for StangaNetLib.EventBus operations.
/// </summary>
public static class EventBusErrors
{
    /// <summary>
    /// No handlers are registered for the published event type.
    /// Returned by <see cref="Abstractions.IEventBus.PublishAsync{TEvent}"/> when the subscription manager
    /// has no entries for the event.
    /// </summary>
    public static readonly Error NoHandlers = new(
        "EventBus.NoHandlers",
        "No handlers are registered for this event type.",
        ErrorType.Validation);

    /// <summary>
    /// One or more handlers returned a failure result when processing the event.
    /// </summary>
    /// <param name="failureCount">Number of handlers that failed.</param>
    /// <param name="totalCount">Total number of handlers invoked.</param>
    public static Error HandlersFailed(int failureCount, int totalCount) => new(
        "EventBus.HandlersFailed",
        $"{failureCount} of {totalCount} handler(s) failed to process the event.",
        ErrorType.Internal);

    /// <summary>
    /// The RabbitMQ event bus has not been started yet or is not connected.
    /// Ensure <c>IHostedService.StartAsync</c> has completed before publishing.
    /// </summary>
    public static readonly Error NotConnected = new(
        "EventBus.NotConnected",
        "The event bus transport is not connected. Ensure the host has started.",
        ErrorType.Internal);

    /// <summary>Event serialization failed before transport publish.</summary>
    public static readonly Error SerializationFailed = new(
        "EventBus.SerializationFailed",
        "Failed to serialize the event for transport.",
        ErrorType.Internal);

    /// <summary>The transport layer rejected or failed to accept the published event.</summary>
    public static readonly Error PublishFailed = new(
        "EventBus.PublishFailed",
        "Failed to publish the event to the transport layer.",
        ErrorType.Internal);
}
