# SpaceLens

**Understand your storage. Clean it safely.**

SpaceLens is a Windows desktop **Storage Analyzer + Cleanup Assistant**. It scans local drives, explains what is using space, classifies files with a transparent risk engine, and only deletes after explicit confirmation (default: Recycle Bin).

## Privacy

Your files stay on your computer. Storage analysis is performed locally. There is no telemetry by default and no cloud upload of file metadata or contents.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## How to install as a desktop app

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Install-DesktopApp.ps1
```

This publishes SpaceLens to `%LocalAppData%\Programs\SpaceLens` and creates **Desktop** and **Start Menu** shortcuts. Double-click SpaceLens like any other Windows app.

## How to uninstall

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Uninstall-DesktopApp.ps1
```

Add `-RemoveUserData` to also delete settings and scan history under `%LocalAppData%\SpaceLens`.

Developer run (without install):

```powershell
dotnet run --project src\SpaceLens\SpaceLens.csproj
```

## How to build

```powershell
dotnet build SpaceLens.sln -c Release
```

Output:

`src\SpaceLens\bin\Release\net8.0-windows\SpaceLens.exe`

## How to package for Windows

```powershell
dotnet publish src\SpaceLens\SpaceLens.csproj -c Release -r win-x64 --self-contained false -o publish
```

For a self-contained single-folder package:

```powershell
dotnet publish src\SpaceLens\SpaceLens.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-standalone
```

## How permissions work

- Runs as a normal user by default (`asInvoker`).
- Access-denied folders are recorded as errors; the scan continues.
- Settings includes **Run with elevated permissions (UAC)** which relaunches via the standard Windows elevation prompt. SpaceLens never bypasses UAC.

## How the scanner works

1. Enumerates ready fixed/removable/network drives (or a chosen folder/drive).
2. Walks the filesystem with inaccessible paths ignored, junctions/reparse points skipped (no infinite recursion).
3. Aggregates folder sizes, category totals, largest files/folders incrementally.
4. Detects development artifacts (`node_modules`, `.next`, `__pycache__`, etc.) as units.
5. Finds duplicate *candidates* by size, then hashes only those candidates.
6. Builds cleanup suggestions with SAFE / REVIEW / PROTECTED / UNKNOWN rules.

Long paths are enabled via the application manifest (`longPathAware`).

## Safety model

| Risk | Meaning |
|------|---------|
| SAFE | Known temp/cache/regeneratable data |
| REVIEW | Likely removable but needs human judgment |
| PROTECTED | Windows/system/app-critical or personal documents — never one-click delete |
| UNKNOWN | Not confidently classified — never treated as SAFE |

Protected roots include Windows, System32, Program Files, boot/page/hiber files, credential stores, and whole user-profile special folders.

## Cleanup

- Default action: **Move to Recycle Bin**
- Permanent delete requires two confirmations
- Protected items are blocked in the cleanup service even if somehow selected

## Tests

```powershell
dotnet test SpaceLens.sln
```

## Architecture

| Project | Role |
|---------|------|
| `SpaceLens.Core` | Models, classification, risk engine, explanations, settings, cleanup rules |
| `SpaceLens.Scanner` | Filesystem scan, duplicates, cleanup execution, reports |
| `SpaceLens` | WPF UI (MVVM) |
| `SpaceLens.Tests` | Safety and unit tests |

## Documentation

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for deeper design notes.

## Limitations

- Application detection is heuristic (path matching), not an uninstaller.
- Duplicate hashing uses a fast size + partial-content hash for large files (full hash for small files).
- Full PC scans of millions of files can take a long time; use folder/drive scans for quicker results.
- Some folders require elevation to read; SpaceLens reports them rather than crashing.
