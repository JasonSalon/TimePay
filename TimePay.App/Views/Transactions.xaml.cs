using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.App.Services;

namespace TimePay.App.Views;

public class TransactionDisplayItem
{
    public string TransactionId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string FormattedDate => CreatedAt.ToLocalTime().ToString("MMM dd, yyyy h:mm tt");
    public decimal Amount { get; set; }
    public string FormattedAmount { get; set; } = string.Empty;
    public decimal MinutesPerPeso { get; set; }
    public string FormattedRate => $"{MinutesPerPeso} min/unit";
    public decimal MinutesAdded { get; set; }
    public string FormattedTimeAdded { get; set; } = string.Empty;
    public DateTimeOffset NewExpiration { get; set; }
    public string FormattedNewExpiration => NewExpiration.ToLocalTime().ToString("MMM dd, yyyy h:mm tt");
    public string AdminUsername { get; set; } = "admin";
}

/// <summary>
/// Transactions view displaying immutable history of computer time purchases,
/// revenue statistics, date filters, search, and CSV export.
/// </summary>
public partial class Transactions : Page
{
    private List<TransactionDisplayItem> _allTransactions = new();
    private Currency _currency = Currency.PHP;

    public Transactions()
    {
        InitializeComponent();
        Loaded += Transactions_Loaded;
    }

    private async void Transactions_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadTransactionsAsync();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await LoadTransactionsAsync();
    }

    private async Task LoadTransactionsAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var txnService = scope.ServiceProvider.GetRequiredService<ITransactionService>();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var timeCalc = App.Services.GetRequiredService<ITimeCalculator>();

            _currency = await settingsService.GetCurrencyAsync();
            var rawList = await txnService.GetTransactionsAsync(maxResults: 500);

            _allTransactions = rawList.Select(t => new TransactionDisplayItem
            {
                TransactionId = t.TransactionId,
                CreatedAt = t.CreatedAt,
                Amount = t.Amount,
                FormattedAmount = $"{_currency.Symbol}{t.Amount:N2}",
                MinutesPerPeso = t.MinutesPerPeso,
                MinutesAdded = t.MinutesAdded,
                FormattedTimeAdded = timeCalc.FormatTime(t.MinutesAdded),
                NewExpiration = t.NewExpiration,
                AdminUsername = t.AdminUser?.Username ?? "admin"
            }).ToList();

            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load transactions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allTransactions.AsEnumerable();

        // 1. Date Filter
        var now = DateTimeOffset.UtcNow;
        var selectedDateIndex = DateFilterCombo?.SelectedIndex ?? 0;
        switch (selectedDateIndex)
        {
            case 1: // Today
                var startOfToday = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
                filtered = filtered.Where(t => t.CreatedAt >= startOfToday);
                break;
            case 2: // Last 7 Days
                var sevenDaysAgo = now.AddDays(-7);
                filtered = filtered.Where(t => t.CreatedAt >= sevenDaysAgo);
                break;
            case 3: // This Month
                var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
                filtered = filtered.Where(t => t.CreatedAt >= startOfMonth);
                break;
        }

        // 2. Search Box Filter
        var search = SearchBox?.Text?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(t =>
                t.TransactionId.ToLowerInvariant().Contains(search) ||
                t.AdminUsername.ToLowerInvariant().Contains(search) ||
                t.FormattedAmount.ToLowerInvariant().Contains(search));
        }

        var resultList = filtered.ToList();
        TransactionsGrid.ItemsSource = resultList;

        // 3. Update KPI Summary Cards
        var timeCalc = App.Services.GetRequiredService<ITimeCalculator>();
        var totalRevenue = resultList.Sum(t => t.Amount);
        var totalMinutes = resultList.Sum(t => t.MinutesAdded);

        TotalRevenueText.Text = $"{_currency.Symbol}{totalRevenue:N2}";
        TotalTimeSoldText.Text = timeCalc.FormatTime(totalMinutes);
        TotalTransactionsCountText.Text = $"{resultList.Count:N0}";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void DateFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ExportCsvBtn_Click(object sender, RoutedEventArgs e)
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionService>();
        adminSession.RecordActivity();

        if (TransactionsGrid.ItemsSource is not List<TransactionDisplayItem> list || list.Count == 0)
        {
            MessageBox.Show("No transactions to export.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"TimePay_Transactions_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            DefaultExt = ".csv"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Transaction ID,Date & Time,Amount,Currency,Rate (Min/Unit),Minutes Added,New Expiration,Admin User");

                foreach (var item in list)
                {
                    sb.AppendLine($"\"{item.TransactionId}\",\"{item.FormattedDate}\",{item.Amount},\"{_currency.Code}\",{item.MinutesPerPeso},{item.MinutesAdded},\"{item.FormattedNewExpiration}\",\"{item.AdminUsername}\"");
                }

                File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

                MessageBox.Show($"Transactions exported successfully to:\n{saveDialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export CSV: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
}
