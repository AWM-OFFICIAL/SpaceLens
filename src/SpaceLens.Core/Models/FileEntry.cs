namespace SpaceLens.Core.Models;

public sealed class FileEntry
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required string DirectoryPath { get; init; }
    public long SizeBytes { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime ModifiedUtc { get; init; }
    public DateTime AccessedUtc { get; init; }
    public FileCategory Category { get; set; } = FileCategory.Other;
    public RiskLevel Risk { get; set; } = RiskLevel.Unknown;
    public string Reason { get; set; } = string.Empty;
    public string? Hash { get; set; }
    public bool IsDirectory { get; init; }
}
