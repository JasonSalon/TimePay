using TimePay.Core.Models;

namespace TimePay.Core.Ipc;

public static class IpcConstants
{
    public const string PipeName = "TimePay_Service_Ipc_Pipe";
}

public enum IpcMessageType
{
    GetStatusRequest,
    GetStatusResponse,
    AddTimeRequest,
    AddTimeResponse,
    PauseRequest,
    PauseResponse,
    ResumeRequest,
    ResumeResponse,
    LockRequest,
    LockResponse,
    ResetRequest,
    ResetResponse,
    Heartbeat
}

public class IpcMessage
{
    public IpcMessageType Type { get; set; }
    public string? Payload { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public class ServiceStatusDto
{
    public bool IsActiveSession { get; set; }
    public string? SessionId { get; set; }
    public SessionStatus Status { get; set; }
    public AppState AppState { get; set; }
    public double RemainingSeconds { get; set; }
    public DateTimeOffset? ExpirationAt { get; set; }
    public decimal ActiveRateMinutesPerPeso { get; set; }
    public string CurrencySymbol { get; set; } = "₱";
}

public class AddTimeRequestDto
{
    public decimal MinutesToAdd { get; set; }
    public string AdminUsername { get; set; } = "admin";
}
