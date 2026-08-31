namespace TimePay.Core.Models;

/// <summary>
/// Represents the status of a TimePay session.
/// </summary>
public enum SessionStatus
{
    Active,
    Paused,
    Expired,
    Locked,
    Completed
}
