using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TimePay.App.Services;
using TimePay.App.Views;

namespace TimePay.App;

/// <summary>
/// Main application window — acts as a navigation shell.
/// Protects against casual closure while in locked or user modes.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
    }

    /// <summary>
    /// Navigates to a page within the main frame.
    /// </summary>
    public void NavigateTo(object page)
    {
        MainFrame.Navigate(page);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Allow close if Admin is logged in or if in SetupWizard
        if (MainFrame.Content is AdminDashboard || MainFrame.Content is SetupWizard)
        {
            return;
        }

        var adminSession = App.Services?.GetService<AdminSessionService>();
        if (adminSession != null && adminSession.IsLoggedIn)
        {
            return;
        }

        // Otherwise prevent ordinary user / guest from closing TimePay lock screen or timer
        if (MainFrame.Content is LockScreen || MainFrame.Content is UserDashboard)
        {
            e.Cancel = true;
        }
    }
}