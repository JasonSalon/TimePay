using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.App.Services;

namespace TimePay.App.Views;

/// <summary>
/// Add Time screen allowing administrators to convert monetary amounts into usage time.
/// Provides live calculation preview, preset buttons, transaction recording, and balance extension.
/// </summary>
public partial class AddTime : Page
{
    private Settings? _settings;
    private Currency _currency = Currency.PHP;
    private TimeSpan _currentRemaining = TimeSpan.Zero;
    private Session? _currentSession;
    private bool _isInitialized = false;

    public AddTime()
    {
        InitializeComponent();
        Loaded += AddTime_Loaded;
    }

    private async void AddTime_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
        AmountInputBox.Focus();
        AmountInputBox.SelectAll();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();
            var timeCalc = App.Services.GetRequiredService<ITimeCalculator>();

            _settings = await settingsService.GetSettingsAsync();
            _currency = await settingsService.GetCurrencyAsync();
            _currentSession = await sessionManager.GetCurrentSessionAsync();
            _currentRemaining = await sessionManager.GetRemainingTimeAsync();

            CurrencySymbolPrefix.Text = _currency.Symbol;
            CurrentBalanceText.Text = timeCalc.FormatTimeSpan(_currentRemaining);
            CurrentRateText.Text = $"{_currency.Symbol}1 = {_settings.MinutesPerPeso} min";

            _isInitialized = true;
            UpdateLiveCalculation();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load configuration: {ex.Message}");
        }
    }

    private void UpdateLiveCalculation()
    {
        if (!_isInitialized || _settings == null)
            return;

        var amountText = AmountInputBox.Text.Trim();
        if (string.IsNullOrEmpty(amountText))
        {
            TimeToAddText.Text = "+ 00:00:00 (0 mins)";
            NewBalanceText.Text = CurrentBalanceText.Text;
            HideError();
            return;
        }

        if (!decimal.TryParse(amountText, out var amount) || amount <= 0)
        {
            ShowError("Please enter a valid positive amount.");
            TimeToAddText.Text = "+ 00:00:00";
            NewBalanceText.Text = CurrentBalanceText.Text;
            return;
        }

        // Validate decimal amounts if setting is disabled
        if (!_settings.AllowDecimalAmounts && amount != Math.Floor(amount))
        {
            ShowError("Decimal amounts are currently disabled in settings.");
            return;
        }

        HideError();

        var timeCalculator = App.Services.GetRequiredService<ITimeCalculator>();
        var minutesToAdd = timeCalculator.CalculateMinutes(amount, _settings.MinutesPerPeso);
        var timeSpanToAdd = TimeSpan.FromMinutes((double)minutesToAdd);
        var projectedNewBalance = _currentRemaining + timeSpanToAdd;

        TimeToAddText.Text = $"+ {timeCalculator.FormatTimeSpan(timeSpanToAdd)} ({timeCalculator.FormatTime(minutesToAdd)})";
        NewBalanceText.Text = timeCalculator.FormatTimeSpan(projectedNewBalance);
    }

    private void AmountInputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateLiveCalculation();
    }

    private async void AmountInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ProcessAddTimeAsync();
        }
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && decimal.TryParse(tagStr, out var presetVal))
        {
            AmountInputBox.Text = presetVal.ToString("G29");
            AmountInputBox.Focus();
            AmountInputBox.SelectAll();
        }
    }

    private async void ConfirmAddTimeBtn_Click(object sender, RoutedEventArgs e)
    {
        await ProcessAddTimeAsync();
    }

    private async Task ProcessAddTimeAsync()
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionService>();
        adminSession.RecordActivity();

        if (_settings == null)
            return;

        var amountText = AmountInputBox.Text.Trim();
        if (!decimal.TryParse(amountText, out var amount) || amount <= 0)
        {
            ShowError("Please enter a valid amount greater than 0.");
            return;
        }

        if (!_settings.AllowDecimalAmounts && amount != Math.Floor(amount))
        {
            ShowError("Decimal amounts are not allowed.");
            return;
        }

        ConfirmAddTimeBtn.IsEnabled = false;
        ConfirmAddTimeBtn.Content = "Adding Time...";

        try
        {
            using var scope = App.Services.CreateScope();
            var timerEngine = App.Services.GetRequiredService<ITimerEngine>();
            var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();
            var txnService = scope.ServiceProvider.GetRequiredService<ITransactionService>();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            var timeCalc = App.Services.GetRequiredService<ITimeCalculator>();

            var previousSession = await sessionManager.GetCurrentSessionAsync();
            var previousExpiration = previousSession?.ExpirationAt ?? DateTimeOffset.UtcNow;
            var minutesToAdd = timeCalc.CalculateMinutes(amount, _settings.MinutesPerPeso);

            // 1. Add time to session via TimerEngine (or SessionManager)
            var updatedSession = await timerEngine.AddTimeAsync(minutesToAdd);

            // 2. Create immutable Transaction record with rate versioning (spec Section 30)
            var txn = new Transaction
            {
                SessionId = updatedSession.Id,
                AdminUserId = adminSession.CurrentAdminId ?? 1,
                Amount = amount,
                MinutesPerPeso = _settings.MinutesPerPeso,
                MinutesAdded = minutesToAdd,
                PreviousExpiration = previousExpiration,
                NewExpiration = updatedSession.ExpirationAt
            };
            await txnService.CreateTransactionAsync(txn);

            // 3. Write Audit Log entry
            var adminUser = adminSession.CurrentAdminUsername ?? "admin";
            var formattedTimeAdded = timeCalc.FormatTime(minutesToAdd);
            var remainingTimeSpan = timerEngine.GetRemainingTime();

            await auditLogger.LogAsync(
                AuditAction.TimeAdded,
                adminUser,
                $"Added {_currency.Symbol}{amount:N2} ({formattedTimeAdded}) at rate {_currency.Symbol}1={_settings.MinutesPerPeso}m. New balance: {timeCalc.FormatTimeSpan(remainingTimeSpan)}");

            MessageBox.Show(
                $"Successfully added {formattedTimeAdded}!\n\nNew Balance: {timeCalc.FormatTimeSpan(remainingTimeSpan)}",
                "Time Added",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Navigate back to Admin Dashboard
            var nav = App.Services.GetRequiredService<NavigationService>();
            var dashboard = App.Services.GetRequiredService<AdminDashboard>();
            nav.NavigateTo(dashboard);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to add time: {ex.Message}");
        }
        finally
        {
            ConfirmAddTimeBtn.IsEnabled = true;
            ConfirmAddTimeBtn.Content = "ADD TIME";
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
