using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;

namespace TimePay.Data.Repositories;

/// <summary>
/// Manages application settings persistence.
/// </summary>
public class SettingsRepository : ISettingsService
{
    private readonly TimePayDbContext _context;

    public SettingsRepository(TimePayDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Settings> GetSettingsAsync()
    {
        var settings = await _context.Settings.FirstOrDefaultAsync();
        if (settings == null)
        {
            // Should never happen after initialization, but handle gracefully
            settings = new Settings();
            _context.Settings.Add(settings);
            await _context.SaveChangesAsync();
        }
        return settings;
    }

    /// <inheritdoc />
    public async Task<Settings> UpdateSettingsAsync(Settings settings)
    {
        var existing = await _context.Settings.FirstOrDefaultAsync();
        if (existing == null)
        {
            _context.Settings.Add(settings);
        }
        else
        {
            existing.CurrencyCode = settings.CurrencyCode;
            existing.MinutesPerPeso = settings.MinutesPerPeso;
            existing.WarningMinutes1 = settings.WarningMinutes1;
            existing.WarningMinutes2 = settings.WarningMinutes2;
            existing.WarningMinutes3 = settings.WarningMinutes3;
            existing.SoundEnabled = settings.SoundEnabled;
            existing.AutoStartEnabled = settings.AutoStartEnabled;
            existing.PauseOnShutdown = settings.PauseOnShutdown;
            existing.AllowDecimalAmounts = settings.AllowDecimalAmounts;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();
        return (await _context.Settings.FirstAsync());
    }

    /// <inheritdoc />
    public async Task<Currency> GetCurrencyAsync()
    {
        var settings = await GetSettingsAsync();
        return Currency.FromCode(settings.CurrencyCode) ?? Currency.PHP;
    }
}
