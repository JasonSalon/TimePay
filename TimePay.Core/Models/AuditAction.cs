namespace TimePay.Core.Models;

/// <summary>
/// Represents auditable actions in the TimePay system.
/// </summary>
public enum AuditAction
{
    AdminLoginSuccess,
    AdminLoginFailed,
    TimeAdded,
    RateChanged,
    TimerPaused,
    TimerResumed,
    PcLocked,
    PcUnlocked,
    SessionStarted,
    SessionExpired,
    ClockChangeDetected,
    ServiceStarted,
    ServiceStopped,
    SettingsChanged,
    DatabaseBackup
}
