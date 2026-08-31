using System.IO;
using TimePay.Core.Interfaces;
using TimePay.Core.TimeCalculation;
using TimePay.Core.Timer;
using TimePay.Data;
using TimePay.Service;
using TimePay.Service.Ipc;

var builder = Host.CreateApplicationBuilder(args);

// Configure as Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "TimePayService";
});

// Shared Database path
var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "TimePay", "timepay.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddTimePayData(dbPath);

// Core Services
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<ITimeCalculator, TimeCalculator>();
builder.Services.AddScoped<ITimerEngine, TimerEngine>();
builder.Services.AddScoped<PipeServer>();

// Background Worker
builder.Services.AddHostedService<TimerWorker>();

var host = builder.Build();

// Initialize database
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TimePayDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

host.Run();
