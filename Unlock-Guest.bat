@echo off
:: TimePay Re-enable Task Manager (For Admin Maintenance)
:: Right-click and choose "Run as administrator"

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Requesting Administrative Privileges...
    powershell -Command "Start-Process '%~0' -Verb RunAs"
    exit /b
)

title TimePay Maintenance Unlock
echo ========================================================
echo       TIMEPAY RE-ENABLE TASK MANAGER (MAINTENANCE)
echo ========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\lockdown.ps1" -DisableLockdown

echo.
pause
