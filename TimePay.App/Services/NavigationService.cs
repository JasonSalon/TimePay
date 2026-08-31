using System.Windows.Controls;

namespace TimePay.App.Services;

/// <summary>
/// Navigation service for switching pages within the MainWindow frame.
/// </summary>
public class NavigationService
{
    private Frame? _frame;

    public void Initialize(Frame frame)
    {
        _frame = frame;
    }

    public void NavigateTo(Page page)
    {
        _frame?.Navigate(page);
    }

    /// <summary>
    /// Clear navigation history to prevent back-navigation.
    /// </summary>
    public void ClearHistory()
    {
        if (_frame == null) return;

        while (_frame.CanGoBack)
        {
            _frame.RemoveBackEntry();
        }
    }
}
