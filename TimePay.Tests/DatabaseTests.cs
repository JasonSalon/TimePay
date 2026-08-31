using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.Core.Security;
using TimePay.Core.TimeCalculation;
using TimePay.Data;
using TimePay.Data.Repositories;

namespace TimePay.Tests;

/// <summary>
/// Tests database creation, CRUD operations, and core logic.
/// Uses in-memory SQLite for isolation.
/// </summary>
public class DatabaseTests : IDisposable
{
    private readonly TimePayDbContext _context;

    public DatabaseTests()
    {
        var options = new DbContextOptionsBuilder<TimePayDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new TimePayDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    // ===================== DATABASE INITIALIZATION =====================

    [Fact]
    public async Task DatabaseInitializer_SeedsDefaultSettings()
    {
        await DatabaseInitializer.InitializeAsync(_context);

        var settings = await _context.Settings.FirstOrDefaultAsync();
        Assert.NotNull(settings);
        Assert.Equal("PHP", settings.CurrencyCode);
        Assert.Equal(4m, settings.MinutesPerPeso);
        Assert.Equal(10, settings.WarningMinutes1);
        Assert.Equal(5, settings.WarningMinutes2);
        Assert.Equal(1, settings.WarningMinutes3);
        Assert.True(settings.SoundEnabled);
        Assert.False(settings.PauseOnShutdown);
    }

    [Fact]
    public async Task DatabaseInitializer_DoesNotDuplicateSettings()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        await DatabaseInitializer.InitializeAsync(_context); // Second call

        var count = await _context.Settings.CountAsync();
        Assert.Equal(1, count);
    }

    // ===================== SETTINGS CRUD =====================

    [Fact]
    public async Task SettingsRepository_GetAndUpdate()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var repo = new SettingsRepository(_context);

        var settings = await repo.GetSettingsAsync();
        Assert.Equal(4m, settings.MinutesPerPeso);

        settings.MinutesPerPeso = 5m;
        settings.CurrencyCode = "USD";
        var updated = await repo.UpdateSettingsAsync(settings);

        Assert.Equal(5m, updated.MinutesPerPeso);
        Assert.Equal("USD", updated.CurrencyCode);
    }

    [Fact]
    public async Task SettingsRepository_GetCurrency()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var repo = new SettingsRepository(_context);

        var currency = await repo.GetCurrencyAsync();
        Assert.Equal("PHP", currency.Code);
        Assert.Equal("₱", currency.Symbol);
    }

    // ===================== AUTHENTICATION =====================

    [Fact]
    public async Task AuthRepository_CreateAndValidate()
    {
        var repo = new AuthRepository(_context);

        var admin = await repo.CreateAdminAsync("admin", "SecurePass123!");
        Assert.NotNull(admin);
        Assert.Equal("admin", admin.Username);
        Assert.NotEmpty(admin.PasswordHash);
        Assert.NotEmpty(admin.PasswordSalt);

        // Validate correct password
        var result = await repo.ValidateLoginAsync("admin", "SecurePass123!");
        Assert.NotNull(result);
        Assert.Equal("admin", result!.Username);

        // Validate wrong password
        var failed = await repo.ValidateLoginAsync("admin", "WrongPassword");
        Assert.Null(failed);
    }

    [Fact]
    public async Task AuthRepository_CaseInsensitiveUsername()
    {
        var repo = new AuthRepository(_context);
        await repo.CreateAdminAsync("Admin", "pass123");

        var result = await repo.ValidateLoginAsync("admin", "pass123");
        Assert.NotNull(result);

        var result2 = await repo.ValidateLoginAsync("ADMIN", "pass123");
        Assert.NotNull(result2);
    }

    [Fact]
    public async Task AuthRepository_PreventsDuplicateUsername()
    {
        var repo = new AuthRepository(_context);
        await repo.CreateAdminAsync("admin", "pass123");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.CreateAdminAsync("admin", "different"));
    }

    [Fact]
    public async Task AuthRepository_EmptyCredentialsReturnNull()
    {
        var repo = new AuthRepository(_context);

        var result = await repo.ValidateLoginAsync("", "pass");
        Assert.Null(result);

        var result2 = await repo.ValidateLoginAsync("admin", "");
        Assert.Null(result2);
    }

    [Fact]
    public async Task AuthRepository_ChangePassword()
    {
        var repo = new AuthRepository(_context);
        var admin = await repo.CreateAdminAsync("admin", "OldPass123");

        var changed = await repo.ChangePasswordAsync(admin.Id, "OldPass123", "NewPass456");
        Assert.True(changed);

        // Old password should fail
        var oldLogin = await repo.ValidateLoginAsync("admin", "OldPass123");
        Assert.Null(oldLogin);

        // New password should work
        var newLogin = await repo.ValidateLoginAsync("admin", "NewPass456");
        Assert.NotNull(newLogin);
    }

    [Fact]
    public async Task AuthRepository_AnyAdminExists()
    {
        var repo = new AuthRepository(_context);

        Assert.False(await repo.AnyAdminExistsAsync());

        await repo.CreateAdminAsync("admin", "pass");

        Assert.True(await repo.AnyAdminExistsAsync());
    }

    // ===================== SESSION MANAGEMENT =====================

    [Fact]
    public async Task SessionRepository_StartSession()
    {
        var repo = new SessionRepository(_context);

        var session = await repo.StartSessionAsync(80);
        Assert.NotNull(session);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.StartsWith("TP-", session.SessionId);
        Assert.True(session.ExpirationAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task SessionRepository_AddTimeExtendsExpiration()
    {
        var repo = new SessionRepository(_context);

        var session = await repo.StartSessionAsync(60); // 1 hour
        var originalExpiration = session.ExpirationAt;

        session = await repo.AddTimeAsync(30); // +30 min
        Assert.True(session.ExpirationAt > originalExpiration);

        var diff = (session.ExpirationAt - originalExpiration).TotalMinutes;
        Assert.Equal(30, diff, precision: 1);
    }

    [Fact]
    public async Task SessionRepository_PauseAndResume()
    {
        var repo = new SessionRepository(_context);

        await repo.StartSessionAsync(60);
        var remaining1 = await repo.GetRemainingTimeAsync();

        var paused = await repo.PauseSessionAsync();
        Assert.Equal(SessionStatus.Paused, paused.Status);
        Assert.NotNull(paused.PausedAt);

        // Remaining time should be preserved when paused
        var remainingPaused = await repo.GetRemainingTimeAsync();
        Assert.True(remainingPaused.TotalMinutes > 0);

        var resumed = await repo.ResumeSessionAsync();
        Assert.Equal(SessionStatus.Active, resumed.Status);
        Assert.Null(resumed.PausedAt);
    }

    [Fact]
    public async Task SessionRepository_ExpireSession()
    {
        var repo = new SessionRepository(_context);
        await repo.StartSessionAsync(60);

        var expired = await repo.ExpireSessionAsync();
        Assert.Equal(SessionStatus.Expired, expired.Status);
    }

    [Fact]
    public async Task SessionRepository_LockSession()
    {
        var repo = new SessionRepository(_context);
        await repo.StartSessionAsync(60);

        var locked = await repo.LockSessionAsync();
        Assert.Equal(SessionStatus.Locked, locked.Status);
    }

    [Fact]
    public async Task SessionRepository_GetRemainingTime()
    {
        var repo = new SessionRepository(_context);
        await repo.StartSessionAsync(120); // 2 hours

        var remaining = await repo.GetRemainingTimeAsync();
        // Should be approximately 120 minutes (allow small variance)
        Assert.True(remaining.TotalMinutes > 119 && remaining.TotalMinutes <= 120);
    }

    [Fact]
    public async Task SessionRepository_NoSessionReturnsZeroTime()
    {
        var repo = new SessionRepository(_context);
        var remaining = await repo.GetRemainingTimeAsync();
        Assert.Equal(TimeSpan.Zero, remaining);
    }

    // ===================== TRANSACTIONS =====================

    [Fact]
    public async Task TransactionRepository_CreateAndRetrieve()
    {
        var authRepo = new AuthRepository(_context);
        var admin = await authRepo.CreateAdminAsync("admin", "pass");

        var sessionRepo = new SessionRepository(_context);
        var session = await sessionRepo.StartSessionAsync(60);

        var txnRepo = new TransactionRepository(_context);
        var txn = await txnRepo.CreateTransactionAsync(new Transaction
        {
            SessionId = session.Id,
            AdminUserId = admin.Id,
            Amount = 20m,
            MinutesPerPeso = 4m,
            PreviousExpiration = session.ExpirationAt,
            NewExpiration = session.ExpirationAt.AddMinutes(80)
        });

        Assert.NotNull(txn);
        Assert.StartsWith("TXN-", txn.TransactionId);
        Assert.Equal(80m, txn.MinutesAdded); // 20 × 4
        Assert.Equal(20m, txn.Amount);
        Assert.Equal(4m, txn.MinutesPerPeso);

        // Retrieve
        var list = await txnRepo.GetTransactionsAsync();
        Assert.Single(list);

        var found = await txnRepo.GetByTransactionIdAsync(txn.TransactionId);
        Assert.NotNull(found);
    }

    [Fact]
    public async Task TransactionRepository_RateVersioning()
    {
        var authRepo = new AuthRepository(_context);
        var admin = await authRepo.CreateAdminAsync("admin", "pass");

        var sessionRepo = new SessionRepository(_context);
        var session = await sessionRepo.StartSessionAsync(60);

        var txnRepo = new TransactionRepository(_context);

        // Transaction at rate 4
        var txn1 = await txnRepo.CreateTransactionAsync(new Transaction
        {
            SessionId = session.Id,
            AdminUserId = admin.Id,
            Amount = 10m,
            MinutesPerPeso = 4m,
            PreviousExpiration = session.ExpirationAt,
            NewExpiration = session.ExpirationAt.AddMinutes(40)
        });
        Assert.Equal(40m, txn1.MinutesAdded);

        // Transaction at rate 5 (rate changed)
        var txn2 = await txnRepo.CreateTransactionAsync(new Transaction
        {
            SessionId = session.Id,
            AdminUserId = admin.Id,
            Amount = 10m,
            MinutesPerPeso = 5m,
            PreviousExpiration = session.ExpirationAt,
            NewExpiration = session.ExpirationAt.AddMinutes(50)
        });
        Assert.Equal(50m, txn2.MinutesAdded);

        // Old transaction retains its original rate
        var old = await txnRepo.GetByTransactionIdAsync(txn1.TransactionId);
        Assert.NotNull(old);
        Assert.Equal(4m, old!.MinutesPerPeso);
        Assert.Equal(40m, old.MinutesAdded);
    }

    // ===================== AUDIT LOGS =====================

    [Fact]
    public async Task AuditLogRepository_LogAndRetrieve()
    {
        var repo = new AuditLogRepository(_context);

        await repo.LogAsync(AuditAction.AdminLoginSuccess, "admin", "Logged in successfully");
        await repo.LogAsync(AuditAction.TimeAdded, "admin", "₱20 → 80 minutes");
        await repo.LogAsync(AuditAction.AdminLoginFailed, "unknown", "Invalid password");

        var all = await repo.GetLogsAsync();
        Assert.Equal(3, all.Count);

        var loginOnly = await repo.GetLogsAsync(actionFilter: AuditAction.AdminLoginSuccess);
        Assert.Single(loginOnly);
        Assert.Equal("admin", loginOnly[0].Username);
    }

    // ===================== TIME CALCULATOR =====================

    [Fact]
    public void TimeCalculator_CorrectConversions()
    {
        var calc = new TimeCalculator();

        Assert.Equal(4m, calc.CalculateMinutes(1m, 4m));
        Assert.Equal(20m, calc.CalculateMinutes(5m, 4m));
        Assert.Equal(40m, calc.CalculateMinutes(10m, 4m));
        Assert.Equal(80m, calc.CalculateMinutes(20m, 4m));
        Assert.Equal(200m, calc.CalculateMinutes(50m, 4m));
        Assert.Equal(400m, calc.CalculateMinutes(100m, 4m));
    }

    [Fact]
    public void TimeCalculator_DecimalAmounts()
    {
        var calc = new TimeCalculator();
        Assert.Equal(10m, calc.CalculateMinutes(2.5m, 4m));
    }

    [Fact]
    public void TimeCalculator_FormatTime()
    {
        var calc = new TimeCalculator();

        Assert.Equal("4 minutes", calc.FormatTime(4m));
        Assert.Equal("20 minutes", calc.FormatTime(20m));
        Assert.Equal("1 hour 20 minutes", calc.FormatTime(80m));
        Assert.Equal("3 hours 20 minutes", calc.FormatTime(200m));
        Assert.Equal("6 hours 40 minutes", calc.FormatTime(400m));
    }

    [Fact]
    public void TimeCalculator_FormatTimeSpan()
    {
        var calc = new TimeCalculator();

        Assert.Equal("01:20:00", calc.FormatTimeSpan(TimeSpan.FromMinutes(80)));
        Assert.Equal("00:40:00", calc.FormatTimeSpan(TimeSpan.FromMinutes(40)));
        Assert.Equal("00:00:00", calc.FormatTimeSpan(TimeSpan.Zero));
    }

    [Fact]
    public void TimeCalculator_InvalidInputs()
    {
        var calc = new TimeCalculator();

        Assert.Throws<ArgumentOutOfRangeException>(() => calc.CalculateMinutes(-1m, 4m));
        Assert.Throws<ArgumentOutOfRangeException>(() => calc.CalculateMinutes(10m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => calc.CalculateMinutes(10m, -1m));
    }

    // ===================== PASSWORD HASHER =====================

    [Fact]
    public void PasswordHasher_HashAndVerify()
    {
        var (hash, salt) = PasswordHasher.HashPassword("MySecurePassword");

        Assert.NotEmpty(hash);
        Assert.NotEmpty(salt);
        Assert.True(PasswordHasher.VerifyPassword("MySecurePassword", hash, salt));
        Assert.False(PasswordHasher.VerifyPassword("WrongPassword", hash, salt));
    }

    [Fact]
    public void PasswordHasher_UniqueSalts()
    {
        var (hash1, salt1) = PasswordHasher.HashPassword("SamePassword");
        var (hash2, salt2) = PasswordHasher.HashPassword("SamePassword");

        // Same password should produce different hashes due to unique salts
        Assert.NotEqual(salt1, salt2);
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void PasswordHasher_EmptyPasswordThrows()
    {
        Assert.Throws<ArgumentException>(() => PasswordHasher.HashPassword(""));
        Assert.Throws<ArgumentException>(() => PasswordHasher.HashPassword(null!));
    }
}
