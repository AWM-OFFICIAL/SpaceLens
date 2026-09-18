namespace SpaceLens.Core.Models;

public sealed class DriveSummary
{
    public required string Name { get; init; }
    public required string RootPath { get; init; }
    public string VolumeLabel { get; init; } = string.Empty;
    public DriveTypeKind DriveType { get; init; }
    public long TotalBytes { get; init; }
    public long UsedBytes { get; init; }
    public long FreeBytes { get; init; }
    public bool IsReady { get; init; }
}

public enum DriveTypeKind
{
    Unknown,
    Removable,
    Fixed,
    Network,
    CdRom,
    Ram
}
