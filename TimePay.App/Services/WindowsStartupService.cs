using System.Diagnostics;
using System.IO;
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
                using var cuKey = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                if (cuKey?.GetValue(AppName) != null) return true;

                using var lmKey = Registry.LocalMachine.OpenSubKey(RunRegistryKey, false);
                if (lmKey?.GetValue(AppName) != null) return true;

                var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                if (!string.IsNullOrEmpty(commonStartup) && File.Exists(Path.Combine(commonStartup, "TimePay.lnk")))
                {
                    return true;
                }

                var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (!string.IsNullOrEmpty(userStartup) && File.Exists(Path.Combine(userStartup, "TimePay.lnk")))
                {
                    return true;
                }
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
            if (!OperatingSystem.IsWindows()) return false;

            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return false;

            if (enable)
            {
                // 1. Current User Run Key
                try
                {
                    using var cuKey = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                    cuKey?.SetValue(AppName, $"\"{exePath}\"");
                }
                catch { }

                // 2. Local Machine Run Key (Requires Admin, best-effort)
                try
                {
                    using var lmKey = Registry.LocalMachine.OpenSubKey(RunRegistryKey, true);
                    lmKey?.SetValue(AppName, $"\"{exePath}\"");
                }
                catch { }

                // 3. User Startup Folder Shortcut
                try
                {
                    var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                    if (!string.IsNullOrEmpty(userStartup))
                    {
                        CreateShortcut(Path.Combine(userStartup, "TimePay.lnk"), exePath);
                    }
                }
                catch { }

                // 4. Common (All Users) Startup Folder Shortcut
                try
                {
                    var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                    if (!string.IsNullOrEmpty(commonStartup))
                    {
                        CreateShortcut(Path.Combine(commonStartup, "TimePay.lnk"), exePath);
                    }
                }
                catch { }

                return true;
            }
            else
            {
                // Remove from HKCU
                try
                {
                    using var cuKey = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                    cuKey?.DeleteValue(AppName, false);
                }
                catch { }

                // Remove from HKLM
                try
                {
                    using var lmKey = Registry.LocalMachine.OpenSubKey(RunRegistryKey, true);
                    lmKey?.DeleteValue(AppName, false);
                }
                catch { }

                // Remove shortcuts
                try
                {
                    var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                    var userLnk = Path.Combine(userStartup, "TimePay.lnk");
                    if (File.Exists(userLnk)) File.Delete(userLnk);

                    var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                    var commonLnk = Path.Combine(commonStartup, "TimePay.lnk");
                    if (File.Exists(commonLnk)) File.Delete(commonLnk);
                }
                catch { }

                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void EnsureAllUserStartup()
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return;

            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            // Ensure HKCU has TimePay run key
            try
            {
                using var cuKey = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (cuKey != null)
                {
                    var existing = cuKey.GetValue(AppName) as string;
                    if (string.IsNullOrEmpty(existing) || !existing.Contains(exePath, StringComparison.OrdinalIgnoreCase))
                    {
                        cuKey.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
            }
            catch { }

            // Ensure HKLM has TimePay run key if admin
            if (IsRunningAsWindowsAdmin())
            {
                try
                {
                    using var lmKey = Registry.LocalMachine.OpenSubKey(RunRegistryKey, true);
                    lmKey?.SetValue(AppName, $"\"{exePath}\"");
                }
                catch { }

                // Ensure Common Startup shortcut exists
                try
                {
                    var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                    if (!string.IsNullOrEmpty(commonStartup))
                    {
                        var lnkPath = Path.Combine(commonStartup, "TimePay.lnk");
                        if (!File.Exists(lnkPath))
                        {
                            CreateShortcut(lnkPath, exePath);
                        }
                    }
                }
                catch { }
            }
            else
            {
                // Standard/Guest user: Ensure user startup shortcut exists
                try
                {
                    var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                    if (!string.IsNullOrEmpty(userStartup))
                    {
                        var lnkPath = Path.Combine(userStartup, "TimePay.lnk");
                        if (!File.Exists(lnkPath))
                        {
                            CreateShortcut(lnkPath, exePath);
                        }
                    }
                }
                catch { }
            }
        }
        catch
        {
            // Ignore background startup sync errors
        }
    }

    /// <inheritdoc />
    public void ApplyRolePolicies()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                const string systemPolicyKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
                const string explorerPolicyKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
                bool isWindowsAdmin = IsRunningAsWindowsAdmin();

                if (isWindowsAdmin)
                {
                    // For Administrator: Ensure Task Manager, Volume Icon, Sound Settings, and Win Keys are ENABLED
                    using (var key = Registry.CurrentUser.OpenSubKey(systemPolicyKey, true))
                    {
                        if (key?.GetValue("DisableTaskMgr") != null) key.DeleteValue("DisableTaskMgr", false);
                        if (key?.GetValue("DisableLockWorkstation") != null) key.DeleteValue("DisableLockWorkstation", false);
                        if (key?.GetValue("DisableChangePassword") != null) key.DeleteValue("DisableChangePassword", false);
                    }

                    using (var expKey = Registry.CurrentUser.OpenSubKey(explorerPolicyKey, true))
                    {
                        if (expKey?.GetValue("NoWinKeys") != null) expKey.DeleteValue("NoWinKeys", false);
                        if (expKey?.GetValue("HideSCAVolume") != null) expKey.DeleteValue("HideSCAVolume", false);
                        if (expKey?.GetValue("SettingsPageVisibility") != null) expKey.DeleteValue("SettingsPageVisibility", false);
                        if (expKey?.GetValue("DisallowRun") != null) expKey.DeleteValue("DisallowRun", false);
                    }

                    // Also clean up machine-wide HKLM overrides so admin is not restricted
                    try
                    {
                        const string hklmExplorerKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer";
                        using var hklmExpKey = Registry.LocalMachine.OpenSubKey(hklmExplorerKey, true);
                        if (hklmExpKey?.GetValue("HideSCAVolume") != null) hklmExpKey.DeleteValue("HideSCAVolume", false);
                        if (hklmExpKey?.GetValue("SettingsPageVisibility") != null) hklmExpKey.DeleteValue("SettingsPageVisibility", false);
                    }
                    catch { }
                }
                else
                {
                    // For Standard/Guest User: Hardened lockdown with hidden volume and sound settings
                    using (var key = Registry.CurrentUser.CreateSubKey(systemPolicyKey, true))
                    {
                        key?.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                        key?.SetValue("DisableLockWorkstation", 1, RegistryValueKind.DWord);
                        key?.SetValue("DisableChangePassword", 1, RegistryValueKind.DWord);
                    }

                    using (var expKey = Registry.CurrentUser.CreateSubKey(explorerPolicyKey, true))
                    {
                        expKey?.SetValue("NoWinKeys", 1, RegistryValueKind.DWord);
                        expKey?.SetValue("HideSCAVolume", 1, RegistryValueKind.DWord);
                        expKey?.SetValue("SettingsPageVisibility", "hide:sound;volume-mixer", RegistryValueKind.String);
                        expKey?.SetValue("DisallowRun", 1, RegistryValueKind.DWord);
                    }

                    using (var disallowKey = Registry.CurrentUser.CreateSubKey($@"{explorerPolicyKey}\DisallowRun", true))
                    {
                        disallowKey?.SetValue("1", "sndvol.exe", RegistryValueKind.String);
                        disallowKey?.SetValue("2", "SndVol.exe", RegistryValueKind.String);
                    }
                }
            }
        }
        catch
        {
            // Ignore if standard user lacks permission to modify certain registry keys
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                    shortcut.Description = "TimePay Windows Client";
                    shortcut.Save();
                }
            }
        }
        catch
        {
            // Best effort
        }
    }
}

