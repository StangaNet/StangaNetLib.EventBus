using Microsoft.Extensions.Options;
using StangaNetLib.EventBus.Settings;

namespace StangaNetLib.EventBus.Tests.Settings;

public sealed class EventBusSettingsValidatorTests
{
    private static ValidateOptionsResult Validate(EventBusSettings settings)
    {
        var validator = new EventBusSettingsValidator();
        return validator.Validate(null, settings);
    }

    [Fact]
    public void DefaultSettings_Valid()
    {
        var result = Validate(new EventBusSettings());
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void MaxConcurrentHandlers_Zero_Fails()
    {
        var result = Validate(new EventBusSettings { MaxConcurrentHandlers = 0 });
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain(nameof(EventBusSettings.MaxConcurrentHandlers));
    }

    [Fact]
    public void MaxConcurrentHandlers_Negative_Fails()
    {
        var result = Validate(new EventBusSettings { MaxConcurrentHandlers = -5 });
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void MaxConcurrentHandlers_One_Valid()
    {
        var result = Validate(new EventBusSettings { MaxConcurrentHandlers = 1 });
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void MaxConcurrentHandlers_Large_Valid()
    {
        var result = Validate(new EventBusSettings { MaxConcurrentHandlers = 100 });
        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PropagateHandlerExceptions_AnyValue_Valid(bool value)
    {
        var result = Validate(new EventBusSettings { PropagateHandlerExceptions = value });
        result.Succeeded.Should().BeTrue();
    }
}
