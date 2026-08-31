namespace TimePay.Core.Interfaces;

/// <summary>
/// Pure time/money calculation logic.
/// </summary>
public interface ITimeCalculator
{
    /// <summary>
    /// Converts a monetary amount to minutes using the given rate.
    /// </summary>
    decimal CalculateMinutes(decimal amount, decimal minutesPerPeso);

    /// <summary>
    /// Converts minutes to a human-readable time string (e.g., "1 hour 20 minutes").
    /// </summary>
    string FormatTime(decimal totalMinutes);

    /// <summary>
    /// Converts a TimeSpan to a display string (HH:MM:SS).
    /// </summary>
    string FormatTimeSpan(TimeSpan timeSpan);
}
