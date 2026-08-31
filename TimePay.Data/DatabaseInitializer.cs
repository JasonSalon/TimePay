using Microsoft.EntityFrameworkCore;
using TimePay.Core.Models;

namespace TimePay.Data;

/// <summary>
/// Initializes the database and seeds default data.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Ensures the database is created and seeds default settings.
    /// </summary>
    public static async Task InitializeAsync(TimePayDbContext context)
    {
        // Ensure database and tables exist
        await context.Database.EnsureCreatedAsync();

        // Seed default settings if none exist
        if (!await context.Settings.AnyAsync())
        {
            context.Settings.Add(new Settings
            {
                CurrencyCode = "PHP",
                MinutesPerPeso = 4m,
                WarningMinutes1 = 10,
                WarningMinutes2 = 5,
                WarningMinutes3 = 1,
                SoundEnabled = true,
                AutoStartEnabled = true,
                PauseOnShutdown = false,
                AllowDecimalAmounts = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();
        }
    }
}
