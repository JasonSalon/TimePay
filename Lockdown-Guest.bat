@echo off
:: TimePay 1-Click Guest Lockdown (Disables Task Manager for Guests)
:: Right-click and choose "Run as administrator"

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Requesting Administrative Privileges...
    powershell -Command "Start-Process '%~0' -Verb RunAs"
    exit /b
)

title TimePay Guest Lockdown
echo ========================================================
echo         TIMEPAY GUEST SECURITY HARDENING
echo ========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\lockdown.ps1" -EnableLockdown

echo.
pause
