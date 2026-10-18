using Microsoft.Extensions.DependencyInjection;
using StangaNetLib.Core.Common;
using StangaNetLib.EventBus.Abstractions;
using StangaNetLib.EventBus.Errors;
using StangaNetLib.EventBus.Events;

namespace StangaNetLib.EventBus.Tests.InMemory;

file sealed class AuditHandler : IEventHandler<OrderPlacedEvent>
{
    public int CallCount { get; private set; }
    public Task<Result> HandleAsync(OrderPlacedEvent @event, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(Result.Success());
    }
}

public sealed class InMemoryEventBusTests
{
    // ── PublishAsync — no handlers ────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_NoHandlers_ReturnsNoHandlersFailure()
    {
        var (bus, _) = BusFactory.Create();
        var result = await bus.PublishAsync(new OrderPlacedEvent("ord-1"));
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(EventBusErrors.NoHandlers.Code);
    }

    // ── PublishAsync — happy path ─────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_SingleHandler_HandlerReceivesEvent()
    {
        var handler = new RecordingHandler<OrderPlacedEvent>();
        var (bus, _) = BusFactory.CreateWithServices(sc =>
            sc.AddSingleton(handler));

        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();

        var @event = new OrderPlacedEvent("ord-42");
        var result = await bus.PublishAsync(@event);

        result.IsSuccess.Should().BeTrue();
        handler.Received.Should().ContainSingle().Which.OrderId.Should().Be("ord-42");
    }

    [Fact]
    public async Task PublishAsync_MultipleHandlers_AllHandlersInvoked()
    {
        var primary = new RecordingHandler<OrderPlacedEvent>();
        var audit = new AuditHandler();

        var (bus, _) = BusFactory.CreateWithServices(sc =>
        {
            sc.AddSingleton(primary);
            sc.AddSingleton(audit);
        });

        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        bus.Subscribe<OrderPlacedEvent, AuditHandler>();

        var result = await bus.PublishAsync(new OrderPlacedEvent("ord-1"));

        result.IsSuccess.Should().BeTrue();
        primary.CallCount.Should().Be(1);
        audit.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_TwoEventTypes_DispatchedIndependently()
    {
        var orderHandler = new RecordingHandler<OrderPlacedEvent>();
        var userHandler = new RecordingHandler<UserRegisteredEvent>();

        var (bus, _) = BusFactory.CreateWithServices(sc =>
        {
            sc.AddSingleton(orderHandler);
            sc.AddSingleton(userHandler);
        });

        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        bus.Subscribe<UserRegisteredEvent, RecordingHandler<UserRegisteredEvent>>();

        await bus.PublishAsync(new OrderPlacedEvent("ord-1"));
        await bus.PublishAsync(new UserRegisteredEvent("usr-1"));

        orderHandler.CallCount.Should().Be(1);
        userHandler.CallCount.Should().Be(1);
    }

    // ── Handler failure behaviour ────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_HandlerFails_ReturnsHandlersFailedError()
    {
        var handler = new RecordingHandler<OrderPlacedEvent>(_ => Result.Failure(Error.Internal("oops")));

        var (bus, _) = BusFactory.CreateWithServices(sc => sc.AddSingleton(handler));
        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();

        var result = await bus.PublishAsync(new OrderPlacedEvent("x"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EventBus.HandlersFailed");
    }

    [Fact]
    public async Task PublishAsync_PropagateHandlerExceptions_StopsOnFirstFailure()
    {
        var failing = new RecordingHandler<OrderPlacedEvent>(_ => Result.Failure(Error.Internal("fail")));
        var subsequent = new AuditHandler();

        var (bus, _) = BusFactory.CreateWithServices(
            sc =>
            {
                sc.AddSingleton(failing);
                sc.AddSingleton(subsequent);
            },
            s => s.PropagateHandlerExceptions = true);

        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        bus.Subscribe<OrderPlacedEvent, AuditHandler>();

        var result = await bus.PublishAsync(new OrderPlacedEvent("x"));

        result.IsFailure.Should().BeTrue();
        failing.CallCount.Should().Be(1);
        subsequent.CallCount.Should().Be(0);
    }

    // ── Subscribe / Unsubscribe ──────────────────────────────────────────

    [Fact]
    public async Task Unsubscribe_AfterSubscribe_EventNoLongerDispatched()
    {
        var handler = new RecordingHandler<OrderPlacedEvent>();

        var (bus, _) = BusFactory.CreateWithServices(sc => sc.AddSingleton(handler));

        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        bus.Unsubscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();

        var result = await bus.PublishAsync(new OrderPlacedEvent("x"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(EventBusErrors.NoHandlers.Code);
        handler.CallCount.Should().Be(0);
    }

    // ── Parallel dispatch ────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_MaxConcurrentHandlers_GreaterThan1_SuccessfulDispatch()
    {
        var handler = new RecordingHandler<OrderPlacedEvent>();

        var (bus, _) = BusFactory.CreateWithServices(
            sc => sc.AddSingleton(handler),
            s => s.MaxConcurrentHandlers = 4);

        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();

        var result = await bus.PublishAsync(new OrderPlacedEvent("parallel-1"));

        result.IsSuccess.Should().BeTrue();
        handler.CallCount.Should().Be(1);
    }

    // ── Guard validation ─────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_NullEvent_Throws()
    {
        var (bus, _) = BusFactory.Create();
        Func<Task> act = () => bus.PublishAsync<OrderPlacedEvent>(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
