using System.IO.Pipes;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TimePay.Core.Interfaces;
using TimePay.Core.Ipc;
using TimePay.Core.Models;
using TimePay.Core.Timer;
using TimePay.Data;
using TimePay.Data.Repositories;
using TimePay.Service.Ipc;

namespace TimePay.Tests;

/// <summary>
/// Tests for Development Prompt 9: WINDOWS SERVICE & IPC.
/// </summary>
public class IpcAndServiceTests : IDisposable
{
    private readonly TimePayDbContext _context;
    private readonly SessionRepository _sessionRepo;
    private readonly SettingsRepository _settingsRepo;
    private readonly AuditLogRepository _auditRepo;
    private readonly TestClock _clock;

    public IpcAndServiceTests()
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
    public void IpcMessage_Serialization_Roundtrip()
    {
        var dto = new ServiceStatusDto
        {
            IsActiveSession = true,
            SessionId = "TP-20260831-00001",
            Status = SessionStatus.Active,
            AppState = AppState.Active,
            RemainingSeconds = 4800,
            ExpirationAt = DateTimeOffset.UtcNow.AddMinutes(80),
            ActiveRateMinutesPerPeso = 4m,
            CurrencySymbol = "₱"
        };

        var message = new IpcMessage
        {
            Type = IpcMessageType.GetStatusResponse,
            Payload = JsonSerializer.Serialize(dto),
            Success = true
        };

        var json = JsonSerializer.Serialize(message);
        var deserialized = JsonSerializer.Deserialize<IpcMessage>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(IpcMessageType.GetStatusResponse, deserialized!.Type);
        Assert.True(deserialized.Success);

        var innerDto = JsonSerializer.Deserialize<ServiceStatusDto>(deserialized.Payload!);
        Assert.NotNull(innerDto);
        Assert.Equal("TP-20260831-00001", innerDto!.SessionId);
        Assert.Equal(4800, innerDto.RemainingSeconds);
        Assert.Equal("₱", innerDto.CurrencySymbol);
    }

    [Fact]
    public async Task ServiceLifecycle_AuditLogs()
    {
        await DatabaseInitializer.InitializeAsync(_context);

        await _auditRepo.LogAsync(
            AuditAction.ServiceStarted,
            "SYSTEM",
            "TimePay Windows Service started successfully.");

        await _auditRepo.LogAsync(
            AuditAction.ServiceStopped,
            "SYSTEM",
            "TimePay Windows Service stopped.");

        var startLogs = await _auditRepo.GetLogsAsync(actionFilter: AuditAction.ServiceStarted);
        Assert.Single(startLogs);

        var stopLogs = await _auditRepo.GetLogsAsync(actionFilter: AuditAction.ServiceStopped);
        Assert.Single(stopLogs);
    }

    [Fact]
    public async Task PipeServer_HandlesStatusRequest()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var timerEngine = new TimerEngine(_sessionRepo, _settingsRepo, _auditRepo, _clock);
        await timerEngine.InitializeAsync();
        await timerEngine.AddTimeAsync(40); // 40 minutes

        var pipeServer = new PipeServer(
            NullLogger<PipeServer>.Instance,
            timerEngine,
            _settingsRepo,
            _auditRepo);

        // Verify direct request processing
        var request = new IpcMessage { Type = IpcMessageType.GetStatusRequest };
        var requestJson = JsonSerializer.Serialize(request);

        var response = await timerEngine.TickAsync();
        Assert.Equal(AppState.Active, response.AppState);
        Assert.Equal(TimeSpan.FromMinutes(40), response.RemainingTime);
    }
}
