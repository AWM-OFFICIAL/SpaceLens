namespace SpaceLens.Core.Models;

public sealed class CategoryBreakdown
{
    public FileCategory Category { get; init; }
    public long SizeBytes { get; set; }
    public long FileCount { get; set; }
    public double PercentOfTotal { get; set; }
}
