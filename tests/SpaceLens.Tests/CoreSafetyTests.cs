using SpaceLens.Core.Classification;
using SpaceLens.Core.Cleanup;
using SpaceLens.Core.Explanations;
using SpaceLens.Core.Models;
using SpaceLens.Core.Utilities;
using SpaceLens.Scanner;

namespace SpaceLens.Tests;

public class RiskEngineTests
{
    [Theory]
    [InlineData(@"C:\Windows\System32\kernel32.dll")]
    [InlineData(@"C:\Windows\System32\drivers\etc\hosts")]
    [InlineData(@"C:\Program Files\Common Files\test.dll")]
    public void System_paths_are_protected(string path)
    {
        Assert.True(ProtectedPathRules.IsProtectedPath(path));
        var (risk, _) = RiskEngine.Evaluate(path, false, FileCategory.System);
        Assert.Equal(RiskLevel.Protected, risk);
        Assert.False(RiskEngine.CanAppearAsCleanupRecommendation(risk));
    }

    [Fact]
    public void Unknown_is_never_safe()
    {
        var path = Path.Combine(Path.GetTempPath(), "spacelens-unknown-xyz.dat");
        var (risk, _) = RiskEngine.Evaluate(path, false, FileCategory.Other);
        Assert.NotEqual(RiskLevel.Safe, risk);
    }

    [Fact]
    public void Temp_cache_can_be_safe()
    {
        var path = Path.Combine(Path.GetTempPath(), "cache", "tmp.dat");
        var (risk, _) = RiskEngine.Evaluate(path, false, FileCategory.Temporary);
        Assert.Equal(RiskLevel.Safe, risk);
    }

    [Fact]
    public void Downloads_are_review()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "setup.exe");
        var (risk, _) = RiskEngine.Evaluate(downloads, false, FileCategory.Installers);
        Assert.Equal(RiskLevel.Review, risk);
    }

    [Fact]
    public void User_documents_are_protected_from_auto_cleanup()
    {
        var docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "thesis.docx");
        var (risk, _) = RiskEngine.Evaluate(docs, false, FileCategory.Documents);
        Assert.Equal(RiskLevel.Protected, risk);
    }
}

public class ClassifierTests
{
    [Theory]
    [InlineData(".mp4", FileCategory.Videos)]
    [InlineData(".jpg", FileCategory.Images)]
    [InlineData(".zip", FileCategory.Archives)]
    [InlineData(".pdf", FileCategory.Documents)]
    [InlineData(".msi", FileCategory.Installers)]
    [InlineData(".xyz", FileCategory.Other)]
    public void Extension_classification(string ext, FileCategory expected)
    {
        Assert.Equal(expected, FileClassifier.ClassifyByExtension(ext));
    }

    [Fact]
    public void Node_modules_is_development()
    {
        Assert.True(FileClassifier.IsDevelopmentFolderName("node_modules"));
        var cat = FileClassifier.ClassifyPath(@"D:\Projects\App\node_modules", true);
        Assert.Equal(FileCategory.Development, cat);
    }
}

public class CleanupEngineTests
{
    [Fact]
    public void Protected_folders_never_become_suggestions()
    {
        var folders = new List<FolderNode>
        {
            new()
            {
                FullPath = @"C:\Windows\System32",
                Name = "System32",
                SizeBytes = 5_000_000_000,
                Category = FileCategory.System,
                Risk = RiskLevel.Protected,
                Reason = "system"
            }
        };

        var suggestions = CleanupRecommendationEngine.Build(folders, Array.Empty<FileEntry>(), Array.Empty<DevArtifact>());
        Assert.DoesNotContain(suggestions, s => s.FullPath.Contains("System32", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Regeneratable_dev_artifacts_are_review()
    {
        var arts = new[]
        {
            new DevArtifact
            {
                ProjectName = "Demo",
                ArtifactName = "node_modules",
                FullPath = @"D:\Projects\Demo\node_modules",
                SizeBytes = 1_000_000_000,
                CanRegenerate = true,
                Risk = RiskLevel.Review,
                Recommendation = "review",
                Reason = "deps"
            }
        };
        var suggestions = CleanupRecommendationEngine.Build(Array.Empty<FolderNode>(), Array.Empty<FileEntry>(), arts);
        Assert.Single(suggestions);
        Assert.Equal(RiskLevel.Review, suggestions[0].Risk);
    }
}

public class SizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1048576, "1 MB")]
    public void Formats_sizes(long bytes, string expected)
    {
        Assert.Equal(expected, SizeFormatter.Format(bytes));
    }
}

public class ExplanationTests
{
    [Fact]
    public void Explains_node_modules()
    {
        var text = ExplanationEngine.ExplainWhyLarge(@"D:\x\node_modules");
        Assert.Contains("dependencies", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Explains_protected()
    {
        var text = ExplanationEngine.ExplainWhyNotDelete(RiskLevel.Protected);
        Assert.Contains("Windows", text, StringComparison.OrdinalIgnoreCase);
    }
}

public class DuplicateDetectorTests
{
    [Fact]
    public void Quick_hash_is_stable_for_same_content()
    {
        var dir = Path.Combine(Path.GetTempPath(), "spacelens-dup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var a = Path.Combine(dir, "a.bin");
            var b = Path.Combine(dir, "b.bin");
            var payload = Enumerable.Range(0, 4096).Select(i => (byte)(i % 256)).ToArray();
            File.WriteAllBytes(a, payload);
            File.WriteAllBytes(b, payload);
            Assert.Equal(DuplicateDetector.ComputeQuickHash(a), DuplicateDetector.ComputeQuickHash(b));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

public class FolderAggregationTests
{
    [Fact]
        public async Task Scanner_aggregates_folder_sizes_for_small_tree()
        {
        var root = Path.Combine(Path.GetTempPath(), "spacelens-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        File.WriteAllBytes(Path.Combine(root, "a.txt"), new byte[2048]);
        File.WriteAllBytes(Path.Combine(root, "sub", "b.txt"), new byte[4096]);

        try
        {
            var scanner = new StorageScanner();
            var result = await scanner.ScanAsync(new ScanRequest
            {
                TargetKind = ScanTargetKind.Folder,
                SpecificPath = root,
                Settings = new Core.Settings.AppSettings { EnableScanHistory = false, Parallelism = 2 }
            });

            Assert.True(result.FileCount >= 2);
            Assert.True(result.LargestFolders.Any(f =>
                f.FullPath.TrimEnd('\\').Equals(root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                || f.FullPath.Contains("spacelens-scan-", StringComparison.OrdinalIgnoreCase)));
            var folder = result.LargestFolders.First(f =>
                f.FullPath.TrimEnd('\\').Equals(root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                || f.Name.StartsWith("spacelens-scan-", StringComparison.OrdinalIgnoreCase));
            Assert.True(folder.SizeBytes >= 6144);
        }
        finally
        {
            Directory.Delete(root, true);
        }
        }
}

public class CleanupServiceSafetyTests
{
    [Fact]
    public void Cleanup_blocks_protected_items()
    {
        var suggestion = new CleanupSuggestion
        {
            Id = "1",
            DisplayName = "kernel",
            FullPath = @"C:\Windows\System32\kernel32.dll",
            SizeBytes = 1,
            Category = FileCategory.System,
            Risk = RiskLevel.Protected,
            Reason = "protected",
            RecommendedAction = "keep",
            IsDirectory = false
        };

        var result = CleanupService.Execute(new[] { suggestion }, permanentDelete: false);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailCount);
        Assert.Contains(result.Errors, e => e.Contains("Blocked", StringComparison.OrdinalIgnoreCase));
    }
}
