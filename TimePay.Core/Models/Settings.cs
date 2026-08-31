namespace TimePay.Core.Models;

/// <summary>
/// Application settings stored in the database.
/// </summary>
public class Settings
{
    public int Id { get; set; }

    /// <summary>
    /// Currency code (e.g., "PHP").
    /// </summary>
    public string CurrencyCode { get; set; } = "PHP";

    /// <summary>
    /// How many minutes one unit of currency buys.
    /// Must be greater than 0.
    /// </summary>
    public decimal MinutesPerPeso { get; set; } = 4m;

    /// <summary>
    /// First warning threshold in minutes.
    /// </summary>
    public int WarningMinutes1 { get; set; } = 10;

    /// <summary>
    /// Second warning threshold in minutes.
    /// </summary>
    public int WarningMinutes2 { get; set; } = 5;

    /// <summary>
    /// Third warning threshold in minutes.
    /// </summary>
    public int WarningMinutes3 { get; set; } = 1;

    /// <summary>
    /// Whether low-time warning sounds are enabled.
    /// </summary>
    public bool SoundEnabled { get; set; } = true;

    /// <summary>
    /// Whether TimePay should start automatically with Windows.
    /// </summary>
    public bool AutoStartEnabled { get; set; } = true;

    /// <summary>
    /// If true, the timer pauses when the computer is shut down
    /// instead of continuing to count down. Disabled by default.
    /// </summary>
    public bool PauseOnShutdown { get; set; } = false;

    /// <summary>
    /// Whether decimal amounts are allowed (e.g., ₱2.50).
    /// </summary>
    public bool AllowDecimalAmounts { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
