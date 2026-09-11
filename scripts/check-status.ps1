# PCNotify - 查看当前状态脚本
$CurrentScriptDir = $PSScriptRoot
if (-not $CurrentScriptDir) {
    $CurrentScriptDir = Split-Path -Parent $PSCommandPath
}
$ProjectRoot = (Get-Item $CurrentScriptDir).Parent.FullName
$ExePath = Join-Path $ProjectRoot "src\PCMonitor\bin\Debug\net8.0-windows\PCMonitor.exe"
$SlnPath = Join-Path $ProjectRoot "PCNotify.sln"

if (-not (Test-Path $ExePath)) {
    dotnet build "$SlnPath" -c Debug
}

& "$ExePath" --status | Out-String
