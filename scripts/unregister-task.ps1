# PCNotify - 注销 Windows 任务计划程序脚本
$CurrentScriptDir = $PSScriptRoot
if (-not $CurrentScriptDir) {
    $CurrentScriptDir = Split-Path -Parent $PSCommandPath
}
$ProjectRoot = (Get-Item $CurrentScriptDir).Parent.FullName
$ExePath = Join-Path $ProjectRoot "src\PCMonitor\bin\Debug\net8.0-windows\PCMonitor.exe"

if (Test-Path $ExePath) {
    & "$ExePath" --unregister-task | Out-String
} else {
    Write-Host "[PCNotify] 使用系统 schtasks 注销任务..." -ForegroundColor Yellow
    schtasks.exe /Delete /TN "PCNotify_PCMonitor" /F
}
