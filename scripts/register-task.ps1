# PCNotify - 注册 Windows 任务计划程序脚本
$ErrorActionPreference = "Stop"

$CurrentScriptDir = $PSScriptRoot
if (-not $CurrentScriptDir) {
    $CurrentScriptDir = Split-Path -Parent $PSCommandPath
}
$ProjectRoot = (Get-Item $CurrentScriptDir).Parent.FullName
$ExePath = Join-Path $ProjectRoot "src\PCMonitor\bin\Debug\net8.0-windows\PCMonitor.exe"
$SlnPath = Join-Path $ProjectRoot "PCNotify.sln"

if (-not (Test-Path $ExePath)) {
    Write-Host "[PCNotify] 未找到可执行文件，正在先进行构建..." -ForegroundColor Yellow
    dotnet build "$SlnPath" -c Debug
    if (-not (Test-Path $ExePath)) {
        Write-Error "[PCNotify] 构建失败，未找到 $ExePath"
        exit 1
    }
}

Write-Host "[PCNotify] 正在注册任务计划程序..." -ForegroundColor Cyan
& "$ExePath" --register-task | Out-String
