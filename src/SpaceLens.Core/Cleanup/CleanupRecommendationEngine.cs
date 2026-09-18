using SpaceLens.Core.Classification;
using SpaceLens.Core.Explanations;
using SpaceLens.Core.Models;

namespace SpaceLens.Core.Cleanup;

/// <summary>
/// Builds cleanup suggestions. Protected and Unknown never become one-click SAFE deletes.
/// </summary>
public static class CleanupRecommendationEngine
{
    private static readonly HashSet<string> RegeneratableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "__pycache__", ".next", "dist", "build", "coverage", "obj", "target",
        ".cache", ".parcel-cache", ".turbo", "bin"
    };

    public static List<CleanupSuggestion> Build(IEnumerable<FolderNode> folders, IEnumerable<FileEntry> files, IEnumerable<DevArtifact> devArtifacts)
    {
        var suggestions = new Dictionary<string, CleanupSuggestion>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders)
        {
            TryAddFolder(suggestions, folder);
            foreach (var child in Flatten(folder))
                TryAddFolder(suggestions, child);
        }

        foreach (var file in files)
            TryAddFile(suggestions, file);

        foreach (var art in devArtifacts)
        {
            if (!RiskEngine.CanAppearAsCleanupRecommendation(art.Risk))
                continue;

            suggestions[art.FullPath] = new CleanupSuggestion
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = $"{art.ProjectName} / {art.ArtifactName}",
                FullPath = art.FullPath,
                SizeBytes = art.SizeBytes,
                Category = FileCategory.Development,
                Risk = art.Risk,
                Reason = art.Reason,
                RecommendedAction = art.Recommendation,
                CanRegenerate = art.CanRegenerate,
                IsDirectory = true
            };
        }

        return suggestions.Values
            .Where(s => s.SizeBytes > 0)
            .OrderByDescending(s => s.Risk == RiskLevel.Safe)
            .ThenByDescending(s => s.SizeBytes)
            .ToList();
    }

    private static void TryAddFolder(Dictionary<string, CleanupSuggestion> map, FolderNode folder)
    {
        if (map.ContainsKey(folder.FullPath))
            return;

        var (risk, reason) = RiskEngine.Evaluate(folder.FullPath, true, folder.Category, folder.Name);
        if (!RiskEngine.CanAppearAsCleanupRecommendation(risk))
            return;

        // Only recommend known patterns at folder level to avoid broad deletes
        var name = folder.Name;
        var isKnownPattern = RegeneratableNames.Contains(name)
                             || FileClassifier.LooksLikeTempFolder(name)
                             || FileClassifier.IsCachePath(folder.FullPath)
                             || FileClassifier.IsUnderTempLocation(folder.FullPath);

        if (!isKnownPattern && risk != RiskLevel.Safe)
            return;

        var canRegen = RegeneratableNames.Contains(name);
        // Dev artifacts default to REVIEW even when regeneratable
        if (canRegen && risk == RiskLevel.Safe)
            risk = RiskLevel.Review;

        map[folder.FullPath] = new CleanupSuggestion
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = name,
            FullPath = folder.FullPath,
            SizeBytes = folder.SizeBytes,
            Category = folder.Category,
            Risk = risk,
            Reason = string.IsNullOrWhiteSpace(folder.Reason) ? reason : folder.Reason,
            RecommendedAction = ExplanationEngine.RecommendedAction(risk, canRegen),
            CanRegenerate = canRegen,
            IsDirectory = true
        };
    }

    private static void TryAddFile(Dictionary<string, CleanupSuggestion> map, FileEntry file)
    {
        if (map.ContainsKey(file.FullPath))
            return;

        var (risk, reason) = RiskEngine.Evaluate(file.FullPath, false, file.Category, file.Name);
        if (!RiskEngine.CanAppearAsCleanupRecommendation(risk))
            return;

        var isCandidate = risk == RiskLevel.Safe
                          || file.Category is FileCategory.Temporary or FileCategory.Cache or FileCategory.Logs
                          || (FileClassifier.IsUnderDownloads(file.FullPath)
                              && file.Category is FileCategory.Installers or FileCategory.Archives
                              && file.SizeBytes >= 50 * 1024 * 1024);

        if (!isCandidate)
            return;

        map[file.FullPath] = new CleanupSuggestion
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = file.Name,
            FullPath = file.FullPath,
            SizeBytes = file.SizeBytes,
            Category = file.Category,
            Risk = risk,
            Reason = string.IsNullOrWhiteSpace(file.Reason) ? reason : file.Reason,
            RecommendedAction = ExplanationEngine.RecommendedAction(risk, false),
            CanRegenerate = risk == RiskLevel.Safe,
            IsDirectory = false
        };
    }

    private static IEnumerable<FolderNode> Flatten(FolderNode node)
    {
        foreach (var child in node.Children.Values)
        {
            yield return child;
            foreach (var nested in Flatten(child))
                yield return nested;
        }
    }
}
