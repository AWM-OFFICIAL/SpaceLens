namespace SpaceLens.Core.Models;

public sealed class DuplicateGroup
{
    public required string GroupId { get; init; }
    public long FileSizeBytes { get; init; }
    public required string Hash { get; init; }
    public List<FileEntry> Files { get; } = new();
    public long RecoverableBytes => Files.Count <= 1 ? 0 : FileSizeBytes * (Files.Count - 1);
}
