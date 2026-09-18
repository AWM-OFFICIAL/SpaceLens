namespace SpaceLens.Core.Models;

public sealed class ScanResult
{
    public DateTime StartedUtc { get; set; }
    public DateTime CompletedUtc { get; set; }
    public TimeSpan Duration => CompletedUtc - StartedUtc;
    public List<string> Roots { get; } = new();
    public List<DriveSummary> Drives { get; } = new();
    public long TotalSizeBytes { get; set; }
    public long UsedBytes { get; set; }
    public long FreeBytes { get; set; }
    public long FileCount { get; set; }
    public long FolderCount { get; set; }
    public List<CategoryBreakdown> Categories { get; } = new();
    public List<FileEntry> LargestFiles { get; } = new();
    public List<FolderNode> LargestFolders { get; } = new();
    public FolderNode? RootTree { get; set; }
    public List<CleanupSuggestion> CleanupSuggestions { get; } = new();
    public List<DuplicateGroup> DuplicateGroups { get; } = new();
    public List<FileEntry> OldFiles { get; } = new();
    public List<FileEntry> RecentFiles { get; } = new();
    public List<DevArtifact> DevArtifacts { get; } = new();
    public List<AppStorageInfo> Applications { get; } = new();
    public DownloadsAnalysis? Downloads { get; set; }
    public List<string> AccessErrors { get; } = new();
    public List<string> SkippedJunctions { get; } = new();
}

public sealed class DevArtifact
{
    public required string ProjectName { get; init; }
    public required string ArtifactName { get; init; }
    public required string FullPath { get; init; }
    public long SizeBytes { get; init; }
    public bool CanRegenerate { get; init; }
    public RiskLevel Risk { get; init; }
    public required string Recommendation { get; init; }
    public required string Reason { get; init; }
}

public sealed class AppStorageInfo
{
    public required string ApplicationName { get; init; }
    public long InstalledSizeBytes { get; set; }
    public long CacheSizeBytes { get; set; }
    public long UserDataSizeBytes { get; set; }
    public long TotalBytes => InstalledSizeBytes + CacheSizeBytes + UserDataSizeBytes;
    public List<string> DetectedPaths { get; } = new();
}

public sealed class DownloadsAnalysis
{
    public required string Path { get; init; }
    public long TotalBytes { get; set; }
    public long PotentialCleanupBytes { get; set; }
    public long InstallerBytes { get; set; }
    public long ArchiveBytes { get; set; }
    public long VideoBytes { get; set; }
    public long DocumentBytes { get; set; }
    public long DuplicateBytes { get; set; }
    public long OldFileBytes { get; set; }
    public List<FileEntry> Items { get; } = new();
}
