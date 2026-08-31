using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.App.Services;

namespace TimePay.App.Views;

/// <summary>
/// Time Configuration page allowing administrators to set currency
/// and conversion rate (Minutes per Peso) with real-time preview.
/// </summary>
public partial class TimeConfiguration : Page
{
    private static readonly string[] CurrencyCodes = { "PHP", "USD", "EUR", "JPY", "SGD", "MYR" };
    private static readonly decimal[] PreviewAmounts = { 1m, 5m, 10m, 20m, 50m, 100m };

    private Settings? _currentSettings;
    private bool _isInitialized = false;

    public TimeConfiguration()
    {
        InitializeComponent();
        Loaded += TimeConfiguration_Loaded;
    }

    private async void TimeConfiguration_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            _currentSettings = await settingsService.GetSettingsAsync();

            var index = Array.IndexOf(CurrencyCodes, _currentSettings.CurrencyCode.ToUpperInvariant());
            CurrencyCombo.SelectedIndex = index >= 0 ? index : 0;

            MinutesPerPesoBox.Text = _currentSettings.MinutesPerPeso.ToString("G29");

            var currency = await settingsService.GetCurrencyAsync();
            CurrentRateBadge.Text = $"{currency.Symbol}1 = {_currentSettings.MinutesPerPeso} minutes";

            _isInitialized = true;
            UpdateLivePreview();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load settings: {ex.Message}");
        }
    }

    private void UpdateLivePreview()
    {
        if (!_isInitialized || PreviewItemsPanel == null)
            return;

        PreviewItemsPanel.Children.Clear();

        var selectedIndex = CurrencyCombo.SelectedIndex;
        var currencyCode = (selectedIndex >= 0 && selectedIndex < CurrencyCodes.Length)
            ? CurrencyCodes[selectedIndex]
            : "PHP";
        var currency = Currency.FromCode(currencyCode) ?? Currency.PHP;

        if (!decimal.TryParse(MinutesPerPesoBox.Text, out var minutesPerUnit) || minutesPerUnit <= 0)
        {
            ShowError("Please enter a valid rate greater than 0.");
            return;
        }

        HideError();

        var timeCalculator = App.Services.GetRequiredService<ITimeCalculator>();

        foreach (var amount in PreviewAmounts)
        {
            var calculatedMinutes = timeCalculator.CalculateMinutes(amount, minutesPerUnit);
            var formattedTime = timeCalculator.FormatTime(calculatedMinutes);

            var row = new DockPanel { Margin = new Thickness(6, 7, 6, 7) };

            var amountText = new TextBlock
            {
                Text = $"{currency.Symbol}{amount:N0}",
                FontFamily = (FontFamily)FindResource("MonoFont"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("PrimaryTextBrush"),
                Width = 70
            };
            DockPanel.SetDock(amountText, Dock.Left);

            var equalsText = new TextBlock
            {
                Text = "=",
                FontSize = 13,
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                Margin = new Thickness(0, 0, 12, 0)
            };
            DockPanel.SetDock(equalsText, Dock.Left);

            var timeText = new TextBlock
            {
                Text = formattedTime,
                FontFamily = (FontFamily)FindResource("PrimaryFont"),
                FontSize = 13,
                Foreground = (Brush)FindResource("AccentBrush"),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            row.Children.Add(amountText);
            row.Children.Add(equalsText);
            row.Children.Add(timeText);

            PreviewItemsPanel.Children.Add(row);

            if (amount != PreviewAmounts[^1])
            {
                var sep = new Separator
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    Margin = new Thickness(0, 0, 0, 0)
                };
                PreviewItemsPanel.Children.Add(sep);
            }
        }
    }

    private void MinutesPerPesoBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateLivePreview();
    }

    private void CurrencyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateLivePreview();
    }

    private async void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionService>();
        adminSession.RecordActivity();

        if (!decimal.TryParse(MinutesPerPesoBox.Text, out var newRate) || newRate <= 0)
        {
            ShowError("Please enter a value greater than 0 for minutes per unit.");
            return;
        }

        var selectedIndex = CurrencyCombo.SelectedIndex;
        var selectedCurrencyCode = (selectedIndex >= 0 && selectedIndex < CurrencyCodes.Length)
            ? CurrencyCodes[selectedIndex]
            : "PHP";
        var selectedCurrency = Currency.FromCode(selectedCurrencyCode) ?? Currency.PHP;

        var oldRate = _currentSettings?.MinutesPerPeso ?? 4m;
        var oldCurrency = _currentSettings?.CurrencyCode ?? "PHP";

        // Admin confirmation dialog per spec Section 29
        var confirmMessage = $"CURRENT RATE:\n{_currentSettings?.CurrencyCode} 1 = {oldRate} minutes\n\n" +
                             $"Change to:\n{selectedCurrency.Symbol}1 = {newRate} minutes ({selectedCurrency.Code})?\n\n" +
                             "This will affect future time purchases.\nExisting purchased time will not be changed.";

        var result = MessageBox.Show(confirmMessage, "Confirm Rate Change", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        SaveBtn.IsEnabled = false;
        SaveBtn.Content = "Saving...";

        try
        {
            using var scope = App.Services.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            var settings = await settingsService.GetSettingsAsync();
            settings.CurrencyCode = selectedCurrencyCode;
            settings.MinutesPerPeso = newRate;
            _currentSettings = await settingsService.UpdateSettingsAsync(settings);

            // Audit logging
            await auditLogger.LogAsync(
                AuditAction.RateChanged,
                adminSession.CurrentAdminUsername ?? "admin",
                $"Rate changed from {oldCurrency} 1={oldRate}m to {selectedCurrencyCode} 1={newRate}m");

            CurrentRateBadge.Text = $"{selectedCurrency.Symbol}1 = {newRate} minutes";

            MessageBox.Show("Time configuration saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to save settings: {ex.Message}");
        }
        finally
        {
            SaveBtn.IsEnabled = true;
            SaveBtn.Content = "SAVE SETTINGS";
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

    private void ShowError(string message)
    {
        if (ErrorText != null)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void HideError()
    {
        if (ErrorText != null)
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }
}
