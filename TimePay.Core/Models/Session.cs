namespace TimePay.Core.Models;

/// <summary>
/// Represents a computer usage session.
/// Uses expiration timestamps rather than decrementing counters.
/// </summary>
public class Session
{
    public int Id { get; set; }

    /// <summary>
    /// Human-readable session identifier (e.g., "TP-20260831-00001").
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// When this session was started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// When this session expires. This is the source of truth for remaining time.
    /// Extended when more time is added. Compared against current time to calculate remaining.
    /// </summary>
    public DateTimeOffset ExpirationAt { get; set; }

    /// <summary>
    /// Current session status.
    /// </summary>
    public SessionStatus Status { get; set; } = SessionStatus.Locked;

    /// <summary>
    /// When the session was paused (null if not paused).
    /// Used to calculate how much time to restore when resuming.
    /// </summary>
    public DateTimeOffset? PausedAt { get; set; }

    /// <summary>
    /// Accumulated paused duration in seconds.
    /// Used to correctly extend expiration when resuming from pause.
    /// </summary>
    public long AccumulatedPauseSeconds { get; set; } = 0;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
