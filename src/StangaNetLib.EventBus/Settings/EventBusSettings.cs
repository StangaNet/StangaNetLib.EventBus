namespace StangaNetLib.EventBus.Settings;

/// <summary>Configuration for the in-memory StangaNetLib.EventBus backend.</summary>
public class EventBusSettings
{
    /// <summary>The <c>appsettings.json</c> section key. Value: <c>"EventBusSettings"</c>.</summary>
    public const string SectionName = "EventBusSettings";

    /// <summary>
    /// When <c>true</c>, the first handler failure stops dispatch immediately and returns that failure result
    /// without invoking remaining handlers.
    /// When <c>false</c> (default), all handlers are always invoked and any failures are aggregated into
    /// a single <c>EventBus.HandlersFailed</c> error.
    /// </summary>
    public bool PropagateHandlerExceptions { get; set; } = false;

    /// <summary>
    /// Maximum number of handlers dispatched in parallel for a single <see cref="Abstractions.IEventBus.PublishAsync{TEvent}"/> call.
    /// A value of <c>1</c> (default) dispatches handlers sequentially.
    /// Must be at least 1.
    /// </summary>
    public int MaxConcurrentHandlers { get; set; } = 1;
}
