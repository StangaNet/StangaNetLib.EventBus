using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StangaNetLib.Concurrency.Locking;
using StangaNetLib.Core.Common;
using StangaNetLib.EventBus.Abstractions;
using StangaNetLib.EventBus.Implementations;
using StangaNetLib.EventBus.Implementations.InMemory;
using StangaNetLib.EventBus.Settings;

namespace StangaNetLib.EventBus.Tests.InMemory;

// ── File-scoped handler (second failing type so both can be subscribed together) ──

file sealed class AlwaysFailingHandler : IEventHandler<OrderPlacedEvent>
{
    public int CallCount { get; private set; }

    public Task<Result> HandleAsync(OrderPlacedEvent @event, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(Result.Failure(Error.Internal("handler-failed")));
    }
}

// ── Fake IKeyedLock: tracks acquire/release without real exclusion ────────────

file sealed class FakeKeyedLock : IKeyedLock
{
    private int _acquireCount;
    private int _releaseCount;
    private readonly List<string> _acquiredKeys = [];

    public int AcquireCount => _acquireCount;
    public int ReleaseCount => _releaseCount;
    public IReadOnlyList<string> AcquiredKeys { get { lock (_acquiredKeys) return _acquiredKeys.ToList(); } }

    public Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _acquireCount);
        lock (_acquiredKeys) _acquiredKeys.Add(key);
        return Task.FromResult<IAsyncDisposable>(new LockHandle(this));
    }

    public Task<Result<IAsyncDisposable>> TryAcquireAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    internal void Release() => Interlocked.Increment(ref _releaseCount);

    private sealed class LockHandle(FakeKeyedLock owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { owner.Release(); return ValueTask.CompletedTask; }
    }
}

// ── Serializing IKeyedLock: real SemaphoreSlim exclusion, tracks max holders ──

file sealed class SerializingKeyedLock : IKeyedLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private int _concurrentHolders;
    public int MaxConcurrentHolders { get; private set; }

    public async Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        var current = Interlocked.Increment(ref _concurrentHolders);
        if (current > MaxConcurrentHolders) MaxConcurrentHolders = current;
        return new LockHandle(this);
    }

    public Task<Result<IAsyncDisposable>> TryAcquireAsync(string key, TimeSpan timeout, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    private void Release()
    {
        Interlocked.Decrement(ref _concurrentHolders);
        _semaphore.Release();
    }

    private sealed class LockHandle(SerializingKeyedLock owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { owner.Release(); return ValueTask.CompletedTask; }
    }
}

// ── Factory for buses with IKeyedLock ────────────────────────────────────────

file static class BusWithLock
{
    internal static IEventBus Create(
        IKeyedLock keyedLock,
        Action<IServiceCollection>? configureServices = null,
        Action<EventBusSettings>? configure = null)
    {
        var settings = new EventBusSettings();
        configure?.Invoke(settings);
        var sc = new ServiceCollection();
        configureServices?.Invoke(sc);
        var sp = sc.BuildServiceProvider();
        var subscriptions = new InMemoryEventBusSubscriptionManager();
        return new InMemoryEventBus(
            subscriptions,
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings),
            keyedLock);
    }
}

public sealed class InMemoryEventBusDispatchTests
{
    // ── Parallel dispatch: failure isolation ──────────────────────────────────

    [Fact]
    public async Task PublishAsync_Parallel_OneHandlerFails_OtherHandlerStillCompletes()
    {
        var succeeding = new RecordingHandler<OrderPlacedEvent>();
        var failing = new AlwaysFailingHandler();

        var (bus, _) = BusFactory.CreateWithServices(
            sc =>
            {
                sc.AddSingleton(succeeding);
                sc.AddSingleton(failing);
            },
            s => s.MaxConcurrentHandlers = 4);

        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        bus.Subscribe<OrderPlacedEvent, AlwaysFailingHandler>();

        var result = await bus.PublishAsync(new OrderPlacedEvent("p1"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EventBus.HandlersFailed");
        succeeding.CallCount.Should().Be(1);
        failing.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_Parallel_MultipleHandlersFail_AllFailuresAggregated()
    {
        var failing1 = new RecordingHandler<OrderPlacedEvent>(_ => Result.Failure(Error.Internal("fail-1")));
        var failing2 = new AlwaysFailingHandler();

        var (bus, _) = BusFactory.CreateWithServices(
            sc =>
            {
                sc.AddSingleton(failing1);
                sc.AddSingleton(failing2);
            },
            s => s.MaxConcurrentHandlers = 4);

        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        bus.Subscribe<OrderPlacedEvent, AlwaysFailingHandler>();

        var result = await bus.PublishAsync(new OrderPlacedEvent("p2"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EventBus.HandlersFailed");
        failing1.CallCount.Should().Be(1);
        failing2.CallCount.Should().Be(1);
    }

    // ── IKeyedLock: acquire and release tracking ──────────────────────────────

    [Fact]
    public async Task PublishAsync_WithKeyedLock_AcquiresAndReleasesHandlePerPublish()
    {
        var keyedLock = new FakeKeyedLock();
        var handler = new RecordingHandler<OrderPlacedEvent>();

        var bus = BusWithLock.Create(keyedLock, sc => sc.AddSingleton(handler));
        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();

        await bus.PublishAsync(new OrderPlacedEvent("k1"));
        await bus.PublishAsync(new OrderPlacedEvent("k2"));

        keyedLock.AcquireCount.Should().Be(2);
        keyedLock.ReleaseCount.Should().Be(2);
    }

    [Fact]
    public async Task PublishAsync_WithKeyedLock_UsesEventTypeNameAsLockKey()
    {
        var keyedLock = new FakeKeyedLock();
        var handler = new RecordingHandler<OrderPlacedEvent>();

        var bus = BusWithLock.Create(keyedLock, sc => sc.AddSingleton(handler));
        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();

        await bus.PublishAsync(new OrderPlacedEvent("k3"));

        keyedLock.AcquiredKeys.Should().ContainSingle()
            .Which.Should().Be(nameof(OrderPlacedEvent));
    }

    // ── IKeyedLock: concurrent publishes serialized to one at a time ──────────

    [Fact]
    public async Task PublishAsync_WithKeyedLock_ConcurrentPublishesNeverExceedOneHolder()
    {
        var keyedLock = new SerializingKeyedLock();
        var handler = new RecordingHandler<OrderPlacedEvent>();

        var bus = BusWithLock.Create(keyedLock, sc => sc.AddSingleton(handler));
        bus.Subscribe<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();

        var tasks = Enumerable.Range(1, 5)
            .Select(i => bus.PublishAsync(new OrderPlacedEvent($"c{i}")));

        await Task.WhenAll(tasks);

        keyedLock.MaxConcurrentHolders.Should().Be(1);
        handler.CallCount.Should().Be(5);
    }
}
