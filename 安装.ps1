param([string]$GameRoot = 'D:\SteamLibrary\steamapps\common\时光餐厅')
$ErrorActionPreference = 'Stop'
if (Get-Process -Name TimeRestaurant -ErrorAction SilentlyContinue) { throw '请先完全退出游戏，再安装或更新模组。' }
if (-not (Test-Path -LiteralPath (Join-Path $GameRoot 'TimeRestaurant.exe'))) { throw '所选目录没有时光餐厅游戏程序。' }
$source = Join-Path $PSScriptRoot 'RestaurantBoost'
$target = Join-Path $GameRoot 'Mods\RestaurantBoost'
$publishedId = $null
$existingInfo = Join-Path $target 'info.ini'
if (Test-Path -LiteralPath $existingInfo) {
    $existingText = Get-Content -LiteralPath $existingInfo -Raw
    $idMatch = [regex]::Match($existingText, '(?m)^\s*publishedFileId\s*=\s*([0-9]+)\s*$')
    if ($idMatch.Success -and $idMatch.Groups[1].Value.TrimStart('0').Length -gt 0) {
        $publishedId = $idMatch.Groups[1].Value
    }
}
if (Test-Path -LiteralPath $target) {
    $backup = Join-Path $PSScriptRoot ('备份\RestaurantBoost_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
    New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination $backup -Recurse
    Write-Output ('原模组已备份至：' + $backup)
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
Get-ChildItem -LiteralPath $source -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $target -Recurse -Force }
if ($null -ne $publishedId) {
    $installedInfo = Join-Path $target 'info.ini'
    $installedText = Get-Content -LiteralPath $installedInfo -Raw
    $installedText = [regex]::Replace($installedText, '(?m)^\s*publishedFileId\s*=\s*[0-9]+\s*$', ('publishedFileId = ' + $publishedId))
    Set-Content -LiteralPath $installedInfo -Value $installedText -Encoding utf8
    Write-Output ('保留已有创意工坊作品编号：' + $publishedId)
}
Write-Output '安装完成。请在游戏模组列表中启用“餐厅效率助手”，进入餐厅后点击右上角新图标。'
