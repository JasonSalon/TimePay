using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.Data;
using TimePay.Data.Repositories;

namespace TimePay.Tests;

/// <summary>
/// Tests for Development Prompt 12: AUDIT LOG VIEWER & AUDITING.
/// </summary>
public class AuditLogViewerTests : IDisposable
{
    private readonly TimePayDbContext _context;
    private readonly AuditLogRepository _auditRepo;

    public AuditLogViewerTests()
    {
        var options = new DbContextOptionsBuilder<TimePayDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new TimePayDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _auditRepo = new AuditLogRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task AuditLogs_FilterByAction_ReturnsExpectedEvents()
    {
        await _auditRepo.LogAsync(AuditAction.AdminLoginSuccess, "admin", "Successful login");
        await _auditRepo.LogAsync(AuditAction.AdminLoginFailed, "guest", "Wrong password attempt");
        await _auditRepo.LogAsync(AuditAction.RateChanged, "admin", "Changed rate to 5m/PHP");
        await _auditRepo.LogAsync(AuditAction.ClockChangeDetected, "SYSTEM", "Backward jump of 1800s");

        var allLogs = await _auditRepo.GetLogsAsync();
        Assert.Equal(4, allLogs.Count);

        var loginFailedLogs = await _auditRepo.GetLogsAsync(actionFilter: AuditAction.AdminLoginFailed);
        Assert.Single(loginFailedLogs);
        Assert.Equal("guest", loginFailedLogs[0].Username);

        var clockLogs = await _auditRepo.GetLogsAsync(actionFilter: AuditAction.ClockChangeDetected);
        Assert.Single(clockLogs);
        Assert.Equal("SYSTEM", clockLogs[0].Username);
    }

    [Fact]
    public async Task AuditLogs_MaxResultsLimit_Respected()
    {
        for (int i = 1; i <= 20; i++)
        {
            await _auditRepo.LogAsync(AuditAction.TimeAdded, "admin", $"Added time #{i}");
        }

        var limited = await _auditRepo.GetLogsAsync(maxResults: 5);
        Assert.Equal(5, limited.Count);
    }
}
