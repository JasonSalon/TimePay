using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.Data;
using TimePay.Data.Repositories;

namespace TimePay.Tests;

/// <summary>
/// Tests for Time Configuration per Development Prompt 4.
/// Tests rate changes, validation, audit logs, and immunity of historical records.
/// </summary>
public class TimeConfigurationTests : IDisposable
{
    private readonly TimePayDbContext _context;
    private readonly SettingsRepository _settingsRepo;
    private readonly AuditLogRepository _auditRepo;
    private readonly SessionRepository _sessionRepo;
    private readonly TransactionRepository _txnRepo;
    private readonly AuthRepository _authRepo;

    public TimeConfigurationTests()
    {
        var options = new DbContextOptionsBuilder<TimePayDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new TimePayDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _settingsRepo = new SettingsRepository(_context);
        _auditRepo = new AuditLogRepository(_context);
        _sessionRepo = new SessionRepository(_context);
        _txnRepo = new TransactionRepository(_context);
        _authRepo = new AuthRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task RateUpdate_PersistsCorrectly()
    {
        await DatabaseInitializer.InitializeAsync(_context);

        var settings = await _settingsRepo.GetSettingsAsync();
        settings.MinutesPerPeso = 5m;
        settings.CurrencyCode = "USD";
        var updated = await _settingsRepo.UpdateSettingsAsync(settings);

        Assert.Equal(5m, updated.MinutesPerPeso);
        Assert.Equal("USD", updated.CurrencyCode);

        var retrieved = await _settingsRepo.GetSettingsAsync();
        Assert.Equal(5m, retrieved.MinutesPerPeso);
        Assert.Equal("USD", retrieved.CurrencyCode);
    }

    [Fact]
    public async Task RateChange_RecordsAuditLog()
    {
        await DatabaseInitializer.InitializeAsync(_context);

        await _auditRepo.LogAsync(
            AuditAction.RateChanged,
            "admin",
            "Rate changed from PHP 1=4m to PHP 1=5m");

        var logs = await _auditRepo.GetLogsAsync(actionFilter: AuditAction.RateChanged);
        Assert.Single(logs);
        Assert.Equal("admin", logs[0].Username);
        Assert.Contains("PHP 1=5m", logs[0].Details);
    }

    [Fact]
    public async Task RateChange_DoesNotRetroactivelyModifyExistingSession()
    {
        await DatabaseInitializer.InitializeAsync(_context);

        // Start a session with 80 minutes (e.g. ₱20 @ 4min/peso)
        var session = await _sessionRepo.StartSessionAsync(80);
        var expectedExpiration = session.ExpirationAt;

        // Change global rate to 5min/peso
        var settings = await _settingsRepo.GetSettingsAsync();
        settings.MinutesPerPeso = 5m;
        await _settingsRepo.UpdateSettingsAsync(settings);

        // Active session expiration timestamp must remain unchanged
        var activeSession = await _sessionRepo.GetCurrentSessionAsync();
        Assert.NotNull(activeSession);
        Assert.Equal(expectedExpiration, activeSession!.ExpirationAt);
    }

    [Fact]
    public async Task RateChange_DoesNotRetroactivelyModifyPastTransactions()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var admin = await _authRepo.CreateAdminAsync("admin", "Pass123!");
        var session = await _sessionRepo.StartSessionAsync(60);

        // Transaction recorded with rate = 4
        var txn1 = await _txnRepo.CreateTransactionAsync(new Transaction
        {
            SessionId = session.Id,
            AdminUserId = admin.Id,
            Amount = 25m,
            MinutesPerPeso = 4m,
            PreviousExpiration = session.ExpirationAt,
            NewExpiration = session.ExpirationAt.AddMinutes(100)
        });

        // Change global settings to rate = 6
        var settings = await _settingsRepo.GetSettingsAsync();
        settings.MinutesPerPeso = 6m;
        await _settingsRepo.UpdateSettingsAsync(settings);

        // Verify past transaction still has original rate 4 and 100 minutes added
        var loadedTxn = await _txnRepo.GetByTransactionIdAsync(txn1.TransactionId);
        Assert.NotNull(loadedTxn);
        Assert.Equal(4m, loadedTxn!.MinutesPerPeso);
        Assert.Equal(100m, loadedTxn.MinutesAdded);
    }
}
