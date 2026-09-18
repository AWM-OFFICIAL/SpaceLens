using SpaceLens.Core.Classification;
using SpaceLens.Core.Models;

namespace SpaceLens.Core.Explanations;

/// <summary>
/// Human-language explanations derived from classification rules — not fabricated certainty.
/// </summary>
public static class ExplanationEngine
{
    public static string ExplainCategory(FileCategory category) => category switch
    {
        FileCategory.Images => "Image files such as photos and graphics.",
        FileCategory.Videos => "Video files. These are often among the largest items on a drive.",
        FileCategory.Audio => "Audio and music files.",
        FileCategory.Documents => "Documents and office files. Treat these as personal data that needs review.",
        FileCategory.Archives => "Compressed archives and disk images.",
        FileCategory.Installers => "Installer packages. Often removable after software is installed.",
        FileCategory.Development => "Development tools, dependencies, build outputs, and project caches.",
        FileCategory.Games => "Game installations and related game data.",
        FileCategory.System => "Windows or system-related files. Protected from cleanup recommendations.",
        FileCategory.Temporary => "Temporary files created by Windows or applications.",
        FileCategory.Cache => "Cached data that applications can usually regenerate.",
        FileCategory.Logs => "Log files used for diagnostics.",
        FileCategory.Downloads => "Files in your Downloads folder.",
        FileCategory.Applications => "Installed application files.",
        _ => "Unclassified or mixed content. SpaceLens does not pretend to know what these are."
    };

    public static string ExplainWhyLarge(string pathOrName)
    {
        var name = Path.GetFileName(pathOrName.TrimEnd('\\', '/'));
        var lower = pathOrName.Replace('/', '\\');

        if (name.Equals("AppData", StringComparison.OrdinalIgnoreCase) || lower.Contains(@"\AppData\", StringComparison.OrdinalIgnoreCase))
        {
            return "AppData contains application caches, settings, local databases, temporary files, and other user-specific data. Some of these files can be safely removed, while others are required by applications.";
        }

        if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
        {
            return "node_modules contains installed JavaScript dependencies. It can usually be recreated with your package manager, but deleting it means dependencies will need to be installed again.";
        }

        if (name.Equals("Windows", StringComparison.OrdinalIgnoreCase))
        {
            return "The Windows folder contains the operating system. Removing files here can prevent Windows from starting or cause serious instability. SpaceLens protects this location.";
        }

        if (name.Equals("Downloads", StringComparison.OrdinalIgnoreCase))
        {
            return "Downloads accumulate installers, archives, media, and one-off files over time. Many can be removed after use, but some may still be needed — review before deleting.";
        }

        if (name.Equals(".git", StringComparison.OrdinalIgnoreCase))
        {
            return ".git stores full version history for a repository. It can be large, but deleting it permanently removes history and remote tracking unless you re-clone.";
        }

        if (name.Equals("__pycache__", StringComparison.OrdinalIgnoreCase))
        {
            return "__pycache__ stores compiled Python bytecode. It is regeneratable and usually safe to remove.";
        }

        return ExplainCategory(FileClassifier.ClassifyPath(pathOrName, Directory.Exists(pathOrName), Path.GetExtension(pathOrName)));
    }

    public static string ExplainWhyNotDelete(RiskLevel risk) => risk switch
    {
        RiskLevel.Protected => "This appears to belong to Windows or a system/application-critical location. Removing it manually could cause software or Windows to stop working.",
        RiskLevel.Unknown => "SpaceLens could not confidently classify this item. Unknown items should never be deleted without careful manual review.",
        RiskLevel.Review => "This item may be removable, but it is not guaranteed safe. Review the reason and confirm you no longer need it.",
        RiskLevel.Safe => "This is classified as known temporary, cache, or regeneratable data. Still confirm before deleting.",
        _ => "Exercise caution."
    };

    public static string RecommendedAction(RiskLevel risk, bool canRegenerate) => risk switch
    {
        RiskLevel.Safe => "Move to Recycle Bin if you want to free space. Applications can usually recreate this data.",
        RiskLevel.Review when canRegenerate => "Review, then move to Recycle Bin only if you are comfortable regenerating it (for example reinstalling dependencies).",
        RiskLevel.Review => "Needs review. Do not delete unless you recognize the file and no longer need it.",
        RiskLevel.Protected => "Keep. Do not delete from SpaceLens.",
        _ => "Leave alone until you can identify it."
    };
}
