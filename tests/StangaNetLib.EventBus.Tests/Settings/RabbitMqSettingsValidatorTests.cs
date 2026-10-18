using Microsoft.Extensions.Options;
using StangaNetLib.EventBus.Settings;

namespace StangaNetLib.EventBus.Tests.Settings;

public sealed class RabbitMqSettingsValidatorTests
{
    private static ValidateOptionsResult Validate(RabbitMqSettings settings)
    {
        var validator = new RabbitMqSettingsValidator();
        return validator.Validate(null, settings);
    }

    [Fact]
    public void DefaultSettings_Valid()
    {
        var result = Validate(new RabbitMqSettings());
        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void HostName_NullOrWhiteSpace_Fails(string? value)
    {
        var result = Validate(new RabbitMqSettings { HostName = value! });
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain(nameof(RabbitMqSettings.HostName));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Port_OutOfRange_Fails(int port)
    {
        var result = Validate(new RabbitMqSettings { Port = port });
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain(nameof(RabbitMqSettings.Port));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5672)]
    [InlineData(65535)]
    public void Port_InRange_Valid(int port)
    {
        var result = Validate(new RabbitMqSettings { Port = port });
        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UserName_Empty_Fails(string value)
    {
        var result = Validate(new RabbitMqSettings { UserName = value });
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain(nameof(RabbitMqSettings.UserName));
    }

    [Fact]
    public void VirtualHost_Empty_Fails()
    {
        var result = Validate(new RabbitMqSettings { VirtualHost = "" });
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain(nameof(RabbitMqSettings.VirtualHost));
    }

    [Fact]
    public void ExchangeName_Empty_Fails()
    {
        var result = Validate(new RabbitMqSettings { ExchangeName = "" });
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain(nameof(RabbitMqSettings.ExchangeName));
    }

    [Fact]
    public void QueueName_Empty_Fails()
    {
        var result = Validate(new RabbitMqSettings { QueueName = "" });
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain(nameof(RabbitMqSettings.QueueName));
    }

    [Fact]
    public void MaxConcurrentHandlers_Zero_Fails()
    {
        var result = Validate(new RabbitMqSettings { MaxConcurrentHandlers = 0 });
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain(nameof(RabbitMqSettings.MaxConcurrentHandlers));
    }

    [Fact]
    public void MultipleInvalidFields_AllErrorsReported()
    {
        var result = Validate(new RabbitMqSettings
        {
            HostName = "",
            Port = 0,
            ExchangeName = ""
        });
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain(nameof(RabbitMqSettings.HostName));
        result.FailureMessage.Should().Contain(nameof(RabbitMqSettings.Port));
        result.FailureMessage.Should().Contain(nameof(RabbitMqSettings.ExchangeName));
    }
}
