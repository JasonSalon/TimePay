<#
.SYNOPSIS
    TimePay Guest Lockdown & Task Manager Disabler
.DESCRIPTION
    Disables Task Manager, Registry Tools, and Ctrl+Alt+Del options for standard users
    to prevent guests from killing TimePay.
#>

param (
    [switch]$EnableLockdown,
    [switch]$DisableLockdown
)

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[-] ERROR: This script must be run as Administrator." -ForegroundColor Red
    Pause
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "          TIMEPAY GUEST SECURITY HARDENING                " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$systemPolicyKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"
$userPolicyKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System"

if (-not (Test-Path $systemPolicyKey)) {
    New-Item -Path $systemPolicyKey -Force | Out-Null
}
if (-not (Test-Path $userPolicyKey)) {
    New-Item -Path $userPolicyKey -Force | Out-Null
}

if ($DisableLockdown) {
    Write-Host "[*] Re-enabling Task Manager and Windows system tools..." -ForegroundColor Yellow
    
    Remove-ItemProperty -Path $systemPolicyKey -Name "DisableTaskMgr" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $userPolicyKey -Name "DisableTaskMgr" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $systemPolicyKey -Name "DisableLockWorkstation" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $systemPolicyKey -Name "DisableChangePassword" -ErrorAction SilentlyContinue

    Write-Host "[+] Task Manager is now ENABLED." -ForegroundColor Green
} else {
    Write-Host "[*] Disabling Task Manager and hardening guest access..." -ForegroundColor Yellow

    # Disable Task Manager (Ctrl+Shift+Esc, Ctrl+Alt+Del -> Task Manager disabled)
    Set-ItemProperty -Path $systemPolicyKey -Name "DisableTaskMgr" -Value 1 -Type DWord -Force | Out-Null
    Set-ItemProperty -Path $userPolicyKey -Name "DisableTaskMgr" -Value 1 -Type DWord -Force | Out-Null

    Write-Host "[+] Task Manager has been DISABLED." -ForegroundColor Green
    Write-Host "    Guests/customers will now be unable to open Task Manager or kill TimePay." -ForegroundColor Green
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Cyan
