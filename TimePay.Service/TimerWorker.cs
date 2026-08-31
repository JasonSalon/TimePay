using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.Service.Ipc;

namespace TimePay.Service;

/// <summary>
/// Background worker that monitors the TimePay session timer in the Windows Service.
/// Responsible for checking expiration, enforcing lock state, hosting the Named Pipe server,
/// and persisting session changes.
/// </summary>
public class TimerWorker : BackgroundService
{
    private readonly ILogger<TimerWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public TimerWorker(ILogger<TimerWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TimePayService background timer worker starting...");

        using var scope = _serviceProvider.CreateScope();
        var timerEngine = scope.ServiceProvider.GetRequiredService<ITimerEngine>();
        var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
        var pipeServer = scope.ServiceProvider.GetRequiredService<PipeServer>();

        // 1. Initialize core timer engine
        await timerEngine.InitializeAsync();

        // 2. Start Named Pipe IPC Server in background task
        _ = Task.Run(() => pipeServer.StartListeningAsync(stoppingToken), stoppingToken);

        // 3. Log service start
        await auditLogger.LogAsync(
            AuditAction.ServiceStarted,
            "SYSTEM",
            $"TimePay Windows Service started successfully at {DateTimeOffset.UtcNow:u}");

        _logger.LogInformation("TimePayService background timer worker active.");

        // 4. Continuous Timer Tick Loop (1-second precision)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timerEngine.TickAsync();
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TimerWorker execution cycle.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        // 5. Log service stop
        try
        {
            await auditLogger.LogAsync(
                AuditAction.ServiceStopped,
                "SYSTEM",
                $"TimePay Windows Service stopped at {DateTimeOffset.UtcNow:u}");
        }
        catch
        {
            // Ignore during shutdown
        }

        _logger.LogInformation("TimePayService background timer worker stopped.");
    }
}
