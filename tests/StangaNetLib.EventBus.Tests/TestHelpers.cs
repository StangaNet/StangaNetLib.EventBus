using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StangaNetLib.Core.Common;
using StangaNetLib.EventBus.Abstractions;
using StangaNetLib.EventBus.Events;
using StangaNetLib.EventBus.Implementations;
using StangaNetLib.EventBus.Implementations.InMemory;
using StangaNetLib.EventBus.Settings;
using StangaNetLib.Jobs.Abstractions;
using StangaNetLib.Jobs.Models;

namespace StangaNetLib.EventBus.Tests;

// ── Test events ─────────────────────────────────────────────────────────────

internal sealed class OrderPlacedEvent : IntegrationEvent
{
    public string OrderId { get; }
    public OrderPlacedEvent(string orderId) => OrderId = orderId;
}

internal sealed class UserRegisteredEvent : IntegrationEvent
{
    public string UserId { get; }
    public UserRegisteredEvent(string userId) => UserId = userId;
}

// ── Reusable recording handler ────────────────────────────────────────────

internal sealed class RecordingHandler<TEvent> : IEventHandler<TEvent>
    where TEvent : IntegrationEvent
{
    private readonly List<TEvent> _received = [];
    private readonly Func<TEvent, Result>? _resultFactory;

    public IReadOnlyList<TEvent> Received => _received.AsReadOnly();
    public int CallCount => _received.Count;

    public RecordingHandler(Func<TEvent, Result>? resultFactory = null)
        => _resultFactory = resultFactory;

    public Task<Result> HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        _received.Add(@event);
        return Task.FromResult(_resultFactory?.Invoke(@event) ?? Result.Success());
    }
}

// ── Test job args / job types ─────────────────────────────────────────────

internal sealed record SendNotificationArgs(string UserId, string Message);

internal sealed class SendNotificationJob : IBackgroundJob<SendNotificationArgs>
{
    public Task ExecuteAsync(SendNotificationArgs args, JobContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// ── Fake job scheduler ────────────────────────────────────────────────────

internal sealed class FakeJobScheduler : IJobScheduler
{
    private readonly List<EnqueuedJob> _jobs = [];
    private readonly Result<string>? _enqueueOverride;
    private readonly Result<string>? _scheduleOverride;

    public IReadOnlyList<EnqueuedJob> Jobs => _jobs.AsReadOnly();

    public FakeJobScheduler(
        Result<string>? enqueueOverride = null,
        Result<string>? scheduleOverride = null)
    {
        _enqueueOverride = enqueueOverride;
        _scheduleOverride = scheduleOverride;
    }

    public Task<Result<string>> EnqueueAsync<TJob, TArgs>(TArgs args, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
    {
        _jobs.Add(new EnqueuedJob(typeof(TJob), args!, null));
        return Task.FromResult(_enqueueOverride ?? Result<string>.Success("fake-job-1"));
    }

    public Task<Result<string>> ScheduleAsync<TJob, TArgs>(TArgs args, TimeSpan delay, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
    {
        _jobs.Add(new EnqueuedJob(typeof(TJob), args!, delay));
        return Task.FromResult(_scheduleOverride ?? Result<string>.Success("fake-job-1"));
    }

    public Task<Result<string>> ScheduleAtAsync<TJob, TArgs>(TArgs args, DateTimeOffset runAt, CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TArgs>
    {
        _jobs.Add(new EnqueuedJob(typeof(TJob), args!, null));
        return Task.FromResult(_scheduleOverride ?? Result<string>.Success("fake-job-1"));
    }

    public Result AddOrUpdateRecurring<TJob, TArgs>(string jobId, TArgs args, string cronExpression)
        where TJob : class, IBackgroundJob<TArgs>
        => Result.Success();

    public Result RemoveRecurring(string jobId) => Result.Success();

    public Task<Result<string>> TriggerRecurringAsync(string jobId, CancellationToken cancellationToken = default)
        => Task.FromResult(Result<string>.Success("fake-job-1"));
}

internal sealed record EnqueuedJob(Type JobType, object Args, TimeSpan? Delay);

// ── Bus factory helpers ───────────────────────────────────────────────────

internal static class BusFactory
{
    /// <summary>Creates a bus and subscription manager backed by an empty DI container.</summary>
    internal static (InMemoryEventBus bus, InMemoryEventBusSubscriptionManager subscriptions)
        Create(Action<EventBusSettings>? configure = null)
    {
        var settings = new EventBusSettings();
        configure?.Invoke(settings);
        var sp = new ServiceCollection().BuildServiceProvider();
        return BuildBus(settings, sp);
    }

    /// <summary>Creates a bus where the caller configures the DI container that will supply handlers.</summary>
    internal static (InMemoryEventBus bus, InMemoryEventBusSubscriptionManager subscriptions)
        CreateWithServices(Action<IServiceCollection> configureServices, Action<EventBusSettings>? configure = null)
    {
        var settings = new EventBusSettings();
        configure?.Invoke(settings);
        var sc = new ServiceCollection();
        configureServices(sc);
        var sp = sc.BuildServiceProvider();
        return BuildBus(settings, sp);
    }

    private static (InMemoryEventBus bus, InMemoryEventBusSubscriptionManager subscriptions)
        BuildBus(EventBusSettings settings, IServiceProvider sp)
    {
        var subscriptions = new InMemoryEventBusSubscriptionManager();
        var bus = new InMemoryEventBus(
            subscriptions,
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings));
        return (bus, subscriptions);
    }
}
