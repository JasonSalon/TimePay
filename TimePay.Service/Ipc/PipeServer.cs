using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using TimePay.Core.Interfaces;
using TimePay.Core.Ipc;
using TimePay.Core.Models;

namespace TimePay.Service.Ipc;

/// <summary>
/// Named Pipe IPC Server running inside the Windows Service.
/// Handles secure communication with the WPF client application.
/// </summary>
public class PipeServer
{
    private readonly ILogger<PipeServer> _logger;
    private readonly ITimerEngine _timerEngine;
    private readonly ISettingsService _settingsService;
    private readonly IAuditLogger _auditLogger;

    public PipeServer(
        ILogger<PipeServer> logger,
        ITimerEngine timerEngine,
        ISettingsService settingsService,
        IAuditLogger auditLogger)
    {
        _logger = logger;
        _timerEngine = timerEngine;
        _settingsService = settingsService;
        _auditLogger = auditLogger;
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Named Pipe IPC Server on pipe: {PipeName}", IpcConstants.PipeName);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Create pipe server stream with full duplex and message transmission
                var pipeServer = new NamedPipeServerStream(
                    IpcConstants.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync(cancellationToken);
                _ = HandleClientConnectionAsync(pipeServer, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting Named Pipe connection.");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task HandleClientConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using (pipe)
        using (var reader = new StreamReader(pipe))
        using (var writer = new StreamWriter(pipe) { AutoFlush = true })
        {
            try
            {
                while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrEmpty(line))
                        break;

                    var request = JsonSerializer.Deserialize<IpcMessage>(line);
                    if (request == null)
                        continue;

                    var response = await ProcessRequestAsync(request);
                    var responseJson = JsonSerializer.Serialize(response);
                    await writer.WriteLineAsync(responseJson.AsMemory(), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Client disconnected or error processing pipe message.");
            }
        }
    }

    private async Task<IpcMessage> ProcessRequestAsync(IpcMessage request)
    {
        try
        {
            switch (request.Type)
            {
                case IpcMessageType.GetStatusRequest:
                    var settings = await _settingsService.GetSettingsAsync();
                    var currency = await _settingsService.GetCurrencyAsync();
                    var session = _timerEngine.CurrentSession;
                    var remaining = _timerEngine.GetRemainingTime();

                    var statusDto = new ServiceStatusDto
                    {
                        IsActiveSession = session != null,
                        SessionId = session?.SessionId,
                        Status = session?.Status ?? SessionStatus.Locked,
                        AppState = _timerEngine.CurrentAppState,
                        RemainingSeconds = remaining.TotalSeconds,
                        ExpirationAt = session?.ExpirationAt,
                        ActiveRateMinutesPerPeso = settings.MinutesPerPeso,
                        CurrencySymbol = currency.Symbol
                    };

                    return new IpcMessage
                    {
                        Type = IpcMessageType.GetStatusResponse,
                        Payload = JsonSerializer.Serialize(statusDto),
                        Success = true
                    };

                case IpcMessageType.AddTimeRequest:
                    var addTimeDto = JsonSerializer.Deserialize<AddTimeRequestDto>(request.Payload ?? "{}");
                    if (addTimeDto != null && addTimeDto.MinutesToAdd > 0)
                    {
                        await _timerEngine.AddTimeAsync(addTimeDto.MinutesToAdd);
                        return new IpcMessage { Type = IpcMessageType.AddTimeResponse, Success = true };
                    }
                    return new IpcMessage { Type = IpcMessageType.AddTimeResponse, Success = false, ErrorMessage = "Invalid minutes." };

                case IpcMessageType.PauseRequest:
                    await _timerEngine.PauseSessionAsync();
                    return new IpcMessage { Type = IpcMessageType.PauseResponse, Success = true };

                case IpcMessageType.ResumeRequest:
                    await _timerEngine.ResumeSessionAsync();
                    return new IpcMessage { Type = IpcMessageType.ResumeResponse, Success = true };

                case IpcMessageType.LockRequest:
                    await _timerEngine.LockSessionAsync();
                    return new IpcMessage { Type = IpcMessageType.LockResponse, Success = true };

                case IpcMessageType.ResetRequest:
                    await _timerEngine.ResetSessionAsync();
                    return new IpcMessage { Type = IpcMessageType.ResetResponse, Success = true };

                case IpcMessageType.Heartbeat:
                    return new IpcMessage { Type = IpcMessageType.Heartbeat, Success = true };

                default:
                    return new IpcMessage { Type = request.Type, Success = false, ErrorMessage = "Unknown request type." };
            }
        }
        catch (Exception ex)
        {
            return new IpcMessage
            {
                Type = request.Type,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
