using System.Security.Principal;

namespace TimePay.Core.Interfaces;

/// <summary>
/// Service for Windows role/identity detection and auto-start registry configuration.
/// </summary>
public interface IStartupService
{
    /// <summary>
    /// Checks if the current Windows process is running with elevated Administrator privileges.
    /// </summary>
    bool IsRunningAsWindowsAdmin();

    /// <summary>
    /// Gets the current Windows username.
    /// </summary>
    string GetCurrentWindowsUsername();

    /// <summary>
    /// Checks if TimePay is currently configured to start automatically with Windows.
    /// </summary>
    bool IsAutoStartEnabled();

    /// <summary>
    /// Sets whether TimePay starts automatically with Windows.
    /// </summary>
    bool SetAutoStart(bool enable);
}
