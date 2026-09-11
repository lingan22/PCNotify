# PCNotify - Uninstaller Script
$PurgeData = $args -contains "-PurgeData"

$ErrorActionPreference = "SilentlyContinue"

Write-Host "==================================================" -ForegroundColor Yellow
Write-Host " PCNotify Uninstaller" -ForegroundColor Yellow
Write-Host "==================================================" -ForegroundColor Yellow

# 1. Stop running processes
Write-Host "[1/4] Stopping PCNotify processes..." -ForegroundColor Yellow
Stop-Process -Name PCNotify -Force -ErrorAction SilentlyContinue
Stop-Process -Name PCMonitor -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 2. Unregister Task Scheduler
Write-Host "[2/4] Unregistering Task Scheduler..." -ForegroundColor Yellow
schtasks /delete /tn "PCNotify_PCMonitor" /f | Out-Null

# 3. Clean shortcuts
Write-Host "[3/4] Cleaning Desktop and Start Menu shortcuts..." -ForegroundColor Yellow
$DesktopPath = [Environment]::GetFolderPath("Desktop")
$StartMenuPath = [Environment]::GetFolderPath("Programs")

Remove-Item -Path (Join-Path $DesktopPath "PCNotify.lnk") -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path $StartMenuPath "PCNotify.lnk") -Force -ErrorAction SilentlyContinue

# 4. Remove installation directory
Write-Host "[4/4] Removing program installation directory..." -ForegroundColor Yellow
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\PCNotify"
if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
}

# 5. Optional data purge
$UserDataDir = Join-Path $env:LOCALAPPDATA "PCNotify"
if ($PurgeData -and (Test-Path $UserDataDir)) {
    Remove-Item -Path $UserDataDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "User configurations and logs have been purged." -ForegroundColor DarkGray
} else {
    Write-Host "Note: Configuration and logs retained at: $UserDataDir" -ForegroundColor Cyan
    Write-Host "To purge all data: .\scripts\uninstall.ps1 -PurgeData" -ForegroundColor DarkGray
}

Write-Host "--------------------------------------------------"
Write-Host "[PCNotify] Uninstall completed cleanly." -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Yellow
