using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.App.Services;

namespace TimePay.App.Views;

/// <summary>
/// System Settings view for managing warning thresholds, sound alerts,
/// auto-start configuration, and administrator password changes.
/// </summary>
public partial class SettingsView : Page
{
    private Settings? _settings;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += SettingsView_Loaded;
    }

    private async void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var startupService = App.Services.GetRequiredService<IStartupService>();

            _settings = await settingsService.GetSettingsAsync();

            SoundEnabledCheck.IsChecked = _settings.SoundEnabled;
            AllowDecimalCheck.IsChecked = _settings.AllowDecimalAmounts;
            PauseOnShutdownCheck.IsChecked = _settings.PauseOnShutdown;
            AutoStartCheck.IsChecked = _settings.AutoStartEnabled || startupService.IsAutoStartEnabled();

            Warn1Box.Text = _settings.WarningMinutes1.ToString();
            Warn2Box.Text = _settings.WarningMinutes2.ToString();
            Warn3Box.Text = _settings.WarningMinutes3.ToString();
        }
        catch (Exception ex)
        {
            ShowMessage($"Failed to load settings: {ex.Message}", isError: true);
        }
    }

    private async void SaveSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionService>();
        adminSession.RecordActivity();

        if (!int.TryParse(Warn1Box.Text, out var warn1) || warn1 <= 0 ||
            !int.TryParse(Warn2Box.Text, out var warn2) || warn2 <= 0 ||
            !int.TryParse(Warn3Box.Text, out var warn3) || warn3 <= 0)
        {
            ShowMessage("Warning thresholds must be valid positive numbers.", isError: true);
            return;
        }

        if (warn1 <= warn2 || warn2 <= warn3)
        {
            ShowMessage("Warning thresholds must be in descending order (e.g. 10 > 5 > 1).", isError: true);
            return;
        }

        SaveSettingsBtn.IsEnabled = false;

        try
        {
            using var scope = App.Services.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            var startupService = App.Services.GetRequiredService<IStartupService>();

            var settings = await settingsService.GetSettingsAsync();
            settings.SoundEnabled = SoundEnabledCheck.IsChecked ?? true;
            settings.AllowDecimalAmounts = AllowDecimalCheck.IsChecked ?? true;
            settings.PauseOnShutdown = PauseOnShutdownCheck.IsChecked ?? false;
            settings.AutoStartEnabled = AutoStartCheck.IsChecked ?? true;

            settings.WarningMinutes1 = warn1;
            settings.WarningMinutes2 = warn2;
            settings.WarningMinutes3 = warn3;

            await settingsService.UpdateSettingsAsync(settings);

            // Update Windows startup registry key
            startupService.SetAutoStart(settings.AutoStartEnabled);

            // Write audit log
            await auditLogger.LogAsync(
                AuditAction.SettingsChanged,
                adminSession.CurrentAdminUsername ?? "admin",
                $"Settings updated. AutoStart: {settings.AutoStartEnabled}, Sound: {settings.SoundEnabled}, Warnings: {warn1}/{warn2}/{warn3}m");

            ShowMessage("Settings saved successfully!", isError: false);
        }
        catch (Exception ex)
        {
            ShowMessage($"Failed to save settings: {ex.Message}", isError: true);
        }
        finally
        {
            SaveSettingsBtn.IsEnabled = true;
        }
    }

    private async void ChangePasswordBtn_Click(object sender, RoutedEventArgs e)
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionService>();
        adminSession.RecordActivity();

        var currentPassword = CurrentPasswordBox.Password;
        var newPassword = NewPasswordBox.Password;
        var confirmPassword = ConfirmNewPasswordBox.Password;

        if (string.IsNullOrEmpty(currentPassword))
        {
            ShowMessage("Please enter your current password.", isError: true);
            return;
        }

        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
        {
            ShowMessage("New password must be at least 6 characters.", isError: true);
            return;
        }

        if (newPassword != confirmPassword)
        {
            ShowMessage("New passwords do not match.", isError: true);
            return;
        }

        ChangePasswordBtn.IsEnabled = false;

        try
        {
            using var scope = App.Services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            var adminId = adminSession.CurrentAdminId ?? 1;
            var success = await authService.ChangePasswordAsync(adminId, currentPassword, newPassword);

            if (success)
            {
                await auditLogger.LogAsync(
                    AuditAction.SettingsChanged,
                    adminSession.CurrentAdminUsername ?? "admin",
                    "Admin password changed successfully");

                CurrentPasswordBox.Clear();
                NewPasswordBox.Clear();
                ConfirmNewPasswordBox.Clear();

                ShowMessage("Password changed successfully!", isError: false);
            }
            else
            {
                ShowMessage("Current password is incorrect.", isError: true);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Password update failed: {ex.Message}", isError: true);
        }
        finally
        {
            ChangePasswordBtn.IsEnabled = true;
        }
    }

    private void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionService>();
        adminSession.RecordActivity();

        var nav = App.Services.GetRequiredService<NavigationService>();
        var dashboard = App.Services.GetRequiredService<AdminDashboard>();
        nav.NavigateTo(dashboard);
    }

    private void ShowMessage(string message, bool isError)
    {
        if (StatusMessageText != null)
        {
            StatusMessageText.Text = message;
            StatusMessageText.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "DangerBrush" : "ActiveBrush");
            StatusMessageText.Visibility = Visibility.Visible;
        }
    }
}
