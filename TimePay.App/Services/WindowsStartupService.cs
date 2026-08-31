using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using TimePay.Core.Interfaces;

namespace TimePay.App.Services;

/// <summary>
/// Implements Windows role detection and auto-startup registry management.
/// </summary>
public class WindowsStartupService : IStartupService
{
    private const string AppName = "TimePay";
    private const string RunRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <inheritdoc />
    public bool IsRunningAsWindowsAdmin()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        catch
        {
            // Ignore and default to false
        }
        return false;
    }

    /// <inheritdoc />
    public string GetCurrentWindowsUsername()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return Environment.UserName;
            }
        }
        catch
        {
            // Ignore
        }
        return "Unknown";
    }

    /// <inheritdoc />
    public bool IsAutoStartEnabled()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
        }
        catch
        {
            // Fallback
        }
        return false;
    }

    /// <inheritdoc />
    public bool SetAutoStart(bool enable)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return false;

                if (enable)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                        return true;
                    }
                }
                else
                {
                    if (key.GetValue(AppName) != null)
                    {
                        key.DeleteValue(AppName, false);
                    }
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }
        return false;
    }
}
