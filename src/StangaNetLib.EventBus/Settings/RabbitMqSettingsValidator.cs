using StangaNetLib.Core.Validators;

namespace StangaNetLib.EventBus.Settings;

/// <summary>
/// Validates <see cref="RabbitMqSettings"/> at application startup.
/// Registered automatically by <c>AddStangaNetLibEventBusWithRabbitMq</c>.
/// </summary>
internal sealed class RabbitMqSettingsValidator : SettingsValidatorBase<RabbitMqSettings>
{
    /// <inheritdoc/>
    protected override void ValidateCore(RabbitMqSettings options, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(options.HostName))
            errors.Add($"{nameof(RabbitMqSettings.HostName)} must not be empty.");

        if (options.Port is < 1 or > 65535)
            errors.Add($"{nameof(RabbitMqSettings.Port)} must be between 1 and 65535. Received: {options.Port}.");

        if (string.IsNullOrWhiteSpace(options.UserName))
            errors.Add($"{nameof(RabbitMqSettings.UserName)} must not be empty.");

        if (string.IsNullOrWhiteSpace(options.VirtualHost))
            errors.Add($"{nameof(RabbitMqSettings.VirtualHost)} must not be empty.");

        if (string.IsNullOrWhiteSpace(options.ExchangeName))
            errors.Add($"{nameof(RabbitMqSettings.ExchangeName)} must not be empty.");

        if (string.IsNullOrWhiteSpace(options.QueueName))
            errors.Add($"{nameof(RabbitMqSettings.QueueName)} must not be empty.");

        if (options.MaxConcurrentHandlers < 1)
            errors.Add($"{nameof(RabbitMqSettings.MaxConcurrentHandlers)} must be at least 1. Received: {options.MaxConcurrentHandlers}.");
    }
}
