<#
.SYNOPSIS
    TimePay Automated Uninstaller
.DESCRIPTION
    Stops and removes TimePayService, removes shortcuts, and cleans up Program Files.
#>

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[-] ERROR: This script must be run as Administrator." -ForegroundColor Red
    Pause
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Yellow
Write-Host "          TIMEPAY AUTOMATED UNINSTALLER                   " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Yellow
Write-Host ""

$installDir = "$env:ProgramFiles\TimePay"

# 1. Stop & Remove Windows Service
Write-Host "[1/3] Stopping and removing TimePayService..." -ForegroundColor Yellow
if (Get-Service -Name "TimePayService" -ErrorAction SilentlyContinue) {
    Stop-Service -Name "TimePayService" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete TimePayService | Out-Null
    Write-Host "      TimePayService removed." -ForegroundColor Green
}

# 2. Remove Shortcuts
Write-Host "[2/3] Removing shortcuts..." -ForegroundColor Yellow
$desktopShortcut = "$([Environment]::GetFolderPath('CommonDesktopDirectory'))\TimePay.lnk"
$userDesktopShortcut = "$([Environment]::GetFolderPath('Desktop'))\TimePay.lnk"
$startMenuShortcut = "$([Environment]::GetFolderPath('CommonPrograms'))\TimePay.lnk"

Remove-Item -Path $desktopShortcut -Force -ErrorAction SilentlyContinue
Remove-Item -Path $userDesktopShortcut -Force -ErrorAction SilentlyContinue
Remove-Item -Path $startMenuShortcut -Force -ErrorAction SilentlyContinue

# 3. Remove Program Files directory
Write-Host "[3/3] Removing application files from $installDir..." -ForegroundColor Yellow
Remove-Item -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "      TIMEPAY HAS BEEN UNINSTALLED SUCCESSFULLY!          " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Note: Database records at C:\ProgramData\TimePay were preserved." -ForegroundColor Gray
Write-Host ""
