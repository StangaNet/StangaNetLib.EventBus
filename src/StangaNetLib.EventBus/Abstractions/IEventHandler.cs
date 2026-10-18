using StangaNetLib.Core.Common;
using StangaNetLib.EventBus.Events;

namespace StangaNetLib.EventBus.Abstractions;

/// <summary>
/// Handles a specific type of integration event dispatched through the <see cref="IEventBus"/>.
/// </summary>
/// <typeparam name="TEvent">The <see cref="IntegrationEvent"/> subtype this handler processes.</typeparam>
/// <example>
/// <code>
/// public sealed class OrderPlacedHandler : IEventHandler&lt;OrderPlacedEvent&gt;
/// {
///     public async Task&lt;Result&gt; HandleAsync(OrderPlacedEvent @event, CancellationToken cancellationToken)
///     {
///         // process the event
///         return Result.Success();
///     }
/// }
/// </code>
/// </example>
public interface IEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    /// <summary>
    /// Processes the integration event asynchronously.
    /// </summary>
    /// <param name="event">The event to handle. Never null.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// A successful <see cref="Result"/> when the event was handled without errors;
    /// a failed <see cref="Result"/> for expected business-level processing failures.
    /// </returns>
    Task<Result> HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
