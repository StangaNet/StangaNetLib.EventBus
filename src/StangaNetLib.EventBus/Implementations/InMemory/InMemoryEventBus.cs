using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StangaNetLib.Concurrency.Locking;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.EventBus.Abstractions;
using StangaNetLib.EventBus.Errors;
using StangaNetLib.EventBus.Events;
using StangaNetLib.EventBus.Settings;

namespace StangaNetLib.EventBus.Implementations.InMemory;

/// <summary>
/// In-process publish/subscribe event bus backed by the DI container.
/// Handlers are resolved per-publish from a new DI scope, so any lifetime (transient, scoped, singleton) is supported.
/// </summary>
internal sealed class InMemoryEventBus : IEventBus
{
    private readonly IEventBusSubscriptionManager _subscriptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EventBusSettings _settings;
    private readonly IKeyedLock? _keyedLock;

    /// <summary>
    /// Initialises the in-memory event bus.
    /// </summary>
    /// <param name="subscriptions">Subscription registry shared with callers of <see cref="Subscribe{TEvent,THandler}"/>.</param>
    /// <param name="scopeFactory">Used to create a DI scope per publish call.</param>
    /// <param name="options">Runtime settings controlling dispatch behaviour.</param>
    /// <param name="keyedLock">
    /// Optional per-event-type lock. When provided, concurrent publishes of the same event type
    /// are serialised — useful when handlers have non-idempotent side effects.
    /// </param>
    public InMemoryEventBus(
        IEventBusSubscriptionManager subscriptions,
        IServiceScopeFactory scopeFactory,
        IOptions<EventBusSettings> options,
        IKeyedLock? keyedLock = null)
    {
        _subscriptions = Guard.Against.Null(subscriptions, nameof(subscriptions));
        _scopeFactory = Guard.Against.Null(scopeFactory, nameof(scopeFactory));
        _settings = Guard.Against.Null(options?.Value, nameof(options));
        _keyedLock = keyedLock;
    }

    /// <inheritdoc/>
    public Task<Result> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        Guard.Against.Null(@event, nameof(@event));

        return _keyedLock is not null
            ? PublishWithLockAsync(@event, cancellationToken)
            : DispatchAsync(@event, cancellationToken);
    }

    private async Task<Result> PublishWithLockAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IntegrationEvent
    {
        var key = _subscriptions.GetEventName<TEvent>();
        await using var handle = await _keyedLock!.AcquireAsync(key, cancellationToken);
        return await DispatchAsync(@event, cancellationToken);
    }

    private async Task<Result> DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IntegrationEvent
    {
        var eventName = _subscriptions.GetEventName<TEvent>();

        if (!_subscriptions.HasSubscriptionsForEvent(eventName))
            return Result.Failure(EventBusErrors.NoHandlers);

        var handlerTypes = _subscriptions.GetHandlerTypesForEvent(eventName);

        using var scope = _scopeFactory.CreateScope();

        if (_settings.MaxConcurrentHandlers > 1)
            return await DispatchParallelAsync<TEvent>(@event, handlerTypes, scope.ServiceProvider, cancellationToken);

        return await DispatchSequentialAsync<TEvent>(@event, handlerTypes, scope.ServiceProvider, cancellationToken);
    }

    private async Task<Result> DispatchSequentialAsync<TEvent>(
        TEvent @event,
        IReadOnlyList<Type> handlerTypes,
        IServiceProvider provider,
        CancellationToken cancellationToken)
        where TEvent : IntegrationEvent
    {
        var failures = new List<Error>();

        foreach (var handlerType in handlerTypes)
        {
            var result = await InvokeHandlerAsync<TEvent>(@event, handlerType, provider, cancellationToken);
            if (result.IsFailure)
            {
                if (_settings.PropagateHandlerExceptions)
                    return result;
                failures.Add(result.Error);
            }
        }

        return failures.Count > 0
            ? Result.Failure(EventBusErrors.HandlersFailed(failures.Count, handlerTypes.Count))
            : Result.Success();
    }

    private async Task<Result> DispatchParallelAsync<TEvent>(
        TEvent @event,
        IReadOnlyList<Type> handlerTypes,
        IServiceProvider provider,
        CancellationToken cancellationToken)
        where TEvent : IntegrationEvent
    {
        using var semaphore = new SemaphoreSlim(_settings.MaxConcurrentHandlers, _settings.MaxConcurrentHandlers);

        var tasks = handlerTypes.Select(async handlerType =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await InvokeHandlerAsync<TEvent>(@event, handlerType, provider, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        var failures = results.Where(r => r.IsFailure).Select(r => r.Error).ToList();

        return failures.Count > 0
            ? Result.Failure(EventBusErrors.HandlersFailed(failures.Count, handlerTypes.Count))
            : Result.Success();
    }

    private static Task<Result> InvokeHandlerAsync<TEvent>(
        TEvent @event,
        Type handlerType,
        IServiceProvider provider,
        CancellationToken cancellationToken)
        where TEvent : IntegrationEvent
    {
        if (provider.GetService(handlerType) is IEventHandler<TEvent> handler)
            return handler.HandleAsync(@event, cancellationToken);

        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
        => _subscriptions.AddSubscription<TEvent, THandler>();

    /// <inheritdoc/>
    public void Unsubscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
        => _subscriptions.RemoveSubscription<TEvent, THandler>();
}
