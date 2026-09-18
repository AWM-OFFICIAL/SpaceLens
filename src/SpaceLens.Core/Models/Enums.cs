namespace SpaceLens.Core.Models;

public enum RiskLevel
{
    Safe,
    Review,
    Protected,
    Unknown
}

public enum FileCategory
{
    Images,
    Videos,
    Audio,
    Documents,
    Archives,
    Installers,
    Development,
    Games,
    System,
    Temporary,
    Cache,
    Logs,
    Downloads,
    Applications,
    Other
}

public enum ScanTargetKind
{
    EntirePc,
    Drive,
    Folder
}

public enum CleanupActionKind
{
    None,
    MoveToRecycleBin,
    PermanentDelete
}
