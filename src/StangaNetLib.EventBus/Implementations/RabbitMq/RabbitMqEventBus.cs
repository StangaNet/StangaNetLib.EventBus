using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.EventBus.Abstractions;
using StangaNetLib.EventBus.Errors;
using StangaNetLib.EventBus.Events;
using StangaNetLib.EventBus.Settings;

namespace StangaNetLib.EventBus.Implementations.RabbitMq;

/// <summary>
/// RabbitMQ-backed <see cref="IEventBus"/> that uses a direct exchange per event type.
/// Implements <see cref="IHostedService"/> so that the broker connection is managed by the application host.
/// Register handlers via <see cref="Subscribe{TEvent,THandler}"/> <b>before</b> the host starts,
/// so all bindings are created during <see cref="StartAsync"/>.
/// </summary>
internal sealed class RabbitMqEventBus : IEventBus, IHostedService, IAsyncDisposable
{
    private static readonly MethodInfo _dispatchHandlerMethod =
        typeof(RabbitMqEventBus).GetMethod(nameof(DispatchHandlerAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly IEventBusSubscriptionManager _subscriptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqEventBus> _logger;

    private IConnection? _connection;
    private IChannel? _publishChannel;
    private IChannel? _consumeChannel;
    private volatile bool _started;

    public RabbitMqEventBus(
        IEventBusSubscriptionManager subscriptions,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqSettings> options,
        ILogger<RabbitMqEventBus> logger)
    {
        _subscriptions = Guard.Against.Null(subscriptions, nameof(subscriptions));
        _scopeFactory = Guard.Against.Null(scopeFactory, nameof(scopeFactory));
        _settings = Guard.Against.Null(options?.Value, nameof(options));
        _logger = Guard.Against.Null(logger, nameof(logger));
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password,
            VirtualHost = _settings.VirtualHost,
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _publishChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        _consumeChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _publishChannel.ExchangeDeclareAsync(
            exchange: _settings.ExchangeName,
            type: ExchangeType.Direct,
            durable: _settings.Durable,
            autoDelete: _settings.AutoDelete,
            cancellationToken: cancellationToken);

        await _consumeChannel.QueueDeclareAsync(
            queue: _settings.QueueName,
            durable: _settings.Durable,
            exclusive: false,
            autoDelete: _settings.AutoDelete,
            cancellationToken: cancellationToken);

        foreach (var eventName in _subscriptions.GetAllEventNames())
        {
            await _consumeChannel.QueueBindAsync(
                queue: _settings.QueueName,
                exchange: _settings.ExchangeName,
                routingKey: eventName,
                cancellationToken: cancellationToken);
        }

        var consumer = new AsyncEventingBasicConsumer(_consumeChannel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        await _consumeChannel.BasicConsumeAsync(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        _started = true;
        ((IEventBusSubscriptionManager)_subscriptions).OnEventRemoved += OnEventRemoved;

        _logger.LogInformation("RabbitMQ event bus started. Exchange: {Exchange}, Queue: {Queue}",
            _settings.ExchangeName, _settings.QueueName);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _started = false;
        ((IEventBusSubscriptionManager)_subscriptions).OnEventRemoved -= OnEventRemoved;

        if (_consumeChannel is not null)
        {
            await _consumeChannel.CloseAsync(cancellationToken: cancellationToken);
            _consumeChannel.Dispose();
            _consumeChannel = null;
        }

        if (_publishChannel is not null)
        {
            await _publishChannel.CloseAsync(cancellationToken: cancellationToken);
            _publishChannel.Dispose();
            _publishChannel = null;
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken: cancellationToken);
            _connection.Dispose();
            _connection = null;
        }
    }

    /// <inheritdoc/>
    public async Task<Result> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        Guard.Against.Null(@event, nameof(@event));

        if (!_started || _publishChannel is null)
            return Result.Failure(EventBusErrors.NotConnected);

        var eventName = _subscriptions.GetEventName<TEvent>();

        if (!_subscriptions.HasSubscriptionsForEvent(eventName))
            return Result.Failure(EventBusErrors.NoHandlers);

        byte[] body;
        try
        {
            var json = JsonSerializer.Serialize(@event, @event.GetType());
            body = Encoding.UTF8.GetBytes(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize event {EventType}", typeof(TEvent).Name);
            return Result.Failure(EventBusErrors.SerializationFailed);
        }

        try
        {
            await _publishChannel.BasicPublishAsync(
                exchange: _settings.ExchangeName,
                routingKey: eventName,
                body: new ReadOnlyMemory<byte>(body),
                cancellationToken: cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to RabbitMQ", typeof(TEvent).Name);
            return Result.Failure(EventBusErrors.PublishFailed);
        }
    }

    /// <inheritdoc/>
    public void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
    {
        _subscriptions.AddSubscription<TEvent, THandler>();

        if (_started && _consumeChannel is not null)
        {
            var eventName = _subscriptions.GetEventName<TEvent>();
            _ = _consumeChannel.QueueBindAsync(
                queue: _settings.QueueName,
                exchange: _settings.ExchangeName,
                routingKey: eventName);
        }
    }

    /// <inheritdoc/>
    public void Unsubscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
        => _subscriptions.RemoveSubscription<TEvent, THandler>();

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var eventName = ea.RoutingKey;
        var eventType = _subscriptions.GetEventTypeByName(eventName);

        if (eventType is null)
        {
            _logger.LogWarning("Received message for unknown event type '{EventName}'. Nacking.", eventName);
            await _consumeChannel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        IntegrationEvent? @event;
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            @event = (IntegrationEvent?)JsonSerializer.Deserialize(json, eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize message for event type '{EventName}'", eventName);
            await _consumeChannel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        if (@event is null)
        {
            _logger.LogWarning("Deserialized null event for type '{EventName}'. Nacking.", eventName);
            await _consumeChannel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        var handlerTypes = _subscriptions.GetHandlerTypesForEvent(eventName);
        using var scope = _scopeFactory.CreateScope();

        foreach (var handlerType in handlerTypes)
        {
            var handler = scope.ServiceProvider.GetService(handlerType);
            if (handler is null)
                continue;

            try
            {
                var dispatchMethod = _dispatchHandlerMethod.MakeGenericMethod(eventType);
                await (Task)dispatchMethod.Invoke(this, [@event, handler, CancellationToken.None])!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler {Handler} threw an unhandled exception for event '{EventName}'",
                    handlerType.Name, eventName);
            }
        }

        await _consumeChannel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
    }

    private async Task DispatchHandlerAsync<TEvent>(TEvent @event, object handler, CancellationToken cancellationToken)
        where TEvent : IntegrationEvent
    {
        if (handler is IEventHandler<TEvent> typedHandler)
        {
            var result = await typedHandler.HandleAsync(@event, cancellationToken);
            if (result.IsFailure)
                _logger.LogWarning("Handler {Handler} failed for event {EventType}: {Error}",
                    handler.GetType().Name, typeof(TEvent).Name, result.Error);
        }
    }

    private void OnEventRemoved(object? sender, string eventName)
    {
        if (_consumeChannel is null) return;
        _ = _consumeChannel.QueueUnbindAsync(
            queue: _settings.QueueName,
            exchange: _settings.ExchangeName,
            routingKey: eventName);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _started = false;

        if (_consumeChannel is not null)
        {
            try { await _consumeChannel.CloseAsync(); } catch { /* best-effort */ }
            _consumeChannel.Dispose();
        }

        if (_publishChannel is not null)
        {
            try { await _publishChannel.CloseAsync(); } catch { /* best-effort */ }
            _publishChannel.Dispose();
        }

        if (_connection is not null)
        {
            try { await _connection.CloseAsync(); } catch { /* best-effort */ }
            _connection.Dispose();
        }
    }
}
