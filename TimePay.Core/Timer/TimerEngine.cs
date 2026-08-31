using TimePay.Core.Interfaces;
using TimePay.Core.Models;

namespace TimePay.Core.Timer;

/// <summary>
/// Core timer engine implementing expiration-timestamp tracking,
/// clock manipulation detection, low-time warning triggers, and session state transitions.
/// </summary>
public class TimerEngine : ITimerEngine
{
    private readonly ISessionManager _sessionManager;
    private readonly ISettingsService _settingsService;
    private readonly IAuditLogger _auditLogger;
    private readonly ISystemClock _clock;

    private DateTimeOffset _lastKnownTime;
    private readonly HashSet<int> _triggeredWarnings = new();
    private Settings? _cachedSettings;

    public event EventHandler<TimerStateEventArgs>? TimerTicked;
    public event EventHandler<TimerStateEventArgs>? WarningTriggered;
    public event EventHandler<TimerStateEventArgs>? SessionExpired;
    public event EventHandler<ClockTamperEventArgs>? ClockTamperDetected;

    public Session? CurrentSession { get; private set; }
    public AppState CurrentAppState { get; private set; } = AppState.Initializing;

    public TimerEngine(
        ISessionManager sessionManager,
        ISettingsService settingsService,
        IAuditLogger auditLogger,
        ISystemClock? clock = null)
    {
        _sessionManager = sessionManager;
        _settingsService = settingsService;
        _auditLogger = auditLogger;
        _clock = clock ?? new SystemClock();
        _lastKnownTime = _clock.UtcNow;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _lastKnownTime = _clock.UtcNow;
        _cachedSettings = await _settingsService.GetSettingsAsync();
        CurrentSession = await _sessionManager.GetCurrentSessionAsync();

        if (CurrentSession == null)
        {
            CurrentAppState = AppState.Locked;
            return;
        }

        // Evaluate initial session state upon startup / restart
        if (CurrentSession.Status == SessionStatus.Active)
        {
            var now = _clock.UtcNow;
            if (CurrentSession.ExpirationAt <= now)
            {
                // Session expired while application was closed or PC was off
                CurrentSession = await _sessionManager.ExpireSessionAsync();
                CurrentAppState = AppState.Expired;
                await _auditLogger.LogAsync(AuditAction.SessionExpired, "SYSTEM",
                    $"Session {CurrentSession.SessionId} expired upon startup.");
            }
            else
            {
                CurrentAppState = AppState.Active;
            }
        }
        else if (CurrentSession.Status == SessionStatus.Paused)
        {
            CurrentAppState = AppState.Paused;
        }
        else if (CurrentSession.Status == SessionStatus.Locked)
        {
            CurrentAppState = AppState.Locked;
        }
        else
        {
            CurrentAppState = AppState.Locked;
        }
    }

    /// <inheritdoc />
    public async Task<TimerStateEventArgs> TickAsync()
    {
        var now = _clock.UtcNow;

        // 1. Clock manipulation detection (spec Section 32 & 65)
        // Detect if system clock jumped backwards by more than 5 seconds
        if (now < _lastKnownTime.AddSeconds(-5))
        {
            var backwardJump = _lastKnownTime - now;

            ClockTamperDetected?.Invoke(this, new ClockTamperEventArgs
            {
                BackwardJump = backwardJump,
                LastKnownTime = _lastKnownTime,
                DetectedTime = now
            });

            await _auditLogger.LogAsync(AuditAction.ClockChangeDetected, "SYSTEM",
                $"Backward clock jump of {backwardJump.TotalSeconds:F0}s detected. Last known: {_lastKnownTime:u}, Now: {now:u}");

            // Defense: If active session exists, adjust expiration backwards by the jump amount
            // so user cannot gain free time by turning back system clock.
            if (CurrentSession != null && CurrentSession.Status == SessionStatus.Active)
            {
                CurrentSession.ExpirationAt = CurrentSession.ExpirationAt.Subtract(backwardJump);
            }
        }

        _lastKnownTime = now;

        // 2. Process Current Session
        if (CurrentSession == null || CurrentSession.Status == SessionStatus.Completed)
        {
            CurrentAppState = AppState.Locked;
            var argsNoSession = new TimerStateEventArgs
            {
                Session = null,
                RemainingTime = TimeSpan.Zero,
                Status = SessionStatus.Locked,
                AppState = AppState.Locked
            };
            TimerTicked?.Invoke(this, argsNoSession);
            return argsNoSession;
        }

        TimeSpan remaining = GetRemainingTime();
        int? newlyTriggeredWarning = null;

        if (CurrentSession.Status == SessionStatus.Active)
        {
            if (remaining <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
                CurrentSession = await _sessionManager.ExpireSessionAsync();
                CurrentAppState = AppState.Expired;

                var expiredArgs = new TimerStateEventArgs
                {
                    Session = CurrentSession,
                    RemainingTime = TimeSpan.Zero,
                    Status = SessionStatus.Expired,
                    AppState = AppState.Expired
                };

                SessionExpired?.Invoke(this, expiredArgs);
                await _auditLogger.LogAsync(AuditAction.SessionExpired, "SYSTEM",
                    $"Session {CurrentSession.SessionId} expired.");

                TimerTicked?.Invoke(this, expiredArgs);
                return expiredArgs;
            }

            // Check Low-Time Warnings (spec Section 16 & 17)
            _cachedSettings ??= await _settingsService.GetSettingsAsync();

            int[] thresholds = {
                _cachedSettings.WarningMinutes1,
                _cachedSettings.WarningMinutes2,
                _cachedSettings.WarningMinutes3
            };

            foreach (var threshold in thresholds.OrderByDescending(t => t))
            {
                if (threshold <= 0) continue;

                var thresholdSpan = TimeSpan.FromMinutes(threshold);
                if (remaining <= thresholdSpan && !_triggeredWarnings.Contains(threshold))
                {
                    _triggeredWarnings.Add(threshold);
                    newlyTriggeredWarning = threshold;
                }
            }

            if (remaining <= TimeSpan.FromMinutes(_cachedSettings.WarningMinutes2))
            {
                CurrentAppState = AppState.LowTime;
            }
            else
            {
                CurrentAppState = AppState.Active;
            }
        }
        else if (CurrentSession.Status == SessionStatus.Paused)
        {
            CurrentAppState = AppState.Paused;
        }
        else if (CurrentSession.Status == SessionStatus.Locked)
        {
            CurrentAppState = AppState.Locked;
        }
        else if (CurrentSession.Status == SessionStatus.Expired)
        {
            CurrentAppState = AppState.Expired;
            remaining = TimeSpan.Zero;
        }

        var eventArgs = new TimerStateEventArgs
        {
            Session = CurrentSession,
            RemainingTime = remaining,
            Status = CurrentSession.Status,
            AppState = CurrentAppState,
            TriggeredWarningMinutes = newlyTriggeredWarning
        };

        if (newlyTriggeredWarning.HasValue)
        {
            WarningTriggered?.Invoke(this, eventArgs);
        }

        TimerTicked?.Invoke(this, eventArgs);
        return eventArgs;
    }

    /// <inheritdoc />
    public async Task<Session> AddTimeAsync(decimal minutesToAdd)
    {
        CurrentSession = await _sessionManager.AddTimeAsync(minutesToAdd);
        CurrentAppState = AppState.Active;

        // Reset triggered warnings that are now above threshold
        var remaining = GetRemainingTime();
        if (_cachedSettings != null)
        {
            if (remaining.TotalMinutes > _cachedSettings.WarningMinutes1)
                _triggeredWarnings.Remove(_cachedSettings.WarningMinutes1);
            if (remaining.TotalMinutes > _cachedSettings.WarningMinutes2)
                _triggeredWarnings.Remove(_cachedSettings.WarningMinutes2);
            if (remaining.TotalMinutes > _cachedSettings.WarningMinutes3)
                _triggeredWarnings.Remove(_cachedSettings.WarningMinutes3);
        }

        return CurrentSession;
    }

    /// <inheritdoc />
    public async Task<Session> PauseSessionAsync()
    {
        CurrentSession = await _sessionManager.PauseSessionAsync();
        CurrentAppState = AppState.Paused;
        return CurrentSession;
    }

    /// <inheritdoc />
    public async Task<Session> ResumeSessionAsync()
    {
        CurrentSession = await _sessionManager.ResumeSessionAsync();
        CurrentAppState = AppState.Active;
        return CurrentSession;
    }

    /// <inheritdoc />
    public async Task<Session> LockSessionAsync()
    {
        CurrentSession = await _sessionManager.LockSessionAsync();
        CurrentAppState = AppState.Locked;
        return CurrentSession;
    }

    /// <inheritdoc />
    public async Task ResetSessionAsync()
    {
        await _sessionManager.ResetSessionAsync();
        CurrentSession = null;
        CurrentAppState = AppState.Locked;
        _triggeredWarnings.Clear();
    }

    /// <inheritdoc />
    public TimeSpan GetRemainingTime()
    {
        if (CurrentSession == null)
            return TimeSpan.Zero;

        if (CurrentSession.Status == SessionStatus.Paused && CurrentSession.PausedAt.HasValue)
        {
            var remaining = CurrentSession.ExpirationAt - CurrentSession.PausedAt.Value;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        var now = _clock.UtcNow;
        var timeLeft = CurrentSession.ExpirationAt - now;
        return timeLeft > TimeSpan.Zero ? timeLeft : TimeSpan.Zero;
    }
}
