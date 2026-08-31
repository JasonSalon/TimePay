using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TimePay.App.Services;

/// <summary>
/// Low-level Windows keyboard hook service (WH_KEYBOARD_LL).
/// Intercepts and suppresses dangerous system keys and combinations
/// (Windows Key, Alt+Tab, Alt+F4, Ctrl+Esc, Task Manager shortcuts, etc.)
/// during Kiosk / Lock Screen mode.
/// </summary>
public class KeyboardHookService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    // Virtual key codes
    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_SPACE = 0x20;
    private const int VK_F4 = 0x73;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_APPS = 0x5D;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt key
    private const int VK_VOLUME_MUTE = 0xAD;
    private const int VK_VOLUME_DOWN = 0xAE;
    private const int VK_VOLUME_UP = 0xAF;

    private const int LLKHF_ALTDOWN = 0x20;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private bool _isHookActive = false;

    public bool IsHookActive => _isHookActive;

    /// <summary>
    /// Enables the low-level keyboard hook to suppress system hotkeys.
    /// </summary>
    public void EnableHook()
    {
        if (_isHookActive) return;

        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule?.ModuleName), 0);
        _isHookActive = _hookId != IntPtr.Zero;
    }

    /// <summary>
    /// Disables the low-level keyboard hook, restoring normal keyboard behavior.
    /// </summary>
    public void DisableHook()
    {
        if (!_isHookActive) return;

        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        _isHookActive = false;
        _proc = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isHookActive)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vkCode = hookStruct.vkCode;
            int flags = hookStruct.flags;
            bool isAltDown = (flags & LLKHF_ALTDOWN) != 0;
            bool isCtrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

            // 1. Block Windows Keys (LWin, RWin, Apps key)
            if (vkCode == VK_LWIN || vkCode == VK_RWIN || vkCode == VK_APPS)
            {
                return (IntPtr)1; // Block
            }

            // 2. Block Media Volume Keys (Mute, Volume Down, Volume Up)
            if (vkCode == VK_VOLUME_MUTE || vkCode == VK_VOLUME_DOWN || vkCode == VK_VOLUME_UP)
            {
                return (IntPtr)1; // Block
            }

            // 3. Block Alt Combinations (Alt+Tab, Alt+Esc, Alt+F4, Alt+Space, Alt+Enter, etc.)
            if (isAltDown)
            {
                if (vkCode == VK_TAB || vkCode == VK_ESCAPE || vkCode == VK_F4 || vkCode == VK_SPACE ||
                    (vkCode >= 0x70 && vkCode <= 0x7B)) // Alt + F1..F12
                {
                    return (IntPtr)1; // Block
                }

                // Block general Alt key sequences from activating menus
                if (vkCode == VK_MENU || vkCode == 0xA4 || vkCode == 0xA5)
                {
                    return (IntPtr)1; // Block
                }
            }

            // 4. Block Ctrl Combinations (Ctrl+Esc, Ctrl+Shift+Esc, Ctrl+Alt+Tab)
            if (isCtrlDown)
            {
                if (vkCode == VK_ESCAPE || vkCode == VK_TAB)
                {
                    return (IntPtr)1; // Block
                }
            }

            // 5. Block standalone Function Keys (F1..F12) that might trigger Windows help / search / etc.
            if (vkCode >= 0x70 && vkCode <= 0x7B)
            {
                return (IntPtr)1; // Block
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        DisableHook();
        GC.SuppressFinalize(this);
    }
}
