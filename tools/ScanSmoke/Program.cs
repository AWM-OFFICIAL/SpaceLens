using SpaceLens.Core.Classification;
using SpaceLens.Core.Models;
using SpaceLens.Core.Settings;
using SpaceLens.Scanner;

var root = args.Length > 0 ? args[0] : Path.Combine("c:\\Users\\MY-PC\\Music\\SpaceLens", "src");
Console.WriteLine($"Scanning: {root}");
var sw = System.Diagnostics.Stopwatch.StartNew();
var scanner = new StorageScanner();
var progress = new Progress<ScanProgress>(p =>
{
    if (p.FilesIndexed % 500 == 0 || p.Phase.Contains("complete", StringComparison.OrdinalIgnoreCase))
        Console.WriteLine($"{p.Phase} | files={p.FilesIndexed:N0} | {p.CurrentPath}");
});

var result = await scanner.ScanAsync(new ScanRequest
{
    TargetKind = ScanTargetKind.Folder,
    SpecificPath = root,
    Settings = new AppSettings { EnableScanHistory = false, Parallelism = 4, DuplicateMinSizeBytes = 64 * 1024 }
}, progress);

sw.Stop();
Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:0.0}s");
Console.WriteLine($"Files={result.FileCount:N0} Folders={result.FolderCount:N0}");
Console.WriteLine($"Access errors={result.AccessErrors.Count} Junctions skipped={result.SkippedJunctions.Count}");
Console.WriteLine("Top categories:");
foreach (var c in result.Categories.Where(c => c.SizeBytes > 0).Take(8))
    Console.WriteLine($"  {c.Category}: {c.SizeBytes / (1024.0 * 1024):0.0} MB ({c.FileCount} files)");
Console.WriteLine("Largest folders:");
foreach (var f in result.LargestFolders.Take(5))
    Console.WriteLine($"  [{f.Risk}] {f.Name}: {f.SizeBytes / (1024.0 * 1024):0.0} MB");
Console.WriteLine($"Cleanup suggestions: {result.CleanupSuggestions.Count} (safe={result.CleanupSuggestions.Count(s => s.Risk == RiskLevel.Safe)}, review={result.CleanupSuggestions.Count(s => s.Risk == RiskLevel.Review)})");
Console.WriteLine($"Protected recommendations (should be 0): {result.CleanupSuggestions.Count(s => s.Risk == RiskLevel.Protected)}");
var systemSuggested = result.CleanupSuggestions.Any(s =>
    s.Risk == RiskLevel.Protected ||
    (ProtectedPathRules.IsProtectedPath(s.FullPath) && !s.FullPath.Contains("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)));
Console.WriteLine($"Dangerous protected path in cleanup? {systemSuggested}");
