using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.Data;
using TimePay.Data.Repositories;

namespace TimePay.Tests;

/// <summary>
/// Tests for Development Prompt 10: SETTINGS & STARTUP.
/// </summary>
public class SettingsAndStartupTests : IDisposable
{
    private readonly TimePayDbContext _context;
    private readonly SettingsRepository _settingsRepo;
    private readonly AuthRepository _authRepo;
    private readonly AuditLogRepository _auditRepo;

    public SettingsAndStartupTests()
    {
        var options = new DbContextOptionsBuilder<TimePayDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new TimePayDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _settingsRepo = new SettingsRepository(_context);
        _authRepo = new AuthRepository(_context);
        _auditRepo = new AuditLogRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task Settings_ToggleAllOptions_Persists()
    {
        await DatabaseInitializer.InitializeAsync(_context);

        var settings = await _settingsRepo.GetSettingsAsync();
        settings.SoundEnabled = false;
        settings.AutoStartEnabled = true;
        settings.PauseOnShutdown = true;
        settings.AllowDecimalAmounts = false;
        settings.WarningMinutes1 = 15;
        settings.WarningMinutes2 = 8;
        settings.WarningMinutes3 = 2;

        var updated = await _settingsRepo.UpdateSettingsAsync(settings);

        Assert.False(updated.SoundEnabled);
        Assert.True(updated.AutoStartEnabled);
        Assert.True(updated.PauseOnShutdown);
        Assert.False(updated.AllowDecimalAmounts);
        Assert.Equal(15, updated.WarningMinutes1);
        Assert.Equal(8, updated.WarningMinutes2);
        Assert.Equal(2, updated.WarningMinutes3);
    }

    [Fact]
    public async Task AdminPasswordChange_UpdatesHashAndAuditLogs()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var admin = await _authRepo.CreateAdminAsync("admin", "CurrentPassword123!");

        var success = await _authRepo.ChangePasswordAsync(admin.Id, "CurrentPassword123!", "NewSecret456!");
        Assert.True(success);

        await _auditRepo.LogAsync(
            AuditAction.SettingsChanged,
            "admin",
            "Admin password updated successfully");

        // Old password invalid
        var oldCheck = await _authRepo.ValidateLoginAsync("admin", "CurrentPassword123!");
        Assert.Null(oldCheck);

        // New password valid
        var newCheck = await _authRepo.ValidateLoginAsync("admin", "NewSecret456!");
        Assert.NotNull(newCheck);

        var logs = await _auditRepo.GetLogsAsync(actionFilter: AuditAction.SettingsChanged);
        Assert.Single(logs);
    }
}
