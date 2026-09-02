using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.App.Services;

namespace TimePay.App.Views;

/// <summary>
/// Admin login page with secure password handling.
/// Logs both successful and failed login attempts.
/// Never displays or logs the password itself.
/// </summary>
public partial class AdminLogin : Page
{
    private int _failedAttempts = 0;

    public AdminLogin()
    {
        InitializeComponent();
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private async void LoginBtn_Click(object sender, RoutedEventArgs e)
    {
        await AttemptLogin();
    }

    private async void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await AttemptLogin();
        }
    }

    private async Task AttemptLogin()
    {
        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Please enter your username.");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter your password.");
            return;
        }

        LoginBtn.IsEnabled = false;
        LoginBtn.Content = "Authenticating...";
        HideError();

        try
        {
            using var scope = App.Services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            var admin = await authService.ValidateLoginAsync(username, password);

            if (admin != null)
            {
                // Successful login
                _failedAttempts = 0;

                await auditLogger.LogAsync(
                    AuditAction.AdminLoginSuccess,
                    admin.Username,
                    "Admin logged in successfully");

                // Start admin session
                var sessionService = App.Services.GetRequiredService<AdminSessionService>();
                sessionService.Login(admin.Id, admin.Username);

                // Navigate to admin dashboard
                var nav = App.Services.GetRequiredService<NavigationService>();
                var dashboard = App.Services.GetRequiredService<AdminDashboard>();
                nav.NavigateTo(dashboard);
                nav.ClearHistory();

                // Clear sensitive fields
                PasswordBox.Clear();
            }
            else
            {
                // Failed login
                _failedAttempts++;

                await auditLogger.LogAsync(
                    AuditAction.AdminLoginFailed,
                    username,
                    $"Failed login attempt #{_failedAttempts}");

                ShowError("Invalid username or password.");

                if (_failedAttempts >= 3)
                {
                    AttemptsText.Text = $"{_failedAttempts} failed attempt{(_failedAttempts != 1 ? "s" : "")}";
                    AttemptsText.Visibility = Visibility.Visible;
                }

                // Clear password field on failure
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Login error: {ex.Message}");
        }
        finally
        {
            LoginBtn.IsEnabled = true;
            LoginBtn.Content = "LOGIN";
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

    private async void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        using var scope = App.Services.CreateScope();
        var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();
        var nav = App.Services.GetRequiredService<NavigationService>();
        var mainWindow = Window.GetWindow(this) as MainWindow;

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
        nav.ClearHistory();
    }
}
