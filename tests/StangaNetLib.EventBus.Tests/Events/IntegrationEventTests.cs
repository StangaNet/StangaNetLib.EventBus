using StangaNetLib.EventBus.Events;

namespace StangaNetLib.EventBus.Tests.Events;

public sealed class IntegrationEventTests
{
    private sealed class ConcreteEvent : IntegrationEvent
    {
        public ConcreteEvent() { }
        public ConcreteEvent(Guid id, DateTimeOffset at) : base(id, at) { }
    }

    [Fact]
    public void DefaultConstructor_AssignsNonEmptyEventId()
    {
        var e = new ConcreteEvent();
        e.EventId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void DefaultConstructor_TwoInstances_HaveDistinctEventIds()
    {
        var e1 = new ConcreteEvent();
        var e2 = new ConcreteEvent();
        e1.EventId.Should().NotBe(e2.EventId);
    }

    [Fact]
    public void DefaultConstructor_OccurredAt_IsRecentUtcTime()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var e = new ConcreteEvent();
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        e.OccurredAt.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact]
    public void ExplicitConstructor_PreservesEventId()
    {
        var id = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow.AddHours(-1);
        var e = new ConcreteEvent(id, at);
        e.EventId.Should().Be(id);
    }

    [Fact]
    public void ExplicitConstructor_PreservesOccurredAt()
    {
        var at = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var e = new ConcreteEvent(Guid.NewGuid(), at);
        e.OccurredAt.Should().Be(at);
    }
}
