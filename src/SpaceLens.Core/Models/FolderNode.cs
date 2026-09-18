namespace SpaceLens.Core.Models;

public sealed class FolderNode
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public long SizeBytes { get; set; }
    public long FileCount { get; set; }
    public long FolderCount { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public FileCategory Category { get; set; } = FileCategory.Other;
    public RiskLevel Risk { get; set; } = RiskLevel.Unknown;
    public string Reason { get; set; } = string.Empty;
    public double PercentOfParent { get; set; }
    public Dictionary<string, FolderNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
}
