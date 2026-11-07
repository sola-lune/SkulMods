# 构建全部 mod 并部署到游戏
# 用法：
#   .\build.ps1                    # 用默认游戏路径
#   .\build.ps1 -SkulDir "E://Skul" # 指定游戏目录
#   .\build.ps1 -NoDeploy          # 只构建不部署
param(
    [string]$SkulDir = "D:/Game/Learning/Skul",
    [switch]$NoDeploy
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root "dist"

Write-Host "Game dir : $SkulDir"
Write-Host "Output   : $dist"
Write-Host ""

$running = @(Get-Process -Name "Skul" -ErrorAction SilentlyContinue).Count -gt 0
if ($running) {
    Write-Host "[!] Skul.exe is running - build only, deploy skipped." -ForegroundColor Yellow
}

$mods = @("DestinyBone", "DevMenuZhFix")

foreach ($m in $mods) {
    Write-Host "=== Building $m ===" -ForegroundColor Cyan
    $proj = Join-Path $root "src\$m\$m.csproj"
    dotnet build $proj -c Release -v q --nologo -p:SkulDir="$SkulDir"
    if ($LASTEXITCODE -ne 0) { throw "$m build failed" }

    $out = Join-Path $dist $m
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    Copy-Item (Join-Path $root "src\$m\bin\Release\$m.dll") $out -Force
    Write-Host "  -> dist/$m/$m.dll" -ForegroundColor Gray

    if ($NoDeploy -or $running) { continue }

    $target = Join-Path $SkulDir "BepInEx\plugins\$m"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    try {
        Copy-Item (Join-Path $out "$m.dll") $target -Force
        Write-Host "  deployed -> $target" -ForegroundColor Green
    } catch {
        Write-Host "  deploy failed (file locked?): $target" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
