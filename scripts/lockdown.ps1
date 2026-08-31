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
$systemExplorerPolicyKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer"
$userExplorerPolicyKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"

if (-not (Test-Path $systemPolicyKey)) { New-Item -Path $systemPolicyKey -Force | Out-Null }
if (-not (Test-Path $userPolicyKey)) { New-Item -Path $userPolicyKey -Force | Out-Null }
if (-not (Test-Path $systemExplorerPolicyKey)) { New-Item -Path $systemExplorerPolicyKey -Force | Out-Null }
if (-not (Test-Path $userExplorerPolicyKey)) { New-Item -Path $userExplorerPolicyKey -Force | Out-Null }

if ($DisableLockdown) {
    Write-Host "[*] Re-enabling Task Manager, Volume Controls, and Windows system tools..." -ForegroundColor Yellow
    
    Remove-ItemProperty -Path $systemPolicyKey -Name "DisableTaskMgr" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $userPolicyKey -Name "DisableTaskMgr" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $systemPolicyKey -Name "DisableLockWorkstation" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $systemPolicyKey -Name "DisableChangePassword" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $systemExplorerPolicyKey -Name "NoWinKeys" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $userExplorerPolicyKey -Name "NoWinKeys" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $systemExplorerPolicyKey -Name "HideSCAVolume" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $userExplorerPolicyKey -Name "HideSCAVolume" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $systemExplorerPolicyKey -Name "SettingsPageVisibility" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $userExplorerPolicyKey -Name "SettingsPageVisibility" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $userExplorerPolicyKey -Name "DisallowRun" -ErrorAction SilentlyContinue
    Remove-Item -Path "$userExplorerPolicyKey\DisallowRun" -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "[+] Task Manager, Volume Controls, and Windows Keys are now ENABLED." -ForegroundColor Green
} else {
    Write-Host "[*] Disabling Task Manager, Volume Controls, Windows Hotkeys, and hardening guest access..." -ForegroundColor Yellow

    # Disable Task Manager (Ctrl+Shift+Esc, Ctrl+Alt+Del -> Task Manager disabled)
    Set-ItemProperty -Path $systemPolicyKey -Name "DisableTaskMgr" -Value 1 -Type DWord -Force | Out-Null
    Set-ItemProperty -Path $userPolicyKey -Name "DisableTaskMgr" -Value 1 -Type DWord -Force | Out-Null
    Set-ItemProperty -Path $systemPolicyKey -Name "DisableLockWorkstation" -Value 1 -Type DWord -Force | Out-Null
    Set-ItemProperty -Path $systemPolicyKey -Name "DisableChangePassword" -Value 1 -Type DWord -Force | Out-Null

    # Disable Windows Hotkeys (Win+D, Win+R, Win+E, Win+X, etc.)
    Set-ItemProperty -Path $systemExplorerPolicyKey -Name "NoWinKeys" -Value 1 -Type DWord -Force | Out-Null
    Set-ItemProperty -Path $userExplorerPolicyKey -Name "NoWinKeys" -Value 1 -Type DWord -Force | Out-Null

    # Remove HKLM volume restrictions so Admin is never blocked
    Remove-ItemProperty -Path $systemExplorerPolicyKey -Name "HideSCAVolume" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $systemExplorerPolicyKey -Name "SettingsPageVisibility" -ErrorAction SilentlyContinue

    Write-Host "[+] Security restrictions applied. Guest users cannot adjust volume or kill TimePay." -ForegroundColor Green
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Cyan
