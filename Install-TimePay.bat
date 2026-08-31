@echo off
:: TimePay 1-Click Installer
:: Right-click and choose "Run as administrator"

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Requesting Administrative Privileges...
    powershell -Command "Start-Process '%~0' -Verb RunAs"
    exit /b
)

title TimePay Installer
echo ========================================================
echo               TIMEPAY INSTALLER
echo ========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install.ps1"

echo.
pause
