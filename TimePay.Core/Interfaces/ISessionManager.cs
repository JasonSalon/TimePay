using TimePay.Core.Models;

namespace TimePay.Core.Interfaces;

/// <summary>
/// Manages the timer session — starting, pausing, adding time, and expiration.
/// Uses expiration timestamps, not decrementing counters.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Gets the current active session, or null if none exists.
    /// </summary>
    Task<Session?> GetCurrentSessionAsync();

    /// <summary>
    /// Starts a new session with the given number of minutes.
    /// </summary>
    Task<Session> StartSessionAsync(decimal minutesToAdd);

    /// <summary>
    /// Adds time to the current session by extending the expiration timestamp.
    /// </summary>
    Task<Session> AddTimeAsync(decimal minutesToAdd);

    /// <summary>
    /// Pauses the current session timer.
    /// </summary>
    Task<Session> PauseSessionAsync();

    /// <summary>
    /// Resumes a paused session, adjusting the expiration timestamp.
    /// </summary>
    Task<Session> ResumeSessionAsync();

    /// <summary>
    /// Marks the current session as expired.
    /// </summary>
    Task<Session> ExpireSessionAsync();

    /// <summary>
    /// Gets the remaining time for the current session.
    /// </summary>
    Task<TimeSpan> GetRemainingTimeAsync();

    /// <summary>
    /// Locks the computer (changes session state to Locked).
    /// </summary>
    Task<Session> LockSessionAsync();

    /// <summary>
    /// Resets/ends the current session.
    /// </summary>
    Task ResetSessionAsync();
}
