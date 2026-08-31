using TimePay.Core.Interfaces;

namespace TimePay.Core.TimeCalculation;

/// <summary>
/// Implements the core time-money conversion logic.
/// Minutes Added = Amount × Minutes Per Peso (spec Section 26).
/// </summary>
public class TimeCalculator : ITimeCalculator
{
    /// <inheritdoc />
    public decimal CalculateMinutes(decimal amount, decimal minutesPerPeso)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative.");

        if (minutesPerPeso <= 0)
            throw new ArgumentOutOfRangeException(nameof(minutesPerPeso), "Minutes per peso must be greater than 0.");

        return amount * minutesPerPeso;
    }

    /// <inheritdoc />
    public string FormatTime(decimal totalMinutes)
    {
        if (totalMinutes <= 0)
            return "0 minutes";

        var totalMinutesInt = (int)Math.Floor(totalMinutes);
        var hours = totalMinutesInt / 60;
        var minutes = totalMinutesInt % 60;

        if (hours > 0 && minutes > 0)
            return $"{hours} hour{(hours != 1 ? "s" : "")} {minutes} minute{(minutes != 1 ? "s" : "")}";
        else if (hours > 0)
            return $"{hours} hour{(hours != 1 ? "s" : "")}";
        else
            return $"{minutes} minute{(minutes != 1 ? "s" : "")}";
    }

    /// <inheritdoc />
    public string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalSeconds <= 0)
            return "00:00:00";

        var totalHours = (int)Math.Floor(timeSpan.TotalHours);
        return $"{totalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }
}
