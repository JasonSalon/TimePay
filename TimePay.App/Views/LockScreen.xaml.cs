using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.App.Services;

namespace TimePay.App.Views;

/// <summary>
/// Lock screen displayed when computer usage time has expired or PC was locked.
/// Covers the desktop, blocks normal user interaction, and provides admin unlock.
/// </summary>
public partial class LockScreen : Page
{
    private int _failedAttempts = 0;

    public LockScreen()
    {
        InitializeComponent();
        Loaded += LockScreen_Loaded;
    }

    private async void LockScreen_Loaded(object sender, RoutedEventArgs e)
    {
        // Enforce full coverage & kiosk lockdown
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.EnterKioskMode();

        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();

            var settings = await settingsService.GetSettingsAsync();
            var currency = await settingsService.GetCurrencyAsync();

            CurrentRateBadge.Text = $"{currency.Symbol}1 = {settings.MinutesPerPeso} minutes";

            var session = await sessionManager.GetCurrentSessionAsync();
            if (session != null && session.Status == SessionStatus.Locked && session.ExpirationAt > DateTimeOffset.UtcNow)
            {
                LockTitleText.Text = "PC MANUALLY LOCKED";
                LockMessageText.Text = "This computer has been locked by the administrator. Remaining time is preserved. Please authenticate to resume.";
            }
            else
            {
                LockTitleText.Text = "COMPUTER TIME EXPIRED";
                LockMessageText.Text = "Your purchased computer time has ended. Please contact the administrator or counter to add more time.";

                if (settings.SoundEnabled)
                {
                    AudioAlertService.PlayExpiredSound();
                }
            }
        }
        catch
        {
            // Fallback default
            CurrentRateBadge.Text = "₱1 = 4 minutes";
        }
    }

    private void ShowAdminLoginBtn_Click(object sender, RoutedEventArgs e)
    {
        ShowAdminLoginBtn.Visibility = Visibility.Collapsed;
        AdminLoginFormPanel.Visibility = Visibility.Visible;
        AdminUsernameBox.Focus();
    }

    private void CloseAdminLoginBtn_Click(object sender, RoutedEventArgs e)
    {
        AdminLoginFormPanel.Visibility = Visibility.Collapsed;
        ShowAdminLoginBtn.Visibility = Visibility.Visible;
        HideError();
    }

    private async void AdminInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await AttemptAdminUnlockAsync();
        }
    }

    private async void AdminUnlockBtn_Click(object sender, RoutedEventArgs e)
    {
        await AttemptAdminUnlockAsync();
    }

    private async Task AttemptAdminUnlockAsync()
    {
        var username = AdminUsernameBox.Text.Trim();
        var password = AdminPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Please enter your admin username.");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter your admin password.");
            return;
        }

        AdminUnlockBtn.IsEnabled = false;
        AdminUnlockBtn.Content = "Authenticating...";
        HideError();

        try
        {
            using var scope = App.Services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            var admin = await authService.ValidateLoginAsync(username, password);

            if (admin != null)
            {
                _failedAttempts = 0;

                await auditLogger.LogAsync(
                    AuditAction.AdminLoginSuccess,
                    admin.Username,
                    "Admin unlocked PC from Lock Screen");

                await auditLogger.LogAsync(
                    AuditAction.PcUnlocked,
                    admin.Username,
                    "PC unlocked");

                // Start admin session
                var adminSession = App.Services.GetRequiredService<AdminSessionService>();
                adminSession.Login(admin.Id, admin.Username);

                // Exit Kiosk Mode for admin session
                var mainWindow = Window.GetWindow(this) as MainWindow;
                mainWindow?.ExitKioskMode();

                // Clear sensitive fields
                AdminPasswordBox.Clear();

                // Navigate to Admin Dashboard
                var nav = App.Services.GetRequiredService<NavigationService>();
                var dashboard = App.Services.GetRequiredService<AdminDashboard>();
                nav.NavigateTo(dashboard);
                nav.ClearHistory();
            }
            else
            {
                _failedAttempts++;

                await auditLogger.LogAsync(
                    AuditAction.AdminLoginFailed,
                    username,
                    $"Failed unlock attempt #{_failedAttempts} on Lock Screen");

                ShowError("Invalid username or password.");
                AdminPasswordBox.Clear();
                AdminPasswordBox.Focus();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Authentication error: {ex.Message}");
        }
        finally
        {
            AdminUnlockBtn.IsEnabled = true;
            AdminUnlockBtn.Content = "AUTHENTICATE & UNLOCK";
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorText.Visibility = Visibility.Collapsed;
    }
}
