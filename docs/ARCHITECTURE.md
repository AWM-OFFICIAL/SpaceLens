# SpaceLens Architecture

## Goals

SpaceLens helps users understand disk usage and clean safely. It prioritizes explanation and confirmation over aggressive cleanup.

## Components

### Classification (`SpaceLens.Core.Classification`)

- `FileClassifier` — extension and path heuristics for categories (Images, Videos, Development, Temporary, …).
- `ProtectedPathRules` — inspectable list of protected prefixes and filenames.
- `RiskEngine` — maps paths/categories to SAFE / REVIEW / PROTECTED / UNKNOWN.

UNKNOWN is never promoted to SAFE.

### Explanations

`ExplanationEngine` produces human-readable reasons derived from the same rules used for classification (not an external AI API).

### Scanner (`SpaceLens.Scanner.StorageScanner`)

- Background `Task.Run` with `IProgress<ScanProgress>` and `CancellationToken`.
- Parallel subdirectory walks when fan-out is high.
- Junction/reparse points skipped; visited-directory set prevents cycles.
- Bounded largest-file list; streaming category aggregation.
- Dev artifact directories measured as units (avoid enumerating every file in huge `node_modules` for classification UI, while still counting size).
- Duplicate detection: group by size → hash candidates only.

### Cleanup

`CleanupService` refuses `RiskLevel.Protected`, defaults to Shell recycle (`FOF_ALLOWUNDO`), and supports permanent delete only when the UI has confirmed twice.

### Persistence

Local JSON under `%LocalAppData%\SpaceLens\`:

- `settings.json`
- `scan-history.json` (sizes/categories only — no file contents)

### UI

WPF + CommunityToolkit.Mvvm. Dark/light themes. Custom donut/bar/history charts (no heavy charting dependency).

## Performance strategy

- Ignore inaccessible files instead of failing the scan.
- Skip reparse points.
- Hash only duplicate size-collisions above a configurable minimum size.
- Trim largest-file buffers periodically.
- Configurable parallelism (default ≈ half the CPU count).

## Privacy model

No network calls in core analysis. Reports are exported only when the user chooses, with a path-privacy warning.

## Testing strategy

Unit tests cover:

- Protected path false-positive prevention
- Classification
- Risk rules
- Size formatting
- Duplicate hash stability
- Folder aggregation on a tiny temp tree
- Cleanup blocking of protected items
