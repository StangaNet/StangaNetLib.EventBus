using StangaNetLib.EventBus.Implementations;

namespace StangaNetLib.EventBus.Tests.InMemory;

public sealed class InMemoryEventBusSubscriptionManagerTests
{
    private static InMemoryEventBusSubscriptionManager Build() => new();

    [Fact]
    public void AddSubscription_HasSubscriptionsForEvent_ReturnsTrue()
    {
        var mgr = Build();
        mgr.AddSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        mgr.HasSubscriptionsForEvent("OrderPlacedEvent").Should().BeTrue();
    }

    [Fact]
    public void HasSubscriptionsForEvent_UnknownName_ReturnsFalse()
    {
        var mgr = Build();
        mgr.HasSubscriptionsForEvent("GhostEvent").Should().BeFalse();
    }

    [Fact]
    public void AddSubscription_SameHandlerTwice_StoredOnce()
    {
        var mgr = Build();
        mgr.AddSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        mgr.AddSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        mgr.GetHandlerTypesForEvent("OrderPlacedEvent").Should().HaveCount(1);
    }

    [Fact]
    public void GetHandlerTypesForEvent_ReturnsRegisteredType()
    {
        var mgr = Build();
        mgr.AddSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        var types = mgr.GetHandlerTypesForEvent("OrderPlacedEvent");
        types.Should().ContainSingle().Which.Should().Be(typeof(RecordingHandler<OrderPlacedEvent>));
    }

    [Fact]
    public void GetHandlerTypesForEvent_UnknownName_ReturnsEmpty()
    {
        var mgr = Build();
        mgr.GetHandlerTypesForEvent("Ghost").Should().BeEmpty();
    }

    [Fact]
    public void GetEventName_Generic_ReturnsTypeName()
    {
        var mgr = Build();
        mgr.GetEventName<OrderPlacedEvent>().Should().Be("OrderPlacedEvent");
    }

    [Fact]
    public void GetEventName_ByType_ReturnsTypeName()
    {
        var mgr = Build();
        mgr.GetEventName(typeof(UserRegisteredEvent)).Should().Be("UserRegisteredEvent");
    }

    [Fact]
    public void GetEventTypeByName_AfterSubscription_ReturnsType()
    {
        var mgr = Build();
        mgr.AddSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        mgr.GetEventTypeByName("OrderPlacedEvent").Should().Be(typeof(OrderPlacedEvent));
    }

    [Fact]
    public void GetEventTypeByName_UnknownName_ReturnsNull()
    {
        var mgr = Build();
        mgr.GetEventTypeByName("Ghost").Should().BeNull();
    }

    [Fact]
    public void RemoveSubscription_LastHandler_HasSubscriptionsReturnsFalse()
    {
        var mgr = Build();
        mgr.AddSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        mgr.RemoveSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        mgr.HasSubscriptionsForEvent("OrderPlacedEvent").Should().BeFalse();
    }

    [Fact]
    public void RemoveSubscription_LastHandler_FiresOnEventRemoved()
    {
        var mgr = Build();
        mgr.AddSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();

        string? firedName = null;
        ((StangaNetLib.EventBus.Abstractions.IEventBusSubscriptionManager)mgr).OnEventRemoved += (_, name) => firedName = name;

        mgr.RemoveSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();

        firedName.Should().Be("OrderPlacedEvent");
    }

    [Fact]
    public void RemoveSubscription_NonExistent_DoesNotThrow()
    {
        var mgr = Build();
        var act = () => mgr.RemoveSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        act.Should().NotThrow();
    }

    [Fact]
    public void GetAllEventNames_ReturnsAllSubscribedEvents()
    {
        var mgr = Build();
        mgr.AddSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        mgr.AddSubscription<UserRegisteredEvent, RecordingHandler<UserRegisteredEvent>>();

        mgr.GetAllEventNames().Should().BeEquivalentTo(["OrderPlacedEvent", "UserRegisteredEvent"]);
    }

    [Fact]
    public void GetAllEventNames_AfterRemovingAll_ReturnsEmpty()
    {
        var mgr = Build();
        mgr.AddSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();
        mgr.RemoveSubscription<OrderPlacedEvent, RecordingHandler<OrderPlacedEvent>>();

        mgr.GetAllEventNames().Should().BeEmpty();
    }
}
