# Uninstall SpaceLens desktop install
# - Removes %LocalAppData%\Programs\SpaceLens
# - Removes Desktop + Start Menu shortcuts
# - Optionally clears local settings/history under %LocalAppData%\SpaceLens

param(
    [switch]$RemoveUserData
)

$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "Programs\SpaceLens"
$dataDir = Join-Path $env:LOCALAPPDATA "SpaceLens"
$desktop = [Environment]::GetFolderPath("Desktop")
$startMenu = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs"

Write-Host "Uninstalling SpaceLens..."

# Stop running instance if possible
Get-Process -Name "SpaceLens" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping running SpaceLens (PID $($_.Id))..."
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Milliseconds 400

foreach ($link in @(
    (Join-Path $desktop "SpaceLens.lnk"),
    (Join-Path $startMenu "SpaceLens.lnk")
)) {
    if (Test-Path $link) {
        Remove-Item $link -Force
        Write-Host "Removed shortcut: $link"
    }
}

if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
    Write-Host "Removed app: $installDir"
} else {
    Write-Host "Install folder not found: $installDir"
}

if ($RemoveUserData) {
    if (Test-Path $dataDir) {
        Remove-Item $dataDir -Recurse -Force
        Write-Host "Removed user data: $dataDir"
    }
} else {
    Write-Host "Kept user data (settings/history): $dataDir"
    Write-Host "Pass -RemoveUserData to delete it."
}

Write-Host ""
Write-Host "Uninstall complete."
