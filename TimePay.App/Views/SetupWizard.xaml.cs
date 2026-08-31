using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.App.Services;

namespace TimePay.App.Views;

/// <summary>
/// First-launch setup wizard.
/// Shown when no admin accounts exist.
/// Creates the initial admin account and configures default settings.
/// </summary>
public partial class SetupWizard : Page
{
    private static readonly string[] CurrencyCodes = { "PHP", "USD", "EUR", "JPY", "SGD", "MYR" };

    public SetupWizard()
    {
        InitializeComponent();
    }

    private async void CompleteSetupBtn_Click(object sender, RoutedEventArgs e)
    {
        // Validate inputs
        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;
        var confirmPassword = ConfirmPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Please enter an admin username.");
            return;
        }

        if (username.Length < 3)
        {
            ShowError("Username must be at least 3 characters.");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter a password.");
            return;
        }

        if (password.Length < 6)
        {
            ShowError("Password must be at least 6 characters.");
            return;
        }

        if (password != confirmPassword)
        {
            ShowError("Passwords do not match.");
            return;
        }

        if (!decimal.TryParse(MinutesPerPesoBox.Text, out var minutesPerPeso) || minutesPerPeso <= 0)
        {
            ShowError("Please enter a valid minutes per peso value greater than 0.");
            return;
        }

        CompleteSetupBtn.IsEnabled = false;
        CompleteSetupBtn.Content = "Setting up...";

        try
        {
            using var scope = App.Services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            // Create admin account
            await authService.CreateAdminAsync(username, password);

            // Update settings
            var settings = await settingsService.GetSettingsAsync();
            settings.CurrencyCode = CurrencyCodes[CurrencyCombo.SelectedIndex];
            settings.MinutesPerPeso = minutesPerPeso;
            await settingsService.UpdateSettingsAsync(settings);

            // Log setup completion
            await auditLogger.LogAsync(
                AuditAction.SettingsChanged,
                username,
                $"Initial setup completed. Currency: {settings.CurrencyCode}, Rate: {minutesPerPeso} min/unit");

            // Navigate to admin login
            var nav = App.Services.GetRequiredService<NavigationService>();
            var adminLogin = App.Services.GetRequiredService<AdminLogin>();
            nav.NavigateTo(adminLogin);
            nav.ClearHistory();
        }
        catch (Exception ex)
        {
            ShowError($"Setup failed: {ex.Message}");
            CompleteSetupBtn.IsEnabled = true;
            CompleteSetupBtn.Content = "COMPLETE SETUP";
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
