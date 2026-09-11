# PCNotify - One-click Installer & Auto-Start Deployer
$ErrorActionPreference = "Stop"

$CurrentScriptDir = $PSScriptRoot
if (-not $CurrentScriptDir) {
    $CurrentScriptDir = Split-Path -Parent $PSCommandPath
}
$ProjectRoot = (Get-Item $CurrentScriptDir).Parent.FullName
$DistExe = Join-Path $ProjectRoot "dist\PCNotify.exe"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\PCNotify"
$InstalledExe = Join-Path $InstallDir "PCNotify.exe"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " PCNotify Installation & Deployment Wizard" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Ensure published single file
if (-not (Test-Path $DistExe)) {
    Write-Host "[1/5] Standalone executable not found, compiling now..." -ForegroundColor Yellow
    & powershell -ExecutionPolicy Bypass -File (Join-Path $CurrentScriptDir "publish.ps1")
} else {
    Write-Host "[1/5] Standalone executable ready: $DistExe" -ForegroundColor Green
}

# 2. Stop running instances
Write-Host "[2/5] Stopping any running instances..." -ForegroundColor Yellow
Stop-Process -Name PCNotify -Force -ErrorAction SilentlyContinue
Stop-Process -Name PCMonitor -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600

# 3. Copy to local user programs folder
Write-Host "[3/5] Installing to $InstallDir ..." -ForegroundColor Yellow
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}
Copy-Item -Path $DistExe -Destination $InstalledExe -Force

# 4. Create Desktop and Start Menu shortcuts
Write-Host "[4/5] Creating Desktop & Start Menu shortcuts..." -ForegroundColor Yellow
$wsh = New-Object -ComObject WScript.Shell

# Desktop shortcut
$DesktopPath = [Environment]::GetFolderPath("Desktop")
$ShortcutDesktop = $wsh.CreateShortcut((Join-Path $DesktopPath "PCNotify.lnk"))
$ShortcutDesktop.TargetPath = $InstalledExe
$ShortcutDesktop.Arguments = "--gui"
$ShortcutDesktop.Description = "PCNotify"
$ShortcutDesktop.WorkingDirectory = $InstallDir
$ShortcutDesktop.IconLocation = "$InstalledExe,0"
$ShortcutDesktop.Save()

# Start Menu shortcut
$StartMenuPath = [Environment]::GetFolderPath("Programs")
$ShortcutStart = $wsh.CreateShortcut((Join-Path $StartMenuPath "PCNotify.lnk"))
$ShortcutStart.TargetPath = $InstalledExe
$ShortcutStart.Arguments = "--gui"
$ShortcutStart.Description = "PCNotify"
$ShortcutStart.WorkingDirectory = $InstallDir
$ShortcutStart.IconLocation = "$InstalledExe,0"
$ShortcutStart.Save()

# 5. Register Task Scheduler auto-start
Write-Host "[5/5] Registering Windows Task Scheduler..." -ForegroundColor Yellow
& "$InstalledExe" --register-task | Out-Null

# Launch application
Write-Host "Launching PCNotify..." -ForegroundColor Green
Start-Process -FilePath "$InstalledExe" -ArgumentList "--gui"

Write-Host "--------------------------------------------------"
Write-Host "[PCNotify] Installation completed successfully!" -ForegroundColor Green
Write-Host "Installed Executable: $InstalledExe" -ForegroundColor Green
Write-Host "Desktop Shortcut:     $DesktopPath\PCNotify.lnk" -ForegroundColor Green
Write-Host "Auto-Start Task:      Registered (Logon trigger)" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Cyan
