using TimePay.Core.Models;

namespace TimePay.Core.Interfaces;

/// <summary>
/// Manages application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current application settings.
    /// </summary>
    Task<Settings> GetSettingsAsync();

    /// <summary>
    /// Updates the application settings.
    /// </summary>
    Task<Settings> UpdateSettingsAsync(Settings settings);

    /// <summary>
    /// Gets the current currency configuration.
    /// </summary>
    Task<Currency> GetCurrencyAsync();
}
