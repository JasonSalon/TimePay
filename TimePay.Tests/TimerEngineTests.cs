using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.Core.Timer;
using TimePay.Data;
using TimePay.Data.Repositories;

namespace TimePay.Tests;

/// <summary>
/// Controllable clock for deterministic timer testing.
/// </summary>
public class TestClock : ISystemClock
{
    public DateTimeOffset CurrentTime { get; set; }

    public TestClock(DateTimeOffset initialTime)
    {
        CurrentTime = initialTime;
    }

    public DateTimeOffset UtcNow => CurrentTime;

    public void Advance(TimeSpan duration) => CurrentTime = CurrentTime.Add(duration);
    public void Rewind(TimeSpan duration) => CurrentTime = CurrentTime.Subtract(duration);
}

/// <summary>
/// Comprehensive test suite for Development Prompt 5: TIMER ENGINE.
/// </summary>
public class TimerEngineTests : IDisposable
{
    private readonly TimePayDbContext _context;
    private readonly SessionRepository _sessionRepo;
    private readonly SettingsRepository _settingsRepo;
    private readonly AuditLogRepository _auditRepo;
    private readonly TestClock _clock;

    public TimerEngineTests()
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
        _auditRepo = new AuditLogRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task TimerEngine_StartSessionAndTick()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var engine = new TimerEngine(_sessionRepo, _settingsRepo, _auditRepo, _clock);
        await engine.InitializeAsync();

        // Add 60 minutes
        await engine.AddTimeAsync(60);

        Assert.Equal(AppState.Active, engine.CurrentAppState);
        Assert.NotNull(engine.CurrentSession);
        Assert.Equal(TimeSpan.FromMinutes(60), engine.GetRemainingTime());

        // Advance clock by 10 minutes
        _clock.Advance(TimeSpan.FromMinutes(10));
        var tickResult = await engine.TickAsync();

        Assert.Equal(TimeSpan.FromMinutes(50), tickResult.RemainingTime);
        Assert.Equal(AppState.Active, tickResult.AppState);
    }

    [Fact]
    public async Task TimerEngine_FiresExpirationWhenTimeZero()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var engine = new TimerEngine(_sessionRepo, _settingsRepo, _auditRepo, _clock);
        await engine.InitializeAsync();

        await engine.AddTimeAsync(10); // 10 minutes

        bool expiredFired = false;
        engine.SessionExpired += (_, args) =>
        {
            expiredFired = true;
            Assert.Equal(AppState.Expired, args.AppState);
        };

        // Advance past expiration (11 minutes)
        _clock.Advance(TimeSpan.FromMinutes(11));
        var result = await engine.TickAsync();

        Assert.True(expiredFired);
        Assert.Equal(AppState.Expired, engine.CurrentAppState);
        Assert.Equal(SessionStatus.Expired, engine.CurrentSession!.Status);
        Assert.Equal(TimeSpan.Zero, result.RemainingTime);

        // Verify audit log
        var logs = await _auditRepo.GetLogsAsync(actionFilter: AuditAction.SessionExpired);
        Assert.Single(logs);
    }

    [Fact]
    public async Task TimerEngine_TriggersWarningThresholds()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var engine = new TimerEngine(_sessionRepo, _settingsRepo, _auditRepo, _clock);
        await engine.InitializeAsync();

        await engine.AddTimeAsync(20); // 20 minutes

        var triggeredWarnings = new List<int>();
        engine.WarningTriggered += (_, args) =>
        {
            if (args.TriggeredWarningMinutes.HasValue)
                triggeredWarnings.Add(args.TriggeredWarningMinutes.Value);
        };

        // 1. Advance to 10 minutes remaining
        _clock.Advance(TimeSpan.FromMinutes(10));
        await engine.TickAsync();
        Assert.Contains(10, triggeredWarnings);

        // 2. Advance to 5 minutes remaining (LowTime state)
        _clock.Advance(TimeSpan.FromMinutes(5));
        var tick5 = await engine.TickAsync();
        Assert.Contains(5, triggeredWarnings);
        Assert.Equal(AppState.LowTime, tick5.AppState);

        // 3. Advance to 1 minute remaining
        _clock.Advance(TimeSpan.FromMinutes(4));
        var tick1 = await engine.TickAsync();
        Assert.Contains(1, triggeredWarnings);
        Assert.Equal(AppState.LowTime, tick1.AppState);
    }

    [Fact]
    public async Task TimerEngine_PauseAndResume_PreservesExactTime()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var engine = new TimerEngine(_sessionRepo, _settingsRepo, _auditRepo, _clock);
        await engine.InitializeAsync();

        await engine.AddTimeAsync(60); // 60 minutes

        // 10 minutes elapse
        _clock.Advance(TimeSpan.FromMinutes(10));
        await engine.TickAsync();
        Assert.Equal(TimeSpan.FromMinutes(50), engine.GetRemainingTime());

        // Pause timer
        await engine.PauseSessionAsync();
        Assert.Equal(AppState.Paused, engine.CurrentAppState);

        // PC stays paused for 30 minutes
        _clock.Advance(TimeSpan.FromMinutes(30));
        await engine.TickAsync();
        // Remaining time must STILL be 50 minutes while paused
        Assert.Equal(TimeSpan.FromMinutes(50), engine.GetRemainingTime());

        // Resume timer
        await engine.ResumeSessionAsync();
        Assert.Equal(AppState.Active, engine.CurrentAppState);

        // 5 more minutes elapse
        _clock.Advance(TimeSpan.FromMinutes(5));
        await engine.TickAsync();
        Assert.Equal(TimeSpan.FromMinutes(45), engine.GetRemainingTime());
    }

    [Fact]
    public async Task TimerEngine_MultipleTimeAdditions_ExtendCorrectly()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var engine = new TimerEngine(_sessionRepo, _settingsRepo, _auditRepo, _clock);
        await engine.InitializeAsync();

        await engine.AddTimeAsync(30); // 30 min
        Assert.Equal(TimeSpan.FromMinutes(30), engine.GetRemainingTime());

        _clock.Advance(TimeSpan.FromMinutes(10));
        await engine.TickAsync();
        Assert.Equal(TimeSpan.FromMinutes(20), engine.GetRemainingTime());

        // Admin adds 40 more minutes
        await engine.AddTimeAsync(40);
        Assert.Equal(TimeSpan.FromMinutes(60), engine.GetRemainingTime());
    }

    [Fact]
    public async Task TimerEngine_DetectsClockTampering_AndDefendsExpiration()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var engine = new TimerEngine(_sessionRepo, _settingsRepo, _auditRepo, _clock);
        await engine.InitializeAsync();

        await engine.AddTimeAsync(60); // 60 minutes
        await engine.TickAsync();

        bool tamperDetected = false;
        engine.ClockTamperDetected += (_, args) =>
        {
            tamperDetected = true;
            Assert.True(args.BackwardJump >= TimeSpan.FromMinutes(30));
        };

        // User changes Windows system clock 30 minutes BACKWARDS
        _clock.Rewind(TimeSpan.FromMinutes(30));
        await engine.TickAsync();

        Assert.True(tamperDetected);

        // Verify audit log created for clock change
        var logs = await _auditRepo.GetLogsAsync(actionFilter: AuditAction.ClockChangeDetected);
        Assert.Single(logs);

        // User should NOT gain 30 minutes free time; remaining time is defended
        Assert.True(engine.GetRemainingTime() <= TimeSpan.FromMinutes(60));
    }

    [Fact]
    public async Task TimerEngine_StartupRecovery_HandlesExpiredSession()
    {
        await DatabaseInitializer.InitializeAsync(_context);

        // Session created in past that has already expired while app was closed
        var session = await _sessionRepo.StartSessionAsync(30);
        _clock.Advance(TimeSpan.FromMinutes(45)); // 45 min pass

        // New engine initialization on app reboot
        var engine = new TimerEngine(_sessionRepo, _settingsRepo, _auditRepo, _clock);
        await engine.InitializeAsync();

        Assert.Equal(AppState.Expired, engine.CurrentAppState);
        Assert.Equal(SessionStatus.Expired, engine.CurrentSession!.Status);
        Assert.Equal(TimeSpan.Zero, engine.GetRemainingTime());
    }

    [Fact]
    public async Task TimerEngine_LockAndResetSession()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var engine = new TimerEngine(_sessionRepo, _settingsRepo, _auditRepo, _clock);
        await engine.InitializeAsync();

        await engine.AddTimeAsync(60);
        Assert.Equal(AppState.Active, engine.CurrentAppState);

        // Manual lock
        await engine.LockSessionAsync();
        Assert.Equal(AppState.Locked, engine.CurrentAppState);
        Assert.Equal(SessionStatus.Locked, engine.CurrentSession!.Status);

        // Reset session
        await engine.ResetSessionAsync();
        Assert.Null(engine.CurrentSession);
        Assert.Equal(AppState.Locked, engine.CurrentAppState);
    }
}
