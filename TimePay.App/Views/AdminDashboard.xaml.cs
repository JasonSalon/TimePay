using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.App.Services;

namespace TimePay.App.Views;

/// <summary>
/// Admin dashboard — the main control panel after admin login.
/// Updates the timer display every second and shows session info.
/// </summary>
public partial class AdminDashboard : Page
{
    private readonly DispatcherTimer _uiTimer;

    public AdminDashboard()
    {
        InitializeComponent();

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += UiTimer_Tick;

        Loaded += AdminDashboard_Loaded;
        Unloaded += (_, _) => _uiTimer.Stop();
    }

    private async void AdminDashboard_Loaded(object sender, RoutedEventArgs e)
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionService>();
        WelcomeText.Text = $"Welcome, {adminSession.CurrentAdminUsername}";
        adminSession.RecordActivity();

        _uiTimer.Start();
        await RefreshDisplay();
    }

    private async void UiTimer_Tick(object? sender, EventArgs e)
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionService>();
        var remaining = adminSession.GetRemainingSessionTime();
        SessionTimeoutText.Text = $"Admin session: {remaining.Minutes}m {remaining.Seconds}s remaining";

        await RefreshDisplay();
    }

    private async Task RefreshDisplay()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var timeCalculator = App.Services.GetRequiredService<ITimeCalculator>();

            var session = await sessionManager.GetCurrentSessionAsync();
            var settings = await settingsService.GetSettingsAsync();
            var currency = await settingsService.GetCurrencyAsync();

            RateDisplay.Text = $"Rate: {currency.Symbol}1 = {settings.MinutesPerPeso} min";

            if (session == null)
            {
                TimerDisplay.Text = "00:00:00";
                StatusIndicator.Text = "● NO SESSION";
                StatusIndicator.Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush");
                ExpiresDisplay.Text = "";
                PauseBtn.Content = "⏸  PAUSE TIMER";
                PauseBtn.IsEnabled = false;
                return;
            }

            PauseBtn.IsEnabled = true;

            var remaining = await sessionManager.GetRemainingTimeAsync();
            TimerDisplay.Text = timeCalculator.FormatTimeSpan(remaining);

            switch (session.Status)
            {
                case SessionStatus.Active:
                    StatusIndicator.Text = "● ACTIVE";
                    StatusIndicator.Foreground = (System.Windows.Media.Brush)FindResource("ActiveBrush");
                    ExpiresDisplay.Text = $"Expires at {session.ExpirationAt.ToLocalTime():h:mm tt}";
                    PauseBtn.Content = "⏸  PAUSE TIMER";

                    // Color-code timer based on remaining time
                    if (remaining.TotalMinutes <= 1)
                        TimerDisplay.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                    else if (remaining.TotalMinutes <= 5)
                        TimerDisplay.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
                    else
                        TimerDisplay.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush");
                    break;

                case SessionStatus.Paused:
                    StatusIndicator.Text = "● PAUSED";
                    StatusIndicator.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
                    ExpiresDisplay.Text = "Timer is paused";
                    PauseBtn.Content = "▶  RESUME TIMER";
                    TimerDisplay.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
                    break;

                case SessionStatus.Locked:
                    StatusIndicator.Text = "● LOCKED";
                    StatusIndicator.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                    ExpiresDisplay.Text = "Computer is locked";
                    TimerDisplay.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                    break;

                case SessionStatus.Expired:
                    StatusIndicator.Text = "● EXPIRED";
                    StatusIndicator.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                    TimerDisplay.Text = "00:00:00";
                    ExpiresDisplay.Text = "Time has expired";
                    TimerDisplay.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                    break;
            }
        }
        catch (Exception ex)
        {
            ExpiresDisplay.Text = $"Error: {ex.Message}";
        }
    }

    private void AddTimeBtn_Click(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        var nav = App.Services.GetRequiredService<NavigationService>();
        var page = App.Services.GetRequiredService<AddTime>();
        nav.NavigateTo(page);
    }

    private void TimeConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        var nav = App.Services.GetRequiredService<NavigationService>();
        var page = App.Services.GetRequiredService<TimeConfiguration>();
        nav.NavigateTo(page);
    }

    private async void PauseBtn_Click(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        try
        {
            using var scope = App.Services.CreateScope();
            var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            var adminSession = App.Services.GetRequiredService<AdminSessionService>();

            var session = await sessionManager.GetCurrentSessionAsync();
            if (session == null) return;

            if (session.Status == SessionStatus.Paused)
            {
                await sessionManager.ResumeSessionAsync();
                await auditLogger.LogAsync(AuditAction.TimerResumed,
                    adminSession.CurrentAdminUsername ?? "admin", "Timer resumed");
            }
            else if (session.Status == SessionStatus.Active)
            {
                await sessionManager.PauseSessionAsync();
                await auditLogger.LogAsync(AuditAction.TimerPaused,
                    adminSession.CurrentAdminUsername ?? "admin", "Timer paused");
            }

            await RefreshDisplay();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void LockBtn_Click(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        try
        {
            using var scope = App.Services.CreateScope();
            var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();
            var session = await sessionManager.GetCurrentSessionAsync();

            var remaining = session != null ? await sessionManager.GetRemainingTimeAsync() : TimeSpan.Zero;
            var timeCalc = App.Services.GetRequiredService<ITimeCalculator>();

            var result = MessageBox.Show(
                $"Are you sure you want to lock this PC?\n\nCurrent remaining time: {timeCalc.FormatTimeSpan(remaining)}\n\nNote: Remaining time will be preserved.",
                "Lock Computer",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (session != null)
                {
                    await sessionManager.LockSessionAsync();
                }

                var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
                var adminSession = App.Services.GetRequiredService<AdminSessionService>();
                await auditLogger.LogAsync(AuditAction.PcLocked,
                    adminSession.CurrentAdminUsername ?? "admin", "PC manually locked");

                await RefreshDisplay();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        RecordActivity();

        try
        {
            using var scope = App.Services.CreateScope();
            var timerEngine = App.Services.GetRequiredService<ITimerEngine>();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            var timeCalc = App.Services.GetRequiredService<ITimeCalculator>();
            var adminSession = App.Services.GetRequiredService<AdminSessionService>();

            var remaining = timerEngine.GetRemainingTime();

            var result = MessageBox.Show(
                $"Are you sure you want to RESET the timer and END the active session?\n\nRemaining time: {timeCalc.FormatTimeSpan(remaining)}\n\nThis will clear all remaining balance to 00:00:00.\nThis action cannot be undone.",
                "Confirm Reset Time",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await timerEngine.ResetSessionAsync();

                await auditLogger.LogAsync(
                    AuditAction.SessionExpired,
                    adminSession.CurrentAdminUsername ?? "admin",
                    $"Session reset and cleared by administrator (was: {timeCalc.FormatTimeSpan(remaining)})");

                await RefreshDisplay();

                MessageBox.Show("Computer time has been reset to 00:00:00.", "Time Reset", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to reset time: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TransactionsBtn_Click(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        var nav = App.Services.GetRequiredService<NavigationService>();
        var page = App.Services.GetRequiredService<Transactions>();
        nav.NavigateTo(page);
    }

    private void AuditLogsBtn_Click(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        var nav = App.Services.GetRequiredService<NavigationService>();
        var page = App.Services.GetRequiredService<AuditLogsView>();
        nav.NavigateTo(page);
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        var nav = App.Services.GetRequiredService<NavigationService>();
        var page = App.Services.GetRequiredService<SettingsView>();
        nav.NavigateTo(page);
    }

    private async void LogoutBtn_Click(object sender, RoutedEventArgs e)
    {
        using var scope = App.Services.CreateScope();
        var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
        var adminSession = App.Services.GetRequiredService<AdminSessionService>();
        var startupService = App.Services.GetRequiredService<IStartupService>();
        var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();

        await auditLogger.LogAsync(AuditAction.AdminLoginSuccess,
            adminSession.CurrentAdminUsername ?? "admin", "Admin logged out");

        adminSession.Logout();

        _uiTimer.Stop();

        var nav = App.Services.GetRequiredService<NavigationService>();
        var mainWindow = Window.GetWindow(this) as MainWindow;

        if (!startupService.IsRunningAsWindowsAdmin())
        {
            var session = await sessionManager.GetCurrentSessionAsync();
            var remaining = await sessionManager.GetRemainingTimeAsync();

            if (session != null && session.Status == SessionStatus.Active && remaining > TimeSpan.Zero)
            {
                mainWindow?.ExitKioskMode();
                var userDashboard = App.Services.GetRequiredService<UserDashboard>();
                nav.NavigateTo(userDashboard);
            }
            else
            {
                mainWindow?.EnterKioskMode();
                var lockScreen = App.Services.GetRequiredService<LockScreen>();
                nav.NavigateTo(lockScreen);
            }
        }
        else
        {
            mainWindow?.ExitKioskMode();
            var login = App.Services.GetRequiredService<AdminLogin>();
            nav.NavigateTo(login);
        }
        nav.ClearHistory();
    }

    private async void ExitAppBtn_Click(object sender, RoutedEventArgs e)
    {
        RecordActivity();

        var result = MessageBox.Show(
            "Are you sure you want to completely exit TimePay?\n\nThis will terminate time enforcement, disable the lock screen, and close the application.",
            "Confirm Exit TimePay",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                using var scope = App.Services.CreateScope();
                var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
                var adminSession = App.Services.GetRequiredService<AdminSessionService>();
                await auditLogger.LogAsync(
                    AuditAction.ServiceStopped,
                    adminSession.CurrentAdminUsername ?? "Admin",
                    "Admin confirmed exit of TimePay application");
            }
            catch { }

            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.ForceClose();
        }
    }

    private void RecordActivity()
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionService>();
        adminSession.RecordActivity();
    }
}
