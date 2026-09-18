using SpaceLens.Core.Models;

namespace SpaceLens.Core.Classification;

/// <summary>
/// Transparent rule-based risk engine. UNKNOWN is never treated as SAFE.
/// </summary>
public static class RiskEngine
{
    private static readonly HashSet<string> SafeDevArtifacts = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "__pycache__", ".next", "dist", "build", "coverage", "obj", "bin",
        "target", ".cache", ".parcel-cache", ".turbo"
    };

    private static readonly HashSet<string> SafeCacheFolderHints = new(StringComparer.OrdinalIgnoreCase)
    {
        "cache", "caches", "code cache", "gpucache", "shadercache", "crashdumps", "thumbcache"
    };

    public static (RiskLevel Risk, string Reason) Evaluate(string fullPath, bool isDirectory, FileCategory category, string? name = null)
    {
        name ??= Path.GetFileName(fullPath.TrimEnd('\\', '/'));

        if (ProtectedPathRules.IsProtectedPath(fullPath))
        {
            return (RiskLevel.Protected,
                "This appears to belong to Windows or a critical system/application location. Removing it manually could break software or Windows.");
        }

        if (ProtectedPathRules.IsUserDocumentLike(fullPath)
            && category is FileCategory.Documents or FileCategory.Images or FileCategory.Videos or FileCategory.Audio)
        {
            return (RiskLevel.Protected,
                "This looks like personal user content (documents or media). SpaceLens never recommends deleting these automatically.");
        }

        // Known regeneratable development artifacts
        if (isDirectory && SafeDevArtifacts.Contains(name))
        {
            return (RiskLevel.Review,
                $"{name} usually contains regeneratable development artifacts. Deleting it typically requires reinstalling dependencies or rebuilding.");
        }

        if (FileClassifier.IsUnderTempLocation(fullPath))
        {
            // Known temp roots: still REVIEW for unknown nested content, SAFE for obvious temp
            if (category is FileCategory.Temporary or FileCategory.Cache or FileCategory.Logs)
            {
                return (RiskLevel.Safe,
                    "Located in a known temporary directory. These files are typically recreated by Windows or applications.");
            }

            return (RiskLevel.Review,
                "Located under a temporary-looking path, but the content is not confidently identified as disposable.");
        }

        if (FileClassifier.IsCachePath(fullPath) || SafeCacheFolderHints.Contains(name))
        {
            return (RiskLevel.Safe,
                "Identified as application or browser cache that can usually be regenerated.");
        }

        if (category == FileCategory.Logs)
        {
            return (RiskLevel.Review,
                "Log files are often safe to remove but may be useful for troubleshooting. Review before deleting.");
        }

        if (FileClassifier.IsUnderDownloads(fullPath))
        {
            return (RiskLevel.Review,
                "Downloads may include installers, archives, or files you still need. Review carefully before deleting.");
        }

        if (category is FileCategory.Archives or FileCategory.Installers)
        {
            return (RiskLevel.Review,
                "Archives and installers can often be removed after installation, but may be needed for reinstalls or backups.");
        }

        if (category == FileCategory.System || FileClassifier.IsApplicationPath(fullPath))
        {
            return (RiskLevel.Protected,
                "This path appears related to installed applications or system software. Do not delete unless you intend to remove the application.");
        }

        if (category == FileCategory.Development)
        {
            return (RiskLevel.Review,
                "Development-related storage. Some items regenerate; others (source code, configs) must be kept.");
        }

        if (category == FileCategory.Games)
        {
            return (RiskLevel.Review,
                "Game installation data. Removing it may require re-downloading the game.");
        }

        // Default: never SAFE
        return (RiskLevel.Unknown,
            "SpaceLens could not confidently classify this item. Unknown items are never treated as safe to delete.");
    }

    public static bool CanAppearAsCleanupRecommendation(RiskLevel risk) =>
        risk is RiskLevel.Safe or RiskLevel.Review;
}
