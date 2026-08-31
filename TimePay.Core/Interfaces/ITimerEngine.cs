using TimePay.Core.Models;

namespace TimePay.Core.Interfaces;

public class TimerStateEventArgs : EventArgs
{
    public Session? Session { get; set; }
    public TimeSpan RemainingTime { get; set; }
    public SessionStatus Status { get; set; }
    public AppState AppState { get; set; }
    public int? TriggeredWarningMinutes { get; set; }
}

public class ClockTamperEventArgs : EventArgs
{
    public TimeSpan BackwardJump { get; set; }
    public DateTimeOffset LastKnownTime { get; set; }
    public DateTimeOffset DetectedTime { get; set; }
}

/// <summary>
/// Core timer engine that monitors expiration, fires live countdown ticks,
/// triggers warning threshold events, detects clock manipulation, and transitions states.
/// </summary>
public interface ITimerEngine
{
    event EventHandler<TimerStateEventArgs>? TimerTicked;
    event EventHandler<TimerStateEventArgs>? WarningTriggered;
    event EventHandler<TimerStateEventArgs>? SessionExpired;
    event EventHandler<ClockTamperEventArgs>? ClockTamperDetected;

    Session? CurrentSession { get; }
    AppState CurrentAppState { get; }

    Task InitializeAsync();
    Task<TimerStateEventArgs> TickAsync();
    Task<Session> AddTimeAsync(decimal minutesToAdd);
    Task<Session> PauseSessionAsync();
    Task<Session> ResumeSessionAsync();
    Task<Session> LockSessionAsync();
    Task ResetSessionAsync();
    TimeSpan GetRemainingTime();
}
