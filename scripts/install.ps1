<#
.SYNOPSIS
    TimePay Automated Installer & Service Registrar
.DESCRIPTION
    Installs TimePay to C:\Program Files\TimePay, configures database directories,
    creates Desktop and Start Menu shortcuts, and registers TimePayService as an auto-start Windows Service.
#>

# Require Administrator Privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[-] ERROR: This script must be run as Administrator." -ForegroundColor Red
    Write-Host "[*] Right-click the install script and select 'Run with PowerShell' / 'Run as administrator'." -ForegroundColor Yellow
    Pause
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "          TIMEPAY AUTOMATED INSTALLER                     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$installDir = "$env:ProgramFiles\TimePay"
$dbDir = "$env:ProgramData\TimePay"
$sourceDir = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path "$sourceDir\publish\App\TimePay.App.exe")) {
    $sourceDir = $PSScriptRoot
}

# 1. Stop existing app and service if running
Write-Host "[1/6] Closing running TimePay processes..." -ForegroundColor Yellow
Stop-Process -Name "TimePay.App" -Force -ErrorAction SilentlyContinue
if (Get-Service -Name "TimePayService" -ErrorAction SilentlyContinue) {
    Write-Host "      Stopping existing TimePayService..." -ForegroundColor Gray
    Stop-Service -Name "TimePayService" -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2

# 2. Create Target Directories
Write-Host "[2/6] Creating installation directories..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path "$installDir\App" -Force | Out-Null
New-Item -ItemType Directory -Path "$installDir\Service" -Force | Out-Null
New-Item -ItemType Directory -Path $dbDir -Force | Out-Null

# 3. Copy Application & Service Files
Write-Host "[3/6] Copying TimePay files to $installDir..." -ForegroundColor Yellow
Copy-Item -Path "$sourceDir\publish\App\*" -Destination "$installDir\App" -Recurse -Force
Copy-Item -Path "$sourceDir\publish\Service\*" -Destination "$installDir\Service" -Recurse -Force

# 4. Configure NTFS File & Database Permissions
Write-Host "[4/6] Configuring security permissions on database directory..." -ForegroundColor Yellow
try {
    icacls $dbDir /inheritance:r | Out-Null
    icacls $dbDir /grant:r "SYSTEM:(OI)(CI)F" | Out-Null
    icacls $dbDir /grant:r "Administrators:(OI)(CI)F" | Out-Null
    icacls $dbDir /grant:r "Users:(OI)(CI)RX" | Out-Null
    icacls $dbDir /grant:r "Users:(OI)(CI)M" | Out-Null # Allow client UI to read/write shared db
} catch {
    Write-Host "      Warning: Could not set fine-grained ACLs (skipping)." -ForegroundColor Gray
}

# 5. Register & Start Windows Service
Write-Host "[5/6] Registering and starting TimePay Windows Service..." -ForegroundColor Yellow
$serviceExe = "$installDir\Service\TimePay.Service.exe"

if (Get-Service -Name "TimePayService" -ErrorAction SilentlyContinue) {
    sc.exe config TimePayService binPath= "`"$serviceExe`"" start= auto | Out-Null
} else {
    sc.exe create TimePayService binPath= "`"$serviceExe`"" start= auto DisplayName= "TimePay Time Management Service" | Out-Null
}

# Configure auto-recovery on crash
sc.exe failure TimePayService reset= 86400 actions= restart/5000/restart/10000/restart/60000 | Out-Null

# Start service
Start-Service -Name "TimePayService" -ErrorAction SilentlyContinue
$svcStatus = (Get-Service -Name "TimePayService").Status
Write-Host "      TimePayService status: $svcStatus" -ForegroundColor Green

# 6. Create Shortcuts & Configure Auto-Start
Write-Host "[6/6] Creating Shortcuts & configuring Windows Startup..." -ForegroundColor Yellow
$WshShell = New-Object -ComObject WScript.Shell

# Desktop Shortcut
$desktopPath = [Environment]::GetFolderPath("CommonDesktopDirectory")
if (-not (Test-Path $desktopPath)) { $desktopPath = [Environment]::GetFolderPath("Desktop") }
$shortcut = $WshShell.CreateShortcut("$desktopPath\TimePay.lnk")
$shortcut.TargetPath = "$installDir\App\TimePay.App.exe"
$shortcut.WorkingDirectory = "$installDir\App"
$shortcut.Description = "TimePay Windows PC Time Management"
$shortcut.Save()

# Start Menu Shortcut
$startMenuPath = [Environment]::GetFolderPath("CommonPrograms")
$shortcut2 = $WshShell.CreateShortcut("$startMenuPath\TimePay.lnk")
$shortcut2.TargetPath = "$installDir\App\TimePay.App.exe"
$shortcut2.WorkingDirectory = "$installDir\App"
$shortcut2.Description = "TimePay Windows PC Time Management"
$shortcut2.Save()

# Clean up any legacy Startup folder shortcuts and duplicate HKCU keys
$commonStartupPath = [Environment]::GetFolderPath("CommonStartup")
if (Test-Path "$commonStartupPath\TimePay.lnk") {
    Remove-Item -Path "$commonStartupPath\TimePay.lnk" -Force -ErrorAction SilentlyContinue
}
$userStartupPath = [Environment]::GetFolderPath("Startup")
if (Test-Path "$userStartupPath\TimePay.lnk") {
    Remove-Item -Path "$userStartupPath\TimePay.lnk" -Force -ErrorAction SilentlyContinue
}
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TimePay" -Force -ErrorAction SilentlyContinue | Out-Null

# Windows Auto-Start on Login (Machine-Wide Single Entry)
Set-ItemProperty -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TimePay" -Value "`"$installDir\App\TimePay.App.exe`"" -Force -ErrorAction SilentlyContinue | Out-Null



Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "      TIMEPAY HAS BEEN SUCCESSFULLY INSTALLED!            " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""
Write-Host " Installation Path : $installDir\App\TimePay.App.exe"
Write-Host " Windows Service   : TimePayService ($svcStatus)"
Write-Host " Database Location : $dbDir\timepay.db"
Write-Host " Desktop Shortcut  : Created on Desktop"
Write-Host ""
Write-Host "You can now launch TimePay from your Desktop shortcut!" -ForegroundColor Cyan
Write-Host ""
