using StangaNetLib.Core.Common;
using StangaNetLib.EventBus.Abstractions;
using StangaNetLib.EventBus.JobIntegration;

namespace StangaNetLib.EventBus.Tests.JobIntegration;

public sealed class EventTriggeredJobDispatcherTests
{
    // ── Fire-and-forget (no delay) ────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoDelay_CallsEnqueueAsync()
    {
        var scheduler = new FakeJobScheduler();
        var dispatcher = new EventTriggeredJobDispatcher<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            scheduler,
            e => new SendNotificationArgs(e.OrderId, "Order placed"));

        var result = await dispatcher.HandleAsync(new OrderPlacedEvent("ord-1"));

        result.IsSuccess.Should().BeTrue();
        scheduler.Jobs.Should().ContainSingle();
        scheduler.Jobs[0].JobType.Should().Be(typeof(SendNotificationJob));
        scheduler.Jobs[0].Delay.Should().BeNull();
        ((SendNotificationArgs)scheduler.Jobs[0].Args).UserId.Should().Be("ord-1");
    }

    [Fact]
    public async Task HandleAsync_NoDelay_ArgsFactoryMapsEventCorrectly()
    {
        var scheduler = new FakeJobScheduler();
        var dispatcher = new EventTriggeredJobDispatcher<UserRegisteredEvent, SendNotificationJob, SendNotificationArgs>(
            scheduler,
            e => new SendNotificationArgs(e.UserId, "Welcome"));

        await dispatcher.HandleAsync(new UserRegisteredEvent("usr-99"));

        var args = (SendNotificationArgs)scheduler.Jobs[0].Args;
        args.UserId.Should().Be("usr-99");
        args.Message.Should().Be("Welcome");
    }

    // ── Delayed scheduling ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WithDelay_CallsScheduleAsync()
    {
        var delay = TimeSpan.FromMinutes(5);
        var scheduler = new FakeJobScheduler();
        var dispatcher = new EventTriggeredJobDispatcher<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            scheduler,
            e => new SendNotificationArgs(e.OrderId, "Delayed"),
            delay);

        var result = await dispatcher.HandleAsync(new OrderPlacedEvent("ord-2"));

        result.IsSuccess.Should().BeTrue();
        scheduler.Jobs.Should().ContainSingle();
        scheduler.Jobs[0].Delay.Should().Be(delay);
    }

    [Fact]
    public async Task HandleAsync_WithDelay_ArgsFactoryStillApplied()
    {
        var scheduler = new FakeJobScheduler();
        var dispatcher = new EventTriggeredJobDispatcher<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            scheduler,
            e => new SendNotificationArgs(e.OrderId, "msg"),
            TimeSpan.FromSeconds(30));

        await dispatcher.HandleAsync(new OrderPlacedEvent("ord-delayed"));

        var args = (SendNotificationArgs)scheduler.Jobs[0].Args;
        args.UserId.Should().Be("ord-delayed");
    }

    // ── Failure propagation ───────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_EnqueueFails_ReturnsFailure()
    {
        var failure = Result<string>.Failure(Error.Internal("backend down"));
        var scheduler = new FakeJobScheduler(enqueueOverride: failure);
        var dispatcher = new EventTriggeredJobDispatcher<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            scheduler,
            e => new SendNotificationArgs(e.OrderId, "msg"));

        var result = await dispatcher.HandleAsync(new OrderPlacedEvent("ord-3"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Internal.Error");
    }

    [Fact]
    public async Task HandleAsync_ScheduleFails_ReturnsFailure()
    {
        var failure = Result<string>.Failure(Error.Validation("Delay", "negative"));
        var scheduler = new FakeJobScheduler(scheduleOverride: failure);
        var dispatcher = new EventTriggeredJobDispatcher<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            scheduler,
            e => new SendNotificationArgs(e.OrderId, "msg"),
            TimeSpan.FromSeconds(5));

        var result = await dispatcher.HandleAsync(new OrderPlacedEvent("ord-4"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Delay");
    }

    // ── Multiple events enqueue independently ─────────────────────────────

    [Fact]
    public async Task HandleAsync_CalledMultipleTimes_EnqueuesOneJobPerCall()
    {
        var scheduler = new FakeJobScheduler();
        var dispatcher = new EventTriggeredJobDispatcher<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            scheduler,
            e => new SendNotificationArgs(e.OrderId, "msg"));

        await dispatcher.HandleAsync(new OrderPlacedEvent("ord-a"));
        await dispatcher.HandleAsync(new OrderPlacedEvent("ord-b"));
        await dispatcher.HandleAsync(new OrderPlacedEvent("ord-c"));

        scheduler.Jobs.Should().HaveCount(3);
        scheduler.Jobs.Select(j => ((SendNotificationArgs)j.Args).UserId)
            .Should().BeEquivalentTo(["ord-a", "ord-b", "ord-c"]);
    }

    // ── Implements marker interface ───────────────────────────────────────

    [Fact]
    public void Dispatcher_ImplementsMarkerInterface()
    {
        var scheduler = new FakeJobScheduler();
        var dispatcher = new EventTriggeredJobDispatcher<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            scheduler, e => new SendNotificationArgs(e.OrderId, "msg"));

        dispatcher.Should().BeAssignableTo<IEventTriggeredJobHandler<OrderPlacedEvent>>();
    }
}
