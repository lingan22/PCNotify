$SelfContained = $args -contains "-SelfContained"
$ErrorActionPreference = "Stop"

$CurrentScriptDir = $PSScriptRoot
if (-not $CurrentScriptDir) {
    $CurrentScriptDir = Split-Path -Parent $PSCommandPath
}
$ProjectRoot = (Get-Item $CurrentScriptDir).Parent.FullName
$ProjectPath = Join-Path $ProjectRoot "src\PCMonitor\PCMonitor.csproj"
$DistDir = Join-Path $ProjectRoot "dist"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " PCNotify Single-File Release Publisher" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Project Path: $ProjectPath"
Write-Host "Output Dir:   $DistDir"
$modeDesc = if ($SelfContained) { "Self-Contained (No .NET runtime required)" } else { "Framework-Dependent (~200KB lightweight)" }
Write-Host "Publish Mode: $modeDesc"
Write-Host "--------------------------------------------------"

if (Test-Path $DistDir) {
    Remove-Item $DistDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

$publishArgs = @(
    "publish",
    "$ProjectPath",
    "-c", "Release",
    "-r", "win-x64",
    "-o", "$DistDir",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true"
)

if ($SelfContained) {
    $publishArgs += "--self-contained"
    $publishArgs += "true"
} else {
    $publishArgs += "--self-contained"
    $publishArgs += "false"
}

Write-Host "Compiling standalone executable with dotnet publish..." -ForegroundColor Yellow
& dotnet @publishArgs

$DefaultExe = Join-Path $DistDir "PCMonitor.exe"
$OutputExe = Join-Path $DistDir "PCNotify.exe"

if (Test-Path $DefaultExe) {
    Move-Item -Path $DefaultExe -Destination $OutputExe -Force
}

Get-ChildItem -Path $DistDir -Filter "*.pdb" | Remove-Item -Force -ErrorAction SilentlyContinue

if (Test-Path $OutputExe) {
    $item = Get-Item $OutputExe
    $sizeKb = [math]::Round($item.Length / 1KB, 1)
    $sizeMb = [math]::Round($item.Length / 1MB, 2)
    
    Write-Host "--------------------------------------------------"
    Write-Host "[PCNotify] Single-file publish successful!" -ForegroundColor Green
    Write-Host "Output Exe: $OutputExe" -ForegroundColor Green
    Write-Host "File Size:  $sizeKb KB ($sizeMb MB)" -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Cyan
} else {
    Write-Host "[PCNotify] Publish failed. Output executable not found." -ForegroundColor Red
    exit 1
}
