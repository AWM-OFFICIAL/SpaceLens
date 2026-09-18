using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SpaceLens.Core.Explanations;
using SpaceLens.Core.Models;
using SpaceLens.Core.Settings;
using SpaceLens.Core.Utilities;
using SpaceLens.Controls;
using SpaceLens.Scanner;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;

namespace SpaceLens.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly StorageScanner _scanner = new();
    private CancellationTokenSource? _scanCts;
    private AppSettings _settings;

    public MainViewModel()
    {
        _settings = AppSettings.Load();
        ThemeName = _settings.Theme;
        ShowWelcome = !_settings.HasCompletedFirstRun;
        CurrentPage = ShowWelcome ? "Welcome" : "Overview";
        RefreshDrives();
        LoadHistory();
        ApplyTheme();
    }

    [ObservableProperty] private string currentPage = "Overview";
    [ObservableProperty] private bool showWelcome;
    [ObservableProperty] private bool isScanning;
    [ObservableProperty] private string scanPhase = string.Empty;
    [ObservableProperty] private string scanCurrentPath = string.Empty;
    [ObservableProperty] private long scanFilesIndexed;
    [ObservableProperty] private long scanFoldersIndexed;
    [ObservableProperty] private double scanPercent;
    [ObservableProperty] private string scanBytesLabel = string.Empty;
    [ObservableProperty] private ScanResult? result;
    [ObservableProperty] private string statusMessage = "Ready";
    [ObservableProperty] private string themeName = "Dark";

    public bool IsDarkTheme => !ThemeName.Equals("Light", StringComparison.OrdinalIgnoreCase);
    public string ThemeActionLabel => IsDarkTheme ? "Use light theme" : "Use dark theme";

    partial void OnThemeNameChanged(string value)
    {
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeActionLabel));
    }
    [ObservableProperty] private string searchQuery = string.Empty;
    [ObservableProperty] private string selectedRiskFilter = "All";
    [ObservableProperty] private string selectedCategoryFilter = "All";
    [ObservableProperty] private string selectedSizeFilter = "All";
    [ObservableProperty] private string selectedAgeFilter = "180";
    [ObservableProperty] private string detailTitle = string.Empty;
    [ObservableProperty] private string detailPath = string.Empty;
    [ObservableProperty] private string detailSize = string.Empty;
    [ObservableProperty] private string detailCategory = string.Empty;
    [ObservableProperty] private string detailRisk = string.Empty;
    [ObservableProperty] private string detailReason = string.Empty;
    [ObservableProperty] private string detailAction = string.Empty;
    [ObservableProperty] private string detailMeta = string.Empty;
    [ObservableProperty] private bool hasDetail;
    [ObservableProperty] private string explanationText = string.Empty;
    [ObservableProperty] private long potentialCleanupBytes;
    [ObservableProperty] private long safeCleanupBytes;
    [ObservableProperty] private long reviewCleanupBytes;
    [ObservableProperty] private string lastCleanupSummary = string.Empty;
    [ObservableProperty] private DriveSummary? primaryDrive;
    [ObservableProperty] private string usedLabel = "—";
    [ObservableProperty] private string freeLabel = "—";
    [ObservableProperty] private string totalLabel = "—";
    [ObservableProperty] private double usedPercent;

    [ObservableProperty] private bool hasScanResult;
    [ObservableProperty] private bool hasHistory;
    [ObservableProperty] private bool hasSearched;
    [ObservableProperty] private IReadOnlyList<CategoryBreakdown> chartCategories = Array.Empty<CategoryBreakdown>();

    public string AppVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public string AppVersionLabel => $"Version {AppVersion}";

    public bool HasCleanupItems => CleanupItems.Count > 0;
    public bool ShowCleanupEmpty => HasScanResult && CleanupItems.Count == 0;
    public bool HasDuplicateGroups => Duplicates.Count > 0;
    public bool ShowDuplicatesEmpty => HasScanResult && Duplicates.Count == 0;
    public bool HasDevArtifactsList => DevArtifacts.Count > 0;
    public bool ShowDevEmpty => HasScanResult && DevArtifacts.Count == 0;
    public bool HasApplicationsList => Applications.Count > 0;
    public bool ShowApplicationsEmpty => HasScanResult && Applications.Count == 0;
    public bool HasSearchResults => SearchResults.Count > 0;
    public bool ShowSearchEmpty => HasScanResult && HasSearched && SearchResults.Count == 0;
    public bool HasOldFilesList => OldFiles.Count > 0;
    public bool ShowOldFilesEmpty => HasScanResult && OldFiles.Count == 0;

    public ObservableCollection<DriveSummary> Drives { get; } = new();
    public ObservableCollection<CategoryBreakdown> Categories { get; } = new();
    public ObservableCollection<FileEntry> LargestFiles { get; } = new();
    public ObservableCollection<FolderNode> LargestFolders { get; } = new();
    public ObservableCollection<CleanupSuggestion> CleanupItems { get; } = new();
    public ObservableCollection<DuplicateGroup> Duplicates { get; } = new();
    public ObservableCollection<DevArtifact> DevArtifacts { get; } = new();
    public ObservableCollection<AppStorageInfo> Applications { get; } = new();
    public ObservableCollection<FileEntry> OldFiles { get; } = new();
    public ObservableCollection<FileEntry> SearchResults { get; } = new();
    public ObservableCollection<ScanHistoryEntry> History { get; } = new();
    public ObservableCollection<System.Windows.Point> HistoryPoints { get; } = new();
    public ObservableCollection<NavEntry> NavItems { get; } = new()
    {
        new("Overview", "Home", "SEE"),
        new("Largest Files", "Largest files", null),
        new("Largest Folders", "Largest folders", null),
        new("Cleanup", "Cleanup", "FREE SPACE"),
        new("Downloads", "Downloads", null),
        new("Old Files", "Old files", null),
        new("Duplicates", "Duplicates", null),
        new("Development", "Development", "LOOK CLOSER"),
        new("Applications", "Applications", null),
        new("Search", "Search", "FIND"),
        new("History", "History", null),
        new("Settings", "Settings", "APP"),
        new("Privacy", "Privacy", null),
        new("About", "About", null),
    };

    public AppSettings Settings => _settings;

    partial void OnSelectedAgeFilterChanged(string value) => RefreshOldFiles();

    [RelayCommand]
    private void Navigate(string page)
    {
        if (string.IsNullOrWhiteSpace(page)) return;
        CurrentPage = page;
        if (page == "Old Files")
            RefreshOldFiles();
        if (page == "History")
            LoadHistory();
    }

    [RelayCommand]
    private async Task StartWelcomeScanAsync()
    {
        ShowWelcome = false;
        CurrentPage = "Overview";
        await ScanEntirePcAsync();
        // First-run completes only after a successful scan (ApplyResult).
    }

    [RelayCommand]
    private async Task StartWelcomeFolderScanAsync()
    {
        var path = PromptForFolder();
        if (string.IsNullOrWhiteSpace(path))
            return; // Keep welcome visible if the user cancels

        ShowWelcome = false;
        CurrentPage = "Overview";
        await RunScanAsync(ScanTargetKind.Folder, path);
    }

    [RelayCommand]
    private void SkipWelcome()
    {
        ShowWelcome = false;
        _settings.HasCompletedFirstRun = true;
        _settings.Save();
        CurrentPage = "Overview";
        StatusMessage = "Ready — run Scan PC when you want an analysis";
    }

    [RelayCommand]
    private async Task ScanEntirePcAsync() => await RunScanAsync(ScanTargetKind.EntirePc, null);

    [RelayCommand]
    private async Task ScanDriveAsync(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return;
        await RunScanAsync(ScanTargetKind.Drive, root);
    }

    [RelayCommand]
    private async Task ScanFolderAsync()
    {
        var path = PromptForFolder();
        if (string.IsNullOrWhiteSpace(path)) return;
        await RunScanAsync(ScanTargetKind.Folder, path);
    }

    /// <summary>Used by --demo-scan for store screenshots and demos.</summary>
    public Task ScanFolderPathAsync(string path) => RunScanAsync(ScanTargetKind.Folder, path);

    [RelayCommand]
    private void CancelScan()
    {
        _scanCts?.Cancel();
        StatusMessage = "Cancelling scan…";
    }

    [RelayCommand]
    private void SelectAllSafe()
    {
        foreach (var item in CleanupItems.Where(i => i.Risk == RiskLevel.Safe))
            item.IsSelected = true;
        OnPropertyChanged(nameof(CleanupItems));
        RefreshCleanupTotals();
    }

    [RelayCommand]
    private void ClearCleanupSelection()
    {
        foreach (var item in CleanupItems)
            item.IsSelected = false;
        RefreshCleanupTotals();
    }

    [RelayCommand]
    private void ToggleCleanupSelection(CleanupSuggestion? item)
    {
        if (item == null) return;
        item.IsSelected = !item.IsSelected;
        RefreshCleanupTotals();
    }

    [RelayCommand]
    private void ConfirmCleanup()
    {
        var selected = CleanupItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select at least one item to clean up.", "SpaceLens", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var protectedSelected = selected.Where(s => s.Risk == RiskLevel.Protected).ToList();
        if (protectedSelected.Count > 0)
        {
            MessageBox.Show("Protected items cannot be deleted through SpaceLens.", "Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var total = selected.Sum(s => s.SizeBytes);
        var confirm = MessageBox.Show(
            $"You are about to move {selected.Count} item(s) to the Recycle Bin.\n\nTotal: {SizeFormatter.Format(total)}\n\nContinue?",
            "Confirm cleanup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes) return;

        var result = CleanupService.Execute(selected, permanentDelete: false);
        LastCleanupSummary = $"{SizeFormatter.Format(result.BytesMoved)} moved to Recycle Bin ({result.SuccessCount} ok, {result.FailCount} failed). You can restore items from the Windows Recycle Bin.";
        StatusMessage = LastCleanupSummary;

        foreach (var item in selected.Where(s => result.Errors.All(e => !e.StartsWith(s.FullPath))).ToList())
            CleanupItems.Remove(item);

        RefreshCleanupTotals();

        if (result.Errors.Count > 0)
            MessageBox.Show(string.Join("\n", result.Errors.Take(10)), "Some items failed", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    [RelayCommand]
    private void OpenRecycleBin() => CleanupService.OpenRecycleBin();

    [RelayCommand]
    private void OpenPath()
    {
        if (!string.IsNullOrWhiteSpace(DetailPath))
            CleanupService.OpenInExplorer(DetailPath);
    }

    [RelayCommand]
    private void OpenFile()
    {
        if (!string.IsNullOrWhiteSpace(DetailPath) && File.Exists(DetailPath))
            CleanupService.OpenFile(DetailPath);
    }

    [RelayCommand]
    private void CopyPath()
    {
        if (!string.IsNullOrWhiteSpace(DetailPath))
        {
            Clipboard.SetText(DetailPath);
            StatusMessage = "Path copied";
        }
    }

    [RelayCommand]
    private void MoveDetailToRecycleBin()
    {
        if (string.IsNullOrWhiteSpace(DetailPath)) return;
        if (DetailRisk.Equals("Protected", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Protected items cannot be deleted.", "Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Move to Recycle Bin?\n\n{DetailPath}",
            "Confirm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;

        var suggestion = new CleanupSuggestion
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = Path.GetFileName(DetailPath),
            FullPath = DetailPath,
            SizeBytes = 0,
            Category = FileCategory.Other,
            Risk = Enum.TryParse<RiskLevel>(DetailRisk, out var r) ? r : RiskLevel.Review,
            Reason = DetailReason,
            RecommendedAction = "Move to Recycle Bin",
            IsDirectory = Directory.Exists(DetailPath)
        };
        var result = CleanupService.Execute(new[] { suggestion }, permanentDelete: false);
        StatusMessage = result.FailCount == 0
            ? "Moved to Recycle Bin"
            : string.Join("; ", result.Errors);
    }

    [RelayCommand]
    private void DeleteDetailPermanently()
    {
        if (string.IsNullOrWhiteSpace(DetailPath)) return;
        if (DetailRisk.Equals("Protected", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Protected items cannot be deleted.", "Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"PERMANENTLY delete this item? This cannot be undone from SpaceLens.\n\n{DetailPath}",
            "Permanent delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;

        var confirm2 = MessageBox.Show(
            "Are you absolutely sure? Prefer Recycle Bin when possible.",
            "Final confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirm2 != MessageBoxResult.Yes) return;

        var suggestion = new CleanupSuggestion
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = Path.GetFileName(DetailPath),
            FullPath = DetailPath,
            SizeBytes = 0,
            Category = FileCategory.Other,
            Risk = RiskLevel.Review,
            Reason = DetailReason,
            RecommendedAction = "Permanent delete",
            IsDirectory = Directory.Exists(DetailPath)
        };
        var result = CleanupService.Execute(new[] { suggestion }, permanentDelete: true);
        StatusMessage = result.FailCount == 0 ? "Permanently deleted" : string.Join("; ", result.Errors);
    }

    [RelayCommand]
    private void ShowFileDetails(FileEntry? entry)
    {
        if (entry == null) return;
        HasDetail = true;
        DetailTitle = entry.Name;
        DetailPath = entry.FullPath;
        DetailSize = SizeFormatter.Format(entry.SizeBytes);
        DetailCategory = entry.Category.ToString();
        DetailRisk = entry.Risk.ToString();
        DetailReason = entry.Reason;
        DetailAction = ExplanationEngine.RecommendedAction(entry.Risk, false);
        DetailMeta = $"Modified {entry.ModifiedUtc.ToLocalTime():g} · Created {entry.CreatedUtc.ToLocalTime():g}" +
                     (string.IsNullOrEmpty(entry.Hash) ? "" : $" · Hash {entry.Hash[..Math.Min(12, entry.Hash.Length)]}…");
        ExplanationText = ExplanationEngine.ExplainWhyLarge(entry.FullPath);
    }

    [RelayCommand]
    private void ShowFolderDetails(FolderNode? folder)
    {
        if (folder == null) return;
        HasDetail = true;
        DetailTitle = folder.Name;
        DetailPath = folder.FullPath;
        DetailSize = SizeFormatter.Format(folder.SizeBytes);
        DetailCategory = folder.Category.ToString();
        DetailRisk = folder.Risk.ToString();
        DetailReason = folder.Reason;
        DetailAction = ExplanationEngine.RecommendedAction(folder.Risk, false);
        DetailMeta = $"Files {folder.FileCount:N0} · Modified {folder.LastModifiedUtc.ToLocalTime():g}";
        ExplanationText = ExplanationEngine.ExplainWhyLarge(folder.FullPath);
    }

    [RelayCommand]
    private void ShowCleanupDetails(CleanupSuggestion? item)
    {
        if (item == null) return;
        HasDetail = true;
        DetailTitle = item.DisplayName;
        DetailPath = item.FullPath;
        DetailSize = SizeFormatter.Format(item.SizeBytes);
        DetailCategory = item.Category.ToString();
        DetailRisk = item.Risk.ToString();
        DetailReason = item.Reason;
        DetailAction = item.RecommendedAction;
        DetailMeta = item.CanRegenerate ? "Can usually be regenerated" : "May not be regeneratable";
        ExplanationText = item.Reason;
    }

    [RelayCommand]
    private void RunSearch()
    {
        SearchResults.Clear();
        HasSearched = false;
        if (Result == null)
        {
            StatusMessage = "Run a scan before searching";
            return;
        }
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            StatusMessage = "Enter a name, path fragment, or extension like .mp4";
            NotifyListStates();
            return;
        }

        HasSearched = true;
        var q = SearchQuery.Trim();
        IEnumerable<FileEntry> source = Result.LargestFiles
            .Concat(Result.OldFiles)
            .Concat(Result.RecentFiles)
            .Concat(Result.Downloads?.Items ?? Enumerable.Empty<FileEntry>());

        if (q.StartsWith('.'))
            source = source.Where(f => f.Extension.Equals(q, StringComparison.OrdinalIgnoreCase));
        else
            source = source.Where(f =>
                f.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                f.FullPath.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                f.Category.ToString().Contains(q, StringComparison.OrdinalIgnoreCase));

        source = ApplyFilters(source);
        foreach (var item in source.DistinctBy(f => f.FullPath).OrderByDescending(f => f.SizeBytes).Take(300))
            SearchResults.Add(item);

        StatusMessage = SearchResults.Count == 0
            ? "No matches in the last scan index"
            : $"{SearchResults.Count} results · {SizeFormatter.Format(SearchResults.Sum(f => f.SizeBytes))}";
        CurrentPage = "Search";
        NotifyListStates();
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.SettingsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = AppSettings.SettingsDirectory,
                UseShellExecute = true
            });
            StatusMessage = "Opened local data folder";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "SpaceLens", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void ClearScanHistory()
    {
        var confirm = MessageBox.Show(
            "Clear all local scan history snapshots? This does not delete any of your files.",
            "Clear history",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;

        ScanHistoryStore.Clear();
        LoadHistory();
        StatusMessage = "Scan history cleared";
    }

    [RelayCommand]
    private void CloseDetail()
    {
        HasDetail = false;
    }

    [RelayCommand]
    private void ExportReport()
    {
        if (Result == null)
        {
            MessageBox.Show("Run a scan first.", "SpaceLens", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var warn = MessageBox.Show(
            "Exported reports include file paths that may contain personal information. Continue?",
            "Privacy warning",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (warn != MessageBoxResult.Yes) return;

        var dlg = new SaveFileDialog
        {
            Filter = "HTML report|*.html|JSON report|*.json|CSV report|*.csv",
            FileName = $"SpaceLens-Report-{DateTime.Now:yyyyMMdd-HHmm}"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (dlg.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                ReportExporter.ExportJson(Result, dlg.FileName);
            else if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                ReportExporter.ExportCsv(Result, dlg.FileName);
            else
                ReportExporter.ExportHtml(Result, dlg.FileName);
            StatusMessage = $"Report saved: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        if (_settings.DuplicateMinSizeBytes < 0)
            _settings.DuplicateMinSizeBytes = 0;
        if (_settings.Parallelism < 1)
            _settings.Parallelism = 1;
        if (_settings.Parallelism > 64)
            _settings.Parallelism = 64;

        _settings.Theme = ThemeName;
        _settings.Save();
        ApplyTheme();
        StatusMessage = "Settings saved";
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        SetTheme(IsDarkTheme ? "Light" : "Dark");
    }

    [RelayCommand]
    private void SetTheme(string theme)
    {
        if (string.IsNullOrWhiteSpace(theme)) return;
        ThemeName = theme.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        _settings.Theme = ThemeName;
        _settings.Save();
        ApplyTheme();
        StatusMessage = $"Theme: {ThemeName}";
    }

    [RelayCommand]
    private void RefreshDrives()
    {
        Drives.Clear();
        foreach (var d in DriveEnumerator.GetDrives().Where(d => d.IsReady))
            Drives.Add(d);
        PrimaryDrive = Drives.FirstOrDefault(d => d.RootPath.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                       ?? Drives.FirstOrDefault();
        UpdateDriveLabels();
    }

    [RelayCommand]
    private void RequestElevation()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;
        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" });
            Application.Current.Shutdown();
        }
        catch
        {
            StatusMessage = "Elevation cancelled";
        }
    }

    private async Task RunScanAsync(ScanTargetKind kind, string? path)
    {
        if (IsScanning) return;
        _scanCts = new CancellationTokenSource();
        IsScanning = true;
        StatusMessage = "Scanning…";
        CurrentPage = "Overview";

        var progress = new Progress<ScanProgress>(p =>
        {
            ScanPhase = p.Phase;
            ScanCurrentPath = p.CurrentPath;
            ScanFilesIndexed = p.FilesIndexed;
            ScanFoldersIndexed = p.FoldersIndexed;
            ScanPercent = p.PercentEstimate;
            ScanBytesLabel = SizeFormatter.Format(p.BytesIndexed);
        });

        try
        {
            var scanResult = await _scanner.ScanAsync(new ScanRequest
            {
                TargetKind = kind,
                SpecificPath = path,
                Settings = _settings
            }, progress, _scanCts.Token);

            ApplyResult(scanResult);
            if (!_settings.HasCompletedFirstRun)
            {
                _settings.HasCompletedFirstRun = true;
                _settings.Save();
            }
            StatusMessage = $"Scan complete · {scanResult.FileCount:N0} files · {scanResult.Duration.TotalSeconds:0}s";
            LoadHistory();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = "Scan failed";
            MessageBox.Show(ex.Message, "Scan error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsScanning = false;
            _scanCts = null;
        }
    }

    private void ApplyResult(ScanResult scanResult)
    {
        Result = scanResult;
        HasScanResult = true;
        HasSearched = false;
        SearchResults.Clear();
        Categories.Clear();
        foreach (var c in scanResult.Categories.Where(c => c.SizeBytes > 0).Take(10))
            Categories.Add(c);
        ChartCategories = Categories.ToList();

        LargestFiles.Clear();
        foreach (var f in ApplyFilters(scanResult.LargestFiles).Take(200))
            LargestFiles.Add(f);

        LargestFolders.Clear();
        foreach (var f in scanResult.LargestFolders.Take(100))
            LargestFolders.Add(f);

        CleanupItems.Clear();
        foreach (var c in scanResult.CleanupSuggestions)
            CleanupItems.Add(c);

        Duplicates.Clear();
        foreach (var d in scanResult.DuplicateGroups)
            Duplicates.Add(d);

        DevArtifacts.Clear();
        foreach (var d in scanResult.DevArtifacts)
            DevArtifacts.Add(d);

        Applications.Clear();
        foreach (var a in scanResult.Applications)
            Applications.Add(a);

        RefreshOldFiles();
        RefreshCleanupTotals();
        NotifyListStates();

        UsedLabel = SizeFormatter.Format(scanResult.UsedBytes);
        FreeLabel = SizeFormatter.Format(scanResult.FreeBytes);
        TotalLabel = SizeFormatter.Format(scanResult.TotalSizeBytes);
        UsedPercent = scanResult.TotalSizeBytes > 0
            ? 100.0 * scanResult.UsedBytes / scanResult.TotalSizeBytes
            : 0;
        PrimaryDrive = scanResult.Drives.FirstOrDefault(d => d.IsReady && d.RootPath.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                       ?? scanResult.Drives.FirstOrDefault(d => d.IsReady);

        CurrentPage = "Overview";

        if (scanResult.AccessErrors.Count > 0)
            StatusMessage = $"Scan complete with {scanResult.AccessErrors.Count} access restrictions";
    }

    private void RefreshCleanupTotals()
    {
        PotentialCleanupBytes = CleanupItems.Sum(c => c.SizeBytes);
        SafeCleanupBytes = CleanupItems.Where(c => c.Risk == RiskLevel.Safe).Sum(c => c.SizeBytes);
        ReviewCleanupBytes = CleanupItems.Where(c => c.Risk == RiskLevel.Review).Sum(c => c.SizeBytes);
        NotifyListStates();
    }

    private void NotifyListStates()
    {
        OnPropertyChanged(nameof(HasCleanupItems));
        OnPropertyChanged(nameof(ShowCleanupEmpty));
        OnPropertyChanged(nameof(HasDuplicateGroups));
        OnPropertyChanged(nameof(ShowDuplicatesEmpty));
        OnPropertyChanged(nameof(HasDevArtifactsList));
        OnPropertyChanged(nameof(ShowDevEmpty));
        OnPropertyChanged(nameof(HasApplicationsList));
        OnPropertyChanged(nameof(ShowApplicationsEmpty));
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(ShowSearchEmpty));
        OnPropertyChanged(nameof(HasOldFilesList));
        OnPropertyChanged(nameof(ShowOldFilesEmpty));
    }

    private void RefreshOldFiles()
    {
        OldFiles.Clear();
        if (Result == null) return;
        if (!int.TryParse(SelectedAgeFilter, out var days)) days = 180;
        var threshold = DateTime.UtcNow.AddDays(-days);
        foreach (var f in ApplyFilters(Result.OldFiles.Where(f => f.ModifiedUtc <= threshold)).Take(200))
            OldFiles.Add(f);
        NotifyListStates();
    }

    private IEnumerable<FileEntry> ApplyFilters(IEnumerable<FileEntry> source)
    {
        if (SelectedRiskFilter is not ("All" or null or ""))
            source = source.Where(f => f.Risk.ToString().Equals(SelectedRiskFilter, StringComparison.OrdinalIgnoreCase));

        if (SelectedCategoryFilter is not ("All" or null or ""))
            source = source.Where(f => f.Category.ToString().Equals(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase));

        source = SelectedSizeFilter switch
        {
            ">1GB" => source.Where(f => f.SizeBytes >= 1L * 1024 * 1024 * 1024),
            ">5GB" => source.Where(f => f.SizeBytes >= 5L * 1024 * 1024 * 1024),
            ">10GB" => source.Where(f => f.SizeBytes >= 10L * 1024 * 1024 * 1024),
            _ => source
        };
        return source;
    }

    private void LoadHistory()
    {
        History.Clear();
        HistoryPoints.Clear();
        var entries = ScanHistoryStore.Load();
        foreach (var e in entries)
            History.Add(e);

        HasHistory = entries.Count > 0;
        if (entries.Count == 0) return;
        var max = entries.Max(e => e.UsedBytes);
        if (max <= 0) max = 1;
        for (var i = 0; i < entries.Count; i++)
            HistoryPoints.Add(new System.Windows.Point(i, entries[i].UsedBytes));
    }

    private void UpdateDriveLabels()
    {
        if (PrimaryDrive == null) return;
        UsedLabel = SizeFormatter.Format(PrimaryDrive.UsedBytes);
        FreeLabel = SizeFormatter.Format(PrimaryDrive.FreeBytes);
        TotalLabel = SizeFormatter.Format(PrimaryDrive.TotalBytes);
        UsedPercent = PrimaryDrive.TotalBytes > 0
            ? 100.0 * PrimaryDrive.UsedBytes / PrimaryDrive.TotalBytes
            : 0;
    }

    private void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null) return;
        var dict = new ResourceDictionary
        {
            Source = new Uri(ThemeName.Equals("Light", StringComparison.OrdinalIgnoreCase)
                ? "Themes/Light.xaml"
                : "Themes/Dark.xaml", UriKind.Relative)
        };

        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/"));
        if (existing != null)
            app.Resources.MergedDictionaries.Remove(existing);
        app.Resources.MergedDictionaries.Insert(0, dict);
        ChartTheme.NotifyChanged();
    }

    private static string? PromptForFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a folder to analyze"
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
