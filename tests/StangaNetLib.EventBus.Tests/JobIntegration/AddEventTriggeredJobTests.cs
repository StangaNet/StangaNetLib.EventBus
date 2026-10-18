using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StangaNetLib.EventBus.Abstractions;
using StangaNetLib.EventBus.Extensions;
using StangaNetLib.EventBus.JobIntegration;
using StangaNetLib.Jobs.Abstractions;

namespace StangaNetLib.EventBus.Tests.JobIntegration;

public sealed class AddEventTriggeredJobTests
{
    // ── DI registration ───────────────────────────────────────────────────

    [Fact]
    public void AddEventTriggeredJob_RegistersDispatcherInDi()
    {
        var services = new ServiceCollection();
        services.AddStangaNetLibEventBus();
        services.AddSingleton<IJobScheduler>(new FakeJobScheduler());
        services.AddEventTriggeredJob<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            e => new SendNotificationArgs(e.OrderId, "msg"));

        var sp = services.BuildServiceProvider();
        var handler = sp.GetService<EventTriggeredJobDispatcher<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>>();

        handler.Should().NotBeNull();
    }

    [Fact]
    public void AddEventTriggeredJob_RegistersBootstrapperHostedService()
    {
        var services = new ServiceCollection();
        services.AddStangaNetLibEventBus();
        services.AddSingleton<IJobScheduler>(new FakeJobScheduler());
        services.AddEventTriggeredJob<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            e => new SendNotificationArgs(e.OrderId, "msg"));

        var sp = services.BuildServiceProvider();
        var hostedServices = sp.GetServices<IHostedService>().ToList();

        hostedServices.Should().ContainSingle(s => s is EventTriggeredJobBootstrapper);
    }

    [Fact]
    public void AddEventTriggeredJob_CalledTwice_RegistersBootstrapperOnce()
    {
        var services = new ServiceCollection();
        services.AddStangaNetLibEventBus();
        services.AddSingleton<IJobScheduler>(new FakeJobScheduler());
        services.AddEventTriggeredJob<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            e => new SendNotificationArgs(e.OrderId, "order"));
        services.AddEventTriggeredJob<UserRegisteredEvent, SendNotificationJob, SendNotificationArgs>(
            e => new SendNotificationArgs(e.UserId, "welcome"));

        var sp = services.BuildServiceProvider();
        var bootstrappers = sp.GetServices<IHostedService>()
            .OfType<EventTriggeredJobBootstrapper>().ToList();

        bootstrappers.Should().ContainSingle();
    }

    // ── Bootstrapper subscription ─────────────────────────────────────────

    [Fact]
    public async Task Bootstrapper_StartAsync_SubscribesDispatcherToEventBus()
    {
        var scheduler = new FakeJobScheduler();
        var services = new ServiceCollection();
        services.AddStangaNetLibEventBus();
        services.AddSingleton<IJobScheduler>(scheduler);
        services.AddEventTriggeredJob<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            e => new SendNotificationArgs(e.OrderId, "msg"));

        var sp = services.BuildServiceProvider();
        var bootstrapper = sp.GetServices<IHostedService>()
            .OfType<EventTriggeredJobBootstrapper>()
            .Single();

        await bootstrapper.StartAsync(CancellationToken.None);

        var bus = sp.GetRequiredService<IEventBus>();
        var result = await bus.PublishAsync(new OrderPlacedEvent("ord-boot"));

        result.IsSuccess.Should().BeTrue();
        scheduler.Jobs.Should().ContainSingle();
    }

    // ── End-to-end: event publish triggers job enqueue ────────────────────

    [Fact]
    public async Task EventPublished_TriggersJobEnqueue_EndToEnd()
    {
        var scheduler = new FakeJobScheduler();
        var services = new ServiceCollection();
        services.AddStangaNetLibEventBus();
        services.AddSingleton<IJobScheduler>(scheduler);
        services.AddEventTriggeredJob<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            e => new SendNotificationArgs(e.OrderId, $"Order {e.OrderId} placed"));

        var sp = services.BuildServiceProvider();

        // Apply subscriptions (normally done by the hosted service)
        var bootstrapper = sp.GetServices<IHostedService>()
            .OfType<EventTriggeredJobBootstrapper>()
            .Single();
        await bootstrapper.StartAsync(CancellationToken.None);

        var bus = sp.GetRequiredService<IEventBus>();
        await bus.PublishAsync(new OrderPlacedEvent("ord-e2e"));

        scheduler.Jobs.Should().ContainSingle();
        var job = scheduler.Jobs[0];
        job.JobType.Should().Be(typeof(SendNotificationJob));
        job.Delay.Should().BeNull();
        ((SendNotificationArgs)job.Args).UserId.Should().Be("ord-e2e");
        ((SendNotificationArgs)job.Args).Message.Should().Be("Order ord-e2e placed");
    }

    [Fact]
    public async Task EventPublished_WithDelay_TriggersDelayedJobSchedule()
    {
        var delay = TimeSpan.FromMinutes(10);
        var scheduler = new FakeJobScheduler();
        var services = new ServiceCollection();
        services.AddStangaNetLibEventBus();
        services.AddSingleton<IJobScheduler>(scheduler);
        services.AddEventTriggeredJob<UserRegisteredEvent, SendNotificationJob, SendNotificationArgs>(
            e => new SendNotificationArgs(e.UserId, "welcome"),
            delay);

        var sp = services.BuildServiceProvider();
        var bootstrapper = sp.GetServices<IHostedService>()
            .OfType<EventTriggeredJobBootstrapper>()
            .Single();
        await bootstrapper.StartAsync(CancellationToken.None);

        var bus = sp.GetRequiredService<IEventBus>();
        await bus.PublishAsync(new UserRegisteredEvent("usr-delay"));

        scheduler.Jobs.Should().ContainSingle();
        scheduler.Jobs[0].Delay.Should().Be(delay);
    }

    [Fact]
    public async Task MultipleEventTypes_EachTriggersDifferentJob()
    {
        var scheduler = new FakeJobScheduler();
        var services = new ServiceCollection();
        services.AddStangaNetLibEventBus();
        services.AddSingleton<IJobScheduler>(scheduler);
        services.AddEventTriggeredJob<OrderPlacedEvent, SendNotificationJob, SendNotificationArgs>(
            e => new SendNotificationArgs(e.OrderId, "order-msg"));
        services.AddEventTriggeredJob<UserRegisteredEvent, SendNotificationJob, SendNotificationArgs>(
            e => new SendNotificationArgs(e.UserId, "user-msg"));

        var sp = services.BuildServiceProvider();
        var bootstrapper = sp.GetServices<IHostedService>()
            .OfType<EventTriggeredJobBootstrapper>()
            .Single();
        await bootstrapper.StartAsync(CancellationToken.None);

        var bus = sp.GetRequiredService<IEventBus>();
        await bus.PublishAsync(new OrderPlacedEvent("o1"));
        await bus.PublishAsync(new UserRegisteredEvent("u1"));

        scheduler.Jobs.Should().HaveCount(2);
    }
}
