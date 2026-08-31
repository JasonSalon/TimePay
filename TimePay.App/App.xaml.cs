using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.Core.TimeCalculation;
using TimePay.Core.Timer;
using TimePay.Data;
using TimePay.App.Services;
using TimePay.App.Views;

namespace TimePay.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"An unexpected error occurred: {args.Exception.Message}\n\n{args.Exception.StackTrace}", "TimePay Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TimePay", "timepay.db");

        // Ensure directory exists
        var dbDir = Path.GetDirectoryName(dbPath)!;
        Directory.CreateDirectory(dbDir);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Database
                services.AddTimePayData(dbPath);

                // Core services
                services.AddSingleton<ISystemClock, SystemClock>();
                services.AddSingleton<ITimeCalculator, TimeCalculator>();
                services.AddSingleton<ITimerEngine, TimerEngine>();

                // App services
                services.AddSingleton<NavigationService>();
                services.AddSingleton<AdminSessionService>();
                services.AddSingleton<IpcClient>();
                services.AddSingleton<IStartupService, WindowsStartupService>();
                services.AddSingleton<KeyboardHookService>();

                // Views (transient so each navigation creates a fresh instance)
                services.AddTransient<MainWindow>();
                services.AddTransient<SetupWizard>();
                services.AddTransient<AdminLogin>();
                services.AddTransient<AdminDashboard>();
                services.AddTransient<UserDashboard>();
                services.AddTransient<LockScreen>();
                services.AddTransient<AddTime>();
                services.AddTransient<TimeConfiguration>();
                services.AddTransient<Transactions>();
                services.AddTransient<AuditLogsView>();
                services.AddTransient<SettingsView>();
            })
            .Build();

        Services = _host.Services;

        // Initialize database
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TimePayDbContext>();
            await DatabaseInitializer.InitializeAsync(db);
        }

        // Create and show main window
        var mainWindow = Services.GetRequiredService<MainWindow>();
        var nav = Services.GetRequiredService<NavigationService>();
        nav.Initialize(mainWindow.MainFrame);

        // Wire up admin session timeout
        var adminSession = Services.GetRequiredService<AdminSessionService>();
        adminSession.SessionTimedOut += async (_, _) =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                using var scope = Services.CreateScope();
                var startupSvc = Services.GetRequiredService<IStartupService>();
                var sessionMgr = scope.ServiceProvider.GetRequiredService<ISessionManager>();
                var remainingTime = await sessionMgr.GetRemainingTimeAsync();
                var currentSession = await sessionMgr.GetCurrentSessionAsync();

                if (!startupSvc.IsRunningAsWindowsAdmin())
                {
                    if (currentSession != null && currentSession.Status == SessionStatus.Active && remainingTime > TimeSpan.Zero)
                    {
                        mainWindow.ExitKioskMode();
                        var userDashboard = Services.GetRequiredService<UserDashboard>();
                        nav.NavigateTo(userDashboard);
                    }
                    else
                    {
                        mainWindow.EnterKioskMode();
                        var lockScreen = Services.GetRequiredService<LockScreen>();
                        nav.NavigateTo(lockScreen);
                    }
                }
                else
                {
                    mainWindow.ExitKioskMode();
                    var loginPage = Services.GetRequiredService<AdminLogin>();
                    nav.NavigateTo(loginPage);
                }
                nav.ClearHistory();
            });
        };

        mainWindow.Show();

        // Determine initial page based on session & roles (spec Section 9 & 61)
        await NavigateToInitialPage(nav, mainWindow);
    }

    private async Task NavigateToInitialPage(NavigationService nav, MainWindow mainWindow)
    {
        using var scope = Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();
        var startupService = Services.GetRequiredService<IStartupService>();

        // Ensure auto-startup for all user logons
        startupService.EnsureAllUserStartup();

        // Automatically enforce role-based security: Disable Task Manager for Guests, Enable for Admins
        startupService.ApplyRolePolicies();

        if (!await authService.AnyAdminExistsAsync())
        {
            // First launch — show setup wizard
            mainWindow.ExitKioskMode();
            var wizard = Services.GetRequiredService<SetupWizard>();
            nav.NavigateTo(wizard);
        }
        else
        {
            bool isWindowsAdmin = startupService.IsRunningAsWindowsAdmin();
            var session = await sessionManager.GetCurrentSessionAsync();
            var remaining = await sessionManager.GetRemainingTimeAsync();

            if (isWindowsAdmin)
            {
                // When logged in as Windows Administrator, open directly to Admin Login (normal windowed mode, no lock screen trap)
                mainWindow.ExitKioskMode();
                var adminLogin = Services.GetRequiredService<AdminLogin>();
                nav.NavigateTo(adminLogin);
            }
            else
            {
                // Standard Guest User
                if (session != null && session.Status == SessionStatus.Active && remaining > TimeSpan.Zero)
                {
                    // Active purchased time available -> User Dashboard with top-right HUD
                    mainWindow.ExitKioskMode();
                    var userDashboard = Services.GetRequiredService<UserDashboard>();
                    nav.NavigateTo(userDashboard);
                }
                else
                {
                    // No time or expired -> Enforce Fullscreen Kiosk Lock Screen
                    mainWindow.EnterKioskMode();
                    var lockScreen = Services.GetRequiredService<LockScreen>();
                    nav.NavigateTo(lockScreen);
                }
            }
        }

        nav.ClearHistory();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
