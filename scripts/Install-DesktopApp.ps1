# Install SpaceLens as a normal Windows desktop app
# - Publishes to %LocalAppData%\Programs\SpaceLens
# - Creates Desktop + Start Menu shortcuts

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path (Join-Path $root "SpaceLens.sln"))) {
    $root = "c:\Users\MY-PC\Music\SpaceLens"
}

$project = Join-Path $root "src\SpaceLens\SpaceLens.csproj"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\SpaceLens"
$exeName = "SpaceLens.exe"

Write-Host "Publishing SpaceLens to $installDir ..."
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

dotnet publish $project -c Release -r win-x64 --self-contained false -o $installDir
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

$exe = Join-Path $installDir $exeName
if (-not (Test-Path $exe)) { throw "Missing $exe" }

# Copy icon next to exe for shortcut reliability
$icoSrc = Join-Path $root "src\SpaceLens\Assets\logo.ico"
$icoDst = Join-Path $installDir "SpaceLens.ico"
if (Test-Path $icoSrc) { Copy-Item $icoSrc $icoDst -Force }

# Ship license with the install
$licenseSrc = Join-Path $root "LICENSE"
if (Test-Path $licenseSrc) { Copy-Item $licenseSrc (Join-Path $installDir "LICENSE.txt") -Force }

function New-Shortcut([string]$path, [string]$target, [string]$workDir, [string]$icon) {
    $w = New-Object -ComObject WScript.Shell
    $s = $w.CreateShortcut($path)
    $s.TargetPath = $target
    $s.WorkingDirectory = $workDir
    $s.Description = "SpaceLens - Understand your storage. Clean it safely."
    $s.IconLocation = "$icon,0"
    $s.Save()
}

$desktop = [Environment]::GetFolderPath("Desktop")
$startMenu = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs"
New-Item -ItemType Directory -Force -Path $startMenu | Out-Null

$iconPath = if (Test-Path $icoDst) { $icoDst } else { $exe }
New-Shortcut (Join-Path $desktop "SpaceLens.lnk") $exe $installDir $iconPath
New-Shortcut (Join-Path $startMenu "SpaceLens.lnk") $exe $installDir $iconPath

Write-Host ""
Write-Host "Installed."
Write-Host "  App:      $exe"
Write-Host "  Desktop:  $(Join-Path $desktop 'SpaceLens.lnk')"
Write-Host "  Start:    $(Join-Path $startMenu 'SpaceLens.lnk')"
Write-Host ""
Write-Host "Launching..."
Start-Process $exe
