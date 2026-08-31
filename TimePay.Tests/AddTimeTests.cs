using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.Core.TimeCalculation;
using TimePay.Data;
using TimePay.Data.Repositories;

namespace TimePay.Tests;

/// <summary>
/// Tests for Development Prompt 6: ADD TIME workflow.
/// </summary>
public class AddTimeWorkflowTests : IDisposable
{
    private readonly TimePayDbContext _context;
    private readonly SessionRepository _sessionRepo;
    private readonly SettingsRepository _settingsRepo;
    private readonly TransactionRepository _txnRepo;
    private readonly AuditLogRepository _auditRepo;
    private readonly AuthRepository _authRepo;
    private readonly TimeCalculator _timeCalc;
    private readonly TestClock _clock;

    public AddTimeWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<TimePayDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new TimePayDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _clock = new TestClock(new DateTimeOffset(2026, 8, 31, 13, 0, 0, TimeSpan.Zero));
        _sessionRepo = new SessionRepository(_context, _clock);
        _settingsRepo = new SettingsRepository(_context);
        _txnRepo = new TransactionRepository(_context);
        _auditRepo = new AuditLogRepository(_context);
        _authRepo = new AuthRepository(_context);
        _timeCalc = new TimeCalculator();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task AddTime_CreatesSessionAndTransaction()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var admin = await _authRepo.CreateAdminAsync("admin", "Pass123!");
        var settings = await _settingsRepo.GetSettingsAsync();

        decimal amount = 20m; // ₱20
        decimal minutesToAdd = _timeCalc.CalculateMinutes(amount, settings.MinutesPerPeso); // 80 min

        var session = await _sessionRepo.StartSessionAsync(minutesToAdd);
        var txn = await _txnRepo.CreateTransactionAsync(new Transaction
        {
            SessionId = session.Id,
            AdminUserId = admin.Id,
            Amount = amount,
            MinutesPerPeso = settings.MinutesPerPeso,
            MinutesAdded = minutesToAdd,
            PreviousExpiration = session.StartedAt,
            NewExpiration = session.ExpirationAt
        });

        await _auditRepo.LogAsync(
            AuditAction.TimeAdded,
            admin.Username,
            $"Added ₱{amount} ({minutesToAdd} mins)");

        // Verify session
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(_clock.UtcNow.AddMinutes(80), session.ExpirationAt);

        // Verify transaction
        var savedTxn = await _txnRepo.GetByTransactionIdAsync(txn.TransactionId);
        Assert.NotNull(savedTxn);
        Assert.Equal(20m, savedTxn!.Amount);
        Assert.Equal(4m, savedTxn.MinutesPerPeso);
        Assert.Equal(80m, savedTxn.MinutesAdded);

        // Verify audit log
        var logs = await _auditRepo.GetLogsAsync(actionFilter: AuditAction.TimeAdded);
        Assert.Single(logs);
        Assert.Equal("admin", logs[0].Username);
    }

    [Fact]
    public async Task AddTimeToActiveSession_ExtendsExpiration()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var admin = await _authRepo.CreateAdminAsync("admin", "Pass123!");

        // Start with 60 minutes
        var session = await _sessionRepo.StartSessionAsync(60);
        var initialExpiration = session.ExpirationAt;

        // 20 minutes elapse
        _clock.Advance(TimeSpan.FromMinutes(20));

        // Add ₱10 @ 4min/peso = 40 minutes
        decimal amount = 10m;
        decimal minutesToAdd = _timeCalc.CalculateMinutes(amount, 4m);
        var previousExpiration = session.ExpirationAt;

        var updatedSession = await _sessionRepo.AddTimeAsync(minutesToAdd);

        await _txnRepo.CreateTransactionAsync(new Transaction
        {
            SessionId = updatedSession.Id,
            AdminUserId = admin.Id,
            Amount = amount,
            MinutesPerPeso = 4m,
            MinutesAdded = minutesToAdd,
            PreviousExpiration = previousExpiration,
            NewExpiration = updatedSession.ExpirationAt
        });

        // New expiration must be initial expiration + 40 minutes
        Assert.Equal(initialExpiration.AddMinutes(40), updatedSession.ExpirationAt);

        // Remaining time should now be (40 remaining from initial + 40 added = 80 minutes)
        var remaining = await _sessionRepo.GetRemainingTimeAsync();
        Assert.Equal(TimeSpan.FromMinutes(80), remaining);
    }

    [Fact]
    public async Task AddTimeToExpiredSession_StartsFreshFromNow()
    {
        await DatabaseInitializer.InitializeAsync(_context);

        // Start session with 10 minutes
        var session = await _sessionRepo.StartSessionAsync(10);

        // 30 minutes elapse (session expired 20 minutes ago)
        _clock.Advance(TimeSpan.FromMinutes(30));

        // Admin adds ₱20 @ 4min/peso = 80 minutes
        var updatedSession = await _sessionRepo.AddTimeAsync(80);

        // Expiration must start fresh from CURRENT time + 80 minutes
        Assert.Equal(_clock.UtcNow.AddMinutes(80), updatedSession.ExpirationAt);
        Assert.Equal(SessionStatus.Active, updatedSession.Status);

        var remaining = await _sessionRepo.GetRemainingTimeAsync();
        Assert.Equal(TimeSpan.FromMinutes(80), remaining);
    }
}
