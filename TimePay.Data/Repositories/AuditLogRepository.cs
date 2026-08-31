using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;

namespace TimePay.Data.Repositories;

/// <summary>
/// Records audit trail for all important system and admin actions.
/// </summary>
public class AuditLogRepository : IAuditLogger
{
    private readonly TimePayDbContext _context;

    public AuditLogRepository(TimePayDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task LogAsync(AuditAction action, string username, string details)
    {
        var log = new AuditLog
        {
            Action = action,
            Username = string.IsNullOrWhiteSpace(username) ? "SYSTEM" : username,
            Details = details.Length > 500 ? details[..500] : details,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<List<AuditLog>> GetLogsAsync(
        AuditAction? actionFilter = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int maxResults = 100)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (actionFilter.HasValue)
            query = query.Where(l => l.Action == actionFilter.Value);

        if (from.HasValue)
            query = query.Where(l => l.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.CreatedAt <= to.Value);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(maxResults)
            .ToListAsync();
    }
}
