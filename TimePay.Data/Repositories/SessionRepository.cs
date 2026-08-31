using Microsoft.EntityFrameworkCore;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;

namespace TimePay.Data.Repositories;

/// <summary>
/// Manages session lifecycle using expiration timestamps.
/// The ExpirationAt field is the source of truth — never a decrementing counter.
/// </summary>
public class SessionRepository : ISessionManager
{
    private readonly TimePayDbContext _context;
    private readonly ISystemClock _clock;

    public SessionRepository(TimePayDbContext context, ISystemClock? clock = null)
    {
        _context = context;
        _clock = clock ?? new SystemClock();
    }

    /// <inheritdoc />
    public async Task<Session?> GetCurrentSessionAsync()
    {
        // Get the most recent session that is active, paused, or locked
        return await _context.Sessions
            .Where(s => s.Status == SessionStatus.Active
                     || s.Status == SessionStatus.Paused
                     || s.Status == SessionStatus.Locked)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<Session> StartSessionAsync(decimal minutesToAdd)
    {
        if (minutesToAdd <= 0)
            throw new ArgumentOutOfRangeException(nameof(minutesToAdd), "Minutes must be greater than 0.");

        var now = _clock.UtcNow;
        var sessionNumber = await _context.Sessions.CountAsync() + 1;

        var session = new Session
        {
            SessionId = $"TP-{now:yyyyMMdd}-{sessionNumber:D5}",
            StartedAt = now,
            ExpirationAt = now.AddMinutes((double)minutesToAdd),
            Status = SessionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        return session;
    }

    /// <inheritdoc />
    public async Task<Session> AddTimeAsync(decimal minutesToAdd)
    {
        if (minutesToAdd <= 0)
            throw new ArgumentOutOfRangeException(nameof(minutesToAdd), "Minutes must be greater than 0.");

        var session = await GetCurrentSessionAsync();
        if (session == null)
            return await StartSessionAsync(minutesToAdd);

        var now = _clock.UtcNow;

        if (session.Status == SessionStatus.Paused && session.PausedAt.HasValue)
        {
            // When paused, extend from paused time perspective
            // The expiration will be adjusted when resumed
            session.ExpirationAt = session.ExpirationAt.AddMinutes((double)minutesToAdd);
        }
        else
        {
            // If session has already expired, start fresh from now
            if (session.ExpirationAt <= now)
            {
                session.ExpirationAt = now.AddMinutes((double)minutesToAdd);
                session.StartedAt = now;
            }
            else
            {
                // Extend the existing expiration
                session.ExpirationAt = session.ExpirationAt.AddMinutes((double)minutesToAdd);
            }
        }

        session.Status = SessionStatus.Active;
        session.UpdatedAt = now;

        await _context.SaveChangesAsync();
        return session;
    }

    /// <inheritdoc />
    public async Task<Session> PauseSessionAsync()
    {
        var session = await GetCurrentSessionAsync()
            ?? throw new InvalidOperationException("No active session to pause.");

        if (session.Status != SessionStatus.Active)
            throw new InvalidOperationException($"Cannot pause session in state: {session.Status}");

        var now = _clock.UtcNow;
        session.PausedAt = now;
        session.Status = SessionStatus.Paused;
        session.UpdatedAt = now;

        await _context.SaveChangesAsync();
        return session;
    }

    /// <inheritdoc />
    public async Task<Session> ResumeSessionAsync()
    {
        var session = await GetCurrentSessionAsync()
            ?? throw new InvalidOperationException("No session to resume.");

        if (session.Status != SessionStatus.Paused || !session.PausedAt.HasValue)
            throw new InvalidOperationException($"Cannot resume session in state: {session.Status}");

        var now = _clock.UtcNow;
        var pausedDuration = now - session.PausedAt.Value;

        // Extend expiration by the paused duration so the user doesn't lose time
        session.ExpirationAt = session.ExpirationAt.Add(pausedDuration);
        session.AccumulatedPauseSeconds += (long)pausedDuration.TotalSeconds;
        session.PausedAt = null;
        session.Status = SessionStatus.Active;
        session.UpdatedAt = now;

        await _context.SaveChangesAsync();
        return session;
    }

    /// <inheritdoc />
    public async Task<Session> ExpireSessionAsync()
    {
        var session = await GetCurrentSessionAsync()
            ?? throw new InvalidOperationException("No active session to expire.");

        session.Status = SessionStatus.Expired;
        session.UpdatedAt = _clock.UtcNow;

        await _context.SaveChangesAsync();
        return session;
    }

    /// <inheritdoc />
    public async Task<TimeSpan> GetRemainingTimeAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session == null)
            return TimeSpan.Zero;

        if (session.Status == SessionStatus.Paused && session.PausedAt.HasValue)
        {
            // When paused, remaining = expiration - paused time
            var remaining = session.ExpirationAt - session.PausedAt.Value;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        var now = _clock.UtcNow;
        var timeLeft = session.ExpirationAt - now;
        return timeLeft > TimeSpan.Zero ? timeLeft : TimeSpan.Zero;
    }

    /// <inheritdoc />
    public async Task<Session> LockSessionAsync()
    {
        var session = await GetCurrentSessionAsync()
            ?? throw new InvalidOperationException("No session to lock.");

        session.Status = SessionStatus.Locked;
        session.UpdatedAt = _clock.UtcNow;

        await _context.SaveChangesAsync();
        return session;
    }

    /// <inheritdoc />
    public async Task ResetSessionAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session == null) return;

        session.Status = SessionStatus.Completed;
        session.UpdatedAt = _clock.UtcNow;

        await _context.SaveChangesAsync();
    }
}
