using StangaNetLib.Core.Validators;

namespace StangaNetLib.EventBus.Settings;

/// <summary>
/// Validates <see cref="EventBusSettings"/> at application startup.
/// Registered automatically by <c>AddStangaNetLibEventBus</c>.
/// </summary>
internal sealed class EventBusSettingsValidator : SettingsValidatorBase<EventBusSettings>
{
    /// <inheritdoc/>
    protected override void ValidateCore(EventBusSettings options, List<string> errors)
    {
        if (options.MaxConcurrentHandlers < 1)
            errors.Add($"{nameof(EventBusSettings.MaxConcurrentHandlers)} must be at least 1. Received: {options.MaxConcurrentHandlers}.");
    }
}
