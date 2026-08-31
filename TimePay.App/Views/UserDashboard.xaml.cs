using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using TimePay.Core.Interfaces;
using TimePay.Core.Models;
using TimePay.App.Services;

namespace TimePay.App.Views;

/// <summary>
/// Standard user dashboard showing countdown timer, active rate, expiration time,
/// low-time warning visual banners, and mini-widget mode.
/// </summary>
public partial class UserDashboard : Page
{
    private readonly DispatcherTimer _uiTimer;
    private TimerWidgetWindow? _miniWidgetWindow;
    private Settings? _settings;
    private Currency _currency = Currency.PHP;
    private bool _triggeredFiveSecondsWarning = false;

    public UserDashboard()
    {
        InitializeComponent();

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += UiTimer_Tick;

        Loaded += UserDashboard_Loaded;
        Unloaded += UserDashboard_Unloaded;
    }

    private async void UserDashboard_Loaded(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.ExitKioskMode();

        await InitializeDashboardAsync();
        _uiTimer.Start();
    }

    private void UserDashboard_Unloaded(object sender, RoutedEventArgs e)
    {
        _uiTimer.Stop();
        CloseMiniWidget();
    }

    private async Task InitializeDashboardAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var timerEngine = App.Services.GetRequiredService<ITimerEngine>();

            _settings = await settingsService.GetSettingsAsync();
            _currency = await settingsService.GetCurrencyAsync();

            CurrentRateText.Text = $"{_currency.Symbol}1 = {_settings.MinutesPerPeso} min";

            await timerEngine.InitializeAsync();
            await RefreshTimerDisplayAsync();
        }
        catch (Exception ex)
        {
            TimerDisplay.Text = "Error";
            WarningBannerText.Text = ex.Message;
            WarningBanner.Visibility = Visibility.Visible;
        }
    }

    private async void UiTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshTimerDisplayAsync();
    }

    private async Task RefreshTimerDisplayAsync()
    {
        var timerEngine = App.Services.GetRequiredService<ITimerEngine>();
        var timeCalc = App.Services.GetRequiredService<ITimeCalculator>();

        var tickResult = await timerEngine.TickAsync();
        var session = tickResult.Session;
        var remaining = tickResult.RemainingTime;

        // If session expired or locked, enforce Kiosk and navigate to Lock Screen
        if (tickResult.AppState == AppState.Expired || tickResult.AppState == AppState.Locked || session == null)
        {
            _uiTimer.Stop();
            CloseMiniWidget();

            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.EnterKioskMode();

            var nav = App.Services.GetRequiredService<NavigationService>();
            var lockScreen = App.Services.GetRequiredService<LockScreen>();
            nav.NavigateTo(lockScreen);
            nav.ClearHistory();
            return;
        }

        SessionIdText.Text = $"Session: {session.SessionId}";
        TimerDisplay.Text = timeCalc.FormatTimeSpan(remaining);
        ExpiresAtText.Text = session.ExpirationAt.ToLocalTime().ToString("h:mm tt");

        bool isLowTime = false;
        bool isPaused = session.Status == SessionStatus.Paused;

        // Reset 5s warning trigger if time was added
        if (remaining.TotalSeconds > 10)
        {
            _triggeredFiveSecondsWarning = false;
        }

        // Status & Color Coding (spec Section 38)
        if (isPaused)
        {
            StatusText.Text = "● PAUSED";
            StatusText.Foreground = (Brush)FindResource("WarningBrush");
            StatusBadge.Background = new SolidColorBrush(Color.FromArgb(50, 245, 166, 35));
            TimerDisplay.Foreground = (Brush)FindResource("WarningBrush");
            WarningBannerText.Text = "TIMER PAUSED by administrator.";
            WarningBanner.Visibility = Visibility.Visible;
        }
        else if (remaining.TotalSeconds <= 5.0 && remaining.TotalSeconds > 0)
        {
            isLowTime = true;
            StatusText.Text = "● LOCKING";
            StatusText.Foreground = (Brush)FindResource("DangerBrush");
            StatusBadge.Background = new SolidColorBrush(Color.FromArgb(50, 255, 71, 87));
            TimerDisplay.Foreground = (Brush)FindResource("DangerBrush");
            WarningBannerText.Text = "🚨 5 SECONDS REMAINING: Computer is locking now!";
            WarningBanner.Visibility = Visibility.Visible;

            if (!_triggeredFiveSecondsWarning)
            {
                _triggeredFiveSecondsWarning = true;
                if (_settings?.SoundEnabled ?? true)
                {
                    AudioAlertService.PlayFiveSecondsCountdownSound();
                }
            }
        }
        else if (remaining.TotalMinutes <= 1)
        {
            isLowTime = true;
            StatusText.Text = "● CRITICAL";
            StatusText.Foreground = (Brush)FindResource("DangerBrush");
            StatusBadge.Background = new SolidColorBrush(Color.FromArgb(50, 255, 71, 87));
            TimerDisplay.Foreground = (Brush)FindResource("DangerBrush");
            WarningBannerText.Text = "🔴 ONE MINUTE REMAINING: Your computer will lock soon!";
            WarningBanner.Visibility = Visibility.Visible;
        }
        else if (remaining.TotalMinutes <= 5)
        {
            isLowTime = true;
            StatusText.Text = "● LOW TIME";
            StatusText.Foreground = (Brush)FindResource("WarningBrush");
            StatusBadge.Background = new SolidColorBrush(Color.FromArgb(50, 245, 166, 35));
            TimerDisplay.Foreground = (Brush)FindResource("WarningBrush");
            WarningBannerText.Text = "⚠️ LOW TIME: You have less than 5 minutes remaining. Please save your work!";
            WarningBanner.Visibility = Visibility.Visible;
        }
        else if (remaining.TotalMinutes <= 10)
        {
            StatusText.Text = "● ACTIVE";
            StatusText.Foreground = (Brush)FindResource("ActiveBrush");
            StatusBadge.Background = new SolidColorBrush(Color.FromArgb(50, 0, 217, 126));
            TimerDisplay.Foreground = (Brush)FindResource("PrimaryTextBrush");
            WarningBannerText.Text = "⚠️ 10 MINUTES REMAINING: Please save your work.";
            WarningBanner.Visibility = Visibility.Visible;
        }
        else
        {
            StatusText.Text = "● ACTIVE";
            StatusText.Foreground = (Brush)FindResource("ActiveBrush");
            StatusBadge.Background = new SolidColorBrush(Color.FromArgb(50, 0, 217, 126));
            TimerDisplay.Foreground = (Brush)FindResource("ActiveBrush");
            WarningBanner.Visibility = Visibility.Collapsed;
        }

        // Trigger distinct warning sounds when warning threshold is hit
        if (tickResult.TriggeredWarningMinutes.HasValue && (_settings?.SoundEnabled ?? true))
        {
            AudioAlertService.PlayWarningSound(tickResult.TriggeredWarningMinutes.Value);
        }

        // Ensure subtle HUD overlay is active and updated
        if (_miniWidgetWindow == null)
        {
            _miniWidgetWindow = new TimerWidgetWindow();
            _miniWidgetWindow.Show();
        }

        _miniWidgetWindow.UpdateDisplay(TimerDisplay.Text, isLowTime, isPaused);
    }

    private void MiniWidgetBtn_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this);
        if (mainWindow != null)
        {
            // Minimize to taskbar — subtle top-right HUD remains visible on screen
            mainWindow.WindowState = WindowState.Minimized;
        }
    }

    private void CloseMiniWidget()
    {
        if (_miniWidgetWindow != null)
        {
            _miniWidgetWindow.Close();
            _miniWidgetWindow = null;
        }
    }

    private void AdminLoginBtn_Click(object sender, RoutedEventArgs e)
    {
        var nav = App.Services.GetRequiredService<NavigationService>();
        var adminLogin = App.Services.GetRequiredService<AdminLogin>();
        nav.NavigateTo(adminLogin);
    }
}
