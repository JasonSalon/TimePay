using TimePay.Core.Models;

namespace TimePay.Core.Interfaces;

/// <summary>
/// Records audit trail of all important system actions.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs an audit event.
    /// </summary>
    Task LogAsync(AuditAction action, string username, string details);

    /// <summary>
    /// Gets audit log entries with optional filtering.
    /// </summary>
    Task<List<AuditLog>> GetLogsAsync(AuditAction? actionFilter = null, DateTimeOffset? from = null, DateTimeOffset? to = null, int maxResults = 100);
}
