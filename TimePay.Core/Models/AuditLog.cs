namespace TimePay.Core.Models;

/// <summary>
/// Records important system and administrative actions for auditing.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    /// <summary>
    /// The type of action performed.
    /// </summary>
    public AuditAction Action { get; set; }

    /// <summary>
    /// The username who performed the action (TimePay admin username or "SYSTEM").
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable details of the action.
    /// </summary>
    public string Details { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
