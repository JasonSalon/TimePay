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

public class AuditLogDisplayItem
{
    public DateTimeOffset CreatedAt { get; set; }
    public string FormattedDate => CreatedAt.ToLocalTime().ToString("MMM dd, yyyy h:mm:ss tt");
    public AuditAction Action { get; set; }
    public string ActionName => Action.ToString();
    public string Username { get; set; } = "SYSTEM";
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Audit Log viewer displaying security logs, login attempts, rate updates,
/// clock tamper records, and system service events.
/// </summary>
public partial class AuditLogsView : Page
{
    private List<AuditLogDisplayItem> _allLogs = new();

    public AuditLogsView()
    {
        InitializeComponent();
        Loaded += AuditLogsView_Loaded;
    }

    private async void AuditLogsView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadLogsAsync();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            var logs = await auditLogger.GetLogsAsync(maxResults: 500);

            _allLogs = logs.Select(l => new AuditLogDisplayItem
            {
                CreatedAt = l.CreatedAt,
                Action = l.Action,
                Username = l.Username,
                Details = l.Details
            }).ToList();

            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load audit logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allLogs.AsEnumerable();

        // 1. Category Filter
        var categoryIndex = ActionFilterCombo?.SelectedIndex ?? 0;
        switch (categoryIndex)
        {
            case 1: // Security & Logins
                filtered = filtered.Where(l => l.Action == AuditAction.AdminLoginSuccess || l.Action == AuditAction.AdminLoginFailed);
                break;
            case 2: // Rate & Settings
                filtered = filtered.Where(l => l.Action == AuditAction.RateChanged || l.Action == AuditAction.SettingsChanged);
                break;
            case 3: // Time & Sessions
                filtered = filtered.Where(l => l.Action == AuditAction.TimeAdded ||
                                               l.Action == AuditAction.SessionStarted ||
                                               l.Action == AuditAction.SessionExpired ||
                                               l.Action == AuditAction.TimerPaused ||
                                               l.Action == AuditAction.TimerResumed);
                break;
            case 4: // System & Service
                filtered = filtered.Where(l => l.Action == AuditAction.ServiceStarted ||
                                               l.Action == AuditAction.ServiceStopped ||
                                               l.Action == AuditAction.ClockChangeDetected ||
                                               l.Action == AuditAction.PcLocked ||
                                               l.Action == AuditAction.PcUnlocked);
                break;
            case 5: // Clock Tampering
                filtered = filtered.Where(l => l.Action == AuditAction.ClockChangeDetected);
                break;
        }

        // 2. Search Text Filter
        var search = SearchBox?.Text?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(l =>
                l.ActionName.ToLowerInvariant().Contains(search) ||
                l.Username.ToLowerInvariant().Contains(search) ||
                l.Details.ToLowerInvariant().Contains(search));
        }

        AuditGrid.ItemsSource = filtered.ToList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ActionFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ExportCsvBtn_Click(object sender, RoutedEventArgs e)
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionService>();
        adminSession.RecordActivity();

        if (AuditGrid.ItemsSource is not List<AuditLogDisplayItem> list || list.Count == 0)
        {
            MessageBox.Show("No audit logs to export.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"TimePay_AuditLogs_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            DefaultExt = ".csv"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Timestamp,Action,User,Details");

                foreach (var item in list)
                {
                    var cleanDetails = item.Details.Replace("\"", "\"\"");
                    sb.AppendLine($"\"{item.FormattedDate}\",\"{item.ActionName}\",\"{item.Username}\",\"{cleanDetails}\"");
                }

                File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

                MessageBox.Show($"Audit logs exported successfully to:\n{saveDialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
