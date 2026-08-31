using System.ComponentModel;
using System.Windows;

namespace TimePay.App.Views;

/// <summary>
/// Secondary monitor lock overlay to prevent bypass on multi-display systems.
/// </summary>
public partial class SecondaryLockOverlay : Window
{
    public SecondaryLockOverlay()
    {
        InitializeComponent();
        Closing += SecondaryLockOverlay_Closing;
    }

    private void SecondaryLockOverlay_Closing(object? sender, CancelEventArgs e)
    {
        // Allow close if requested programmatically
    }
}
