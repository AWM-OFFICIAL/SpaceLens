# Installing SpaceLens

## Easiest path (recommended)

1. Go to [Releases](https://github.com/AWM-OFFICIAL/SpaceLens/releases/latest).
2. Download **SpaceLens-Windows-x64.zip**.
3. Extract it somewhere convenient (e.g. Downloads).
4. Double-click **Setup.bat**.
5. Allow PowerShell if Windows asks.
6. Open **SpaceLens** from the Desktop or Start Menu.

You are installing a local Windows app. Nothing is sent to our servers — SpaceLens has no cloud backend for scanning.

## What Setup.bat does

- Copies the app into `%LocalAppData%\Programs\SpaceLens`
- Creates Desktop and Start Menu shortcuts
- Copies the license next to the app
- Creates `Uninstall.ps1` in the install folder
- Launches SpaceLens

No administrator rights are required for a normal install.

## Portable mode

Run `App\SpaceLens.exe` from the unzipped folder without running Setup. Shortcuts will not be created.

## Uninstall

```text
%LocalAppData%\Programs\SpaceLens\Uninstall.ps1
```

Settings and history stay in `%LocalAppData%\SpaceLens` unless you delete that folder or use:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Uninstall-DesktopApp.ps1 -RemoveUserData
```

## Build from this repository

See the [README](../README.md#build-from-source-developers).
