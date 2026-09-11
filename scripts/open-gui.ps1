# PCNotify - 打开控制面板脚本
$CurrentScriptDir = $PSScriptRoot
if (-not $CurrentScriptDir) {
    $CurrentScriptDir = Split-Path -Parent $PSCommandPath
}
$ProjectRoot = (Get-Item $CurrentScriptDir).Parent.FullName
$DistExe = Join-Path $ProjectRoot "dist\PCNotify.exe"
$DebugExe = Join-Path $ProjectRoot "src\PCMonitor\bin\Debug\net8.0-windows\PCMonitor.exe"
$SlnPath = Join-Path $ProjectRoot "PCNotify.sln"

$TargetExe = if (Test-Path $DistExe) { $DistExe } else { $DebugExe }

if (-not (Test-Path $TargetExe)) {
    dotnet build "$SlnPath" -c Debug
    $TargetExe = $DebugExe
}

Start-Process -FilePath "$TargetExe" -ArgumentList "--gui"
Write-Host "[PCNotify] Control panel launched successfully ($TargetExe)." -ForegroundColor Green
