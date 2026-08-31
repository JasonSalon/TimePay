using System.Windows.Threading;

namespace TimePay.App.Services;

/// <summary>
/// Manages admin session with automatic timeout.
/// When an admin logs in, a timer starts. If the admin is inactive
/// for the configured duration, the session expires automatically.
/// </summary>
public class AdminSessionService
{
    private readonly DispatcherTimer _timeoutTimer;
    private DateTimeOffset _lastActivity;

    /// <summary>
    /// Admin session timeout in minutes. Default: 15 minutes.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 15;

    /// <summary>
    /// Whether an admin is currently logged in.
    /// </summary>
    public bool IsLoggedIn { get; private set; }

    /// <summary>
    /// The currently logged-in admin's username.
    /// </summary>
    public string? CurrentAdminUsername { get; private set; }

    /// <summary>
    /// The currently logged-in admin's ID.
    /// </summary>
    public int? CurrentAdminId { get; private set; }

    /// <summary>
    /// Fired when the admin session times out.
    /// </summary>
    public event EventHandler? SessionTimedOut;

    public AdminSessionService()
    {
        _timeoutTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _timeoutTimer.Tick += CheckTimeout;
    }

    /// <summary>
    /// Start an admin session.
    /// </summary>
    public void Login(int adminId, string username)
    {
        CurrentAdminId = adminId;
        CurrentAdminUsername = username;
        IsLoggedIn = true;
        _lastActivity = DateTimeOffset.UtcNow;
        _timeoutTimer.Start();
    }

    /// <summary>
    /// End the admin session.
    /// </summary>
    public void Logout()
    {
        CurrentAdminId = null;
        CurrentAdminUsername = null;
        IsLoggedIn = false;
        _timeoutTimer.Stop();
    }

    /// <summary>
    /// Record admin activity to reset the timeout.
    /// </summary>
    public void RecordActivity()
    {
        _lastActivity = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the remaining session time.
    /// </summary>
    public TimeSpan GetRemainingSessionTime()
    {
        if (!IsLoggedIn) return TimeSpan.Zero;
        var elapsed = DateTimeOffset.UtcNow - _lastActivity;
        var remaining = TimeSpan.FromMinutes(TimeoutMinutes) - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void CheckTimeout(object? sender, EventArgs e)
    {
        if (!IsLoggedIn) return;

        var elapsed = DateTimeOffset.UtcNow - _lastActivity;
        if (elapsed.TotalMinutes >= TimeoutMinutes)
        {
            Logout();
            SessionTimedOut?.Invoke(this, EventArgs.Empty);
        }
    }
}
