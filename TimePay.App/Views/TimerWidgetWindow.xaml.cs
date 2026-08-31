using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TimePay.App.Views;

/// <summary>
/// Always-on-top draggable mini widget showing live remaining time.
/// </summary>
public partial class TimerWidgetWindow : Window
{
    private readonly Action _onRestore;

    public TimerWidgetWindow(Action onRestore)
    {
        InitializeComponent();
        _onRestore = onRestore;

        // Position in top-right corner by default
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 24;
    }

    public void UpdateDisplay(string timeFormatted, bool isLowTime, bool isPaused)
    {
        WidgetTimeText.Text = timeFormatted;

        if (isPaused)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xf5, 0xa6, 0x23)); // Yellow
            WidgetTimeText.Foreground = new SolidColorBrush(Color.FromRgb(0xf5, 0xa6, 0x23));
        }
        else if (isLowTime)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xff, 0x47, 0x57)); // Red
            WidgetTimeText.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x47, 0x57));
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xd9, 0x7e)); // Green
            WidgetTimeText.Foreground = new SolidColorBrush(Colors.White);
        }
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void ExpandBtn_Click(object sender, RoutedEventArgs e)
    {
        _onRestore?.Invoke();
        Close();
    }
}
