# 构建全部 mod 并部署到游戏
# 用法：
#   .\build.ps1                    # 用默认游戏路径
#   .\build.ps1 -SkulDir "E://Skul" # 指定游戏目录
param(
    [string]$SkulDir = "D://Game//Learning//Skul"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root "dist"

Write-Host "Game dir : $SkulDir"
Write-Host "Output   : $dist`n"

$mods = @("DestinyBone", "DevMenuZhFix")

foreach ($m in $mods) {
    Write-Host "=== Building $m ===" -ForegroundColor Cyan
    $proj = Join-Path $root "src\$m\$m.csproj"
    dotnet build $proj -c Release -v q --nologo -p:SkulDir="$SkulDir"
    if ($LASTEXITCODE -ne 0) { throw "$m build failed" }

    $out = Join-Path $dist $m
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    Copy-Item (Join-Path $root "src\$m\bin\Release\$m.dll") $out -Force

    $target = Join-Path $SkulDir "BepInEx\plugins\$m"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item (Join-Path $out "$m.dll") $target -Force
    Write-Host "  deployed -> $target" -ForegroundColor Green
}

Write-Host "`nDone." -ForegroundColor Green
