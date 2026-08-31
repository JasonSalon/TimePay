namespace TimePay.Core.Interfaces;

/// <summary>
/// Abstraction for system time to allow deterministic testing of clock jumps,
/// expiration calculations, and shutdown/recovery simulations.
/// </summary>
public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// Default system clock using real DateTimeOffset.UtcNow.
/// </summary>
public class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
