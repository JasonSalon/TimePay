using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TimePay.App.Views;

/// <summary>
/// Super-subtle, non-intrusive, click-through overlay showing remaining time in the top-right corner.
/// Mouse clicks pass completely through to whatever game/app is underneath.
/// To restore the full TimePay dashboard, users click TimePay in the Windows taskbar.
/// </summary>
public partial class TimerWidgetWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public TimerWidgetWindow()
    {
        InitializeComponent();

        // Position in top-right corner of primary screen with a clean margin
        Left = SystemParameters.WorkArea.Right - Width - 16;
        Top = SystemParameters.WorkArea.Top + 14;

        Loaded += TimerWidgetWindow_Loaded;
    }

    private void TimerWidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply Win32 click-through (transparent to mouse input) and no-activate styles
        var hwnd = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
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
}
