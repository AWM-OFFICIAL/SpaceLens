namespace SpaceLens.Core.Models;

public sealed class CleanupSuggestion
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string FullPath { get; init; }
    public long SizeBytes { get; init; }
    public FileCategory Category { get; init; }
    public RiskLevel Risk { get; init; }
    public required string Reason { get; init; }
    public required string RecommendedAction { get; init; }
    public bool CanRegenerate { get; init; }
    public bool IsDirectory { get; init; }
    public bool IsSelected { get; set; }
}
