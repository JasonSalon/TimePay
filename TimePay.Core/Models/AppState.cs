namespace TimePay.Core.Models;

/// <summary>
/// Represents the possible states of the TimePay application.
/// </summary>
public enum AppState
{
    Initializing,
    SetupRequired,
    Locked,
    Active,
    Paused,
    LowTime,
    Expired,
    AdminMode,
    Error
}
