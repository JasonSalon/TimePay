using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using TimePay.App.Services;
using TimePay.App.Views;

namespace TimePay.App;

/// <summary>
/// Main application window — acts as a navigation shell and unbypassable Kiosk lock.
/// Protects against minimization, hotkey evasion, and unauthorized closing.
/// </summary>
public partial class MainWindow : Window
{
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE = 0xF020;
    private const int SC_MAXIMIZE = 0xF030;
    private const int SC_RESTORE = 0xF120;
    private const int SC_CLOSE = 0xF060;
    private const int SC_MOVE = 0xF010;
    private const int SC_SIZE = 0xF000;
    private const int SC_KEYMENU = 0xF100;

    private bool _isKioskLocked = false;
    private bool _isExplicitExitAllowed = false;
    private readonly List<SecondaryLockOverlay> _secondaryOverlays = new();

    public bool IsKioskLocked => _isKioskLocked;

    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
        Deactivated += MainWindow_Deactivated;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SYSCOMMAND)
        {
            int command = wParam.ToInt32() & 0xFFF0;
            if (_isKioskLocked)
            {
                if (command == SC_MINIMIZE || command == SC_CLOSE || command == SC_MOVE ||
                    command == SC_SIZE || command == SC_KEYMENU || command == SC_RESTORE)
                {
                    handled = true;
                    return IntPtr.Zero;
                }
            }
            else if (command == SC_CLOSE)
            {
                var adminSession = App.Services?.GetService<AdminSessionService>();
                bool isAdmin = adminSession != null && adminSession.IsLoggedIn;

                if (!isAdmin && !_isExplicitExitAllowed && MainFrame.Content is not SetupWizard)
                {
                    handled = true;
                    // If user is on the User Dashboard during paid time, minimize to HUD instead of closing
                    if (MainFrame.Content is UserDashboard)
                    {
                        WindowState = WindowState.Minimized;
                    }
                    return IntPtr.Zero;
                }
            }
        }
        return IntPtr.Zero;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (_isKioskLocked && WindowState == WindowState.Minimized)
        {
            Dispatcher.BeginInvoke(() =>
            {
                WindowState = WindowState.Maximized;
                Topmost = true;
                Activate();
            });
        }
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        if (_isKioskLocked)
        {
            Dispatcher.BeginInvoke(() =>
            {
                Topmost = true;
                Activate();
                Focus();
            });
        }
    }

    /// <summary>
    /// Enforces full-screen, unbypassable Kiosk lockdown.
    /// Hides window decorations, prevents minimize/move, enables low-level keyboard hook,
    /// and blocks secondary displays.
    /// </summary>
    public void EnterKioskMode()
    {
        _isKioskLocked = true;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        WindowState = WindowState.Maximized;

        Activate();
        Focus();

        // Enable low-level keyboard hook
        var keyboardHook = App.Services?.GetService<KeyboardHookService>();
        keyboardHook?.EnableHook();

        // Cover secondary monitors
        CoverSecondaryMonitors();
    }

    /// <summary>
    /// Exits Kiosk lockdown mode and restores normal window behavior.
    /// </summary>
    public void ExitKioskMode()
    {
        _isKioskLocked = false;

        // Disable low-level keyboard hook
        var keyboardHook = App.Services?.GetService<KeyboardHookService>();
        keyboardHook?.DisableHook();

        // Close secondary overlays
        CloseSecondaryMonitors();

        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = true;
        Topmost = false;
        WindowState = WindowState.Normal;

        Width = 900;
        Height = 600;
        Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - Width) / 2);
        Top = Math.Max(0, (SystemParameters.PrimaryScreenHeight - Height) / 2);
    }

    private void CoverSecondaryMonitors()
    {
        CloseSecondaryMonitors();

        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                // Check if this monitor is not the primary (0,0) monitor
                bool isPrimary = (lprcMonitor.Left == 0 && lprcMonitor.Top == 0);
                if (!isPrimary)
                {
                    var overlay = new SecondaryLockOverlay
                    {
                        Left = lprcMonitor.Left,
                        Top = lprcMonitor.Top,
                        Width = lprcMonitor.Right - lprcMonitor.Left,
                        Height = lprcMonitor.Bottom - lprcMonitor.Top
                    };
                    overlay.Show();
                    _secondaryOverlays.Add(overlay);
                }
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // Ignore monitor enumeration errors
        }
    }

    private void CloseSecondaryMonitors()
    {
        foreach (var overlay in _secondaryOverlays)
        {
            try { overlay.Close(); } catch { }
        }
        _secondaryOverlays.Clear();
    }

    /// <summary>
    /// Navigates to a page within the main frame.
    /// </summary>
    public void NavigateTo(object page)
    {
        MainFrame.Navigate(page);
    }

    /// <summary>
    /// Programmatic exit method called when an authenticated administrator explicitly confirms application shutdown.
    /// </summary>
    public void ForceClose()
    {
        _isExplicitExitAllowed = true;
        ExitKioskMode();
        Close();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // 1. If explicit programmatic exit was approved
        if (_isExplicitExitAllowed)
        {
            ExitKioskMode();
            return;
        }

        // 2. Allow close if SetupWizard is active (initial first-time setup before admin exists)
        if (MainFrame.Content is SetupWizard)
        {
            var result = MessageBox.Show(
                "TimePay initial setup is not yet complete. Are you sure you want to exit setup?",
                "Exit Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _isExplicitExitAllowed = true;
                ExitKioskMode();
                return;
            }
            e.Cancel = true;
            return;
        }

        // 3. If an authenticated Admin clicks [X] on the window
        var adminSession = App.Services?.GetService<AdminSessionService>();
        if (adminSession != null && adminSession.IsLoggedIn)
        {
            var result = MessageBox.Show(
                "Are you sure you want to exit TimePay?\n\nThis will stop time enforcement and PC monitoring.",
                "Confirm Exit TimePay",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _isExplicitExitAllowed = true;
                ExitKioskMode();
                return;
            }
            e.Cancel = true;
            return;
        }

        // 4. If on UserDashboard during active session, minimize to HUD instead of closing
        if (MainFrame.Content is UserDashboard)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            return;
        }

        // 5. In all other scenarios (LockScreen, AdminLogin, unauthenticated states), block close completely!
        e.Cancel = true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
}