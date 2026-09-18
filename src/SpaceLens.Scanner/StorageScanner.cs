using System.Collections.Concurrent;
using System.Security.Cryptography;
using SpaceLens.Core.Classification;
using SpaceLens.Core.Cleanup;
using SpaceLens.Core.Explanations;
using SpaceLens.Core.Models;
using SpaceLens.Core.Settings;

namespace SpaceLens.Scanner;

public sealed class ScanProgress
{
    public string Phase { get; init; } = "Starting";
    public string CurrentPath { get; init; } = string.Empty;
    public long FilesIndexed { get; init; }
    public long FoldersIndexed { get; init; }
    public long BytesIndexed { get; init; }
    public double PercentEstimate { get; init; }
}

public sealed class ScanRequest
{
    public ScanTargetKind TargetKind { get; init; } = ScanTargetKind.EntirePc;
    public string? SpecificPath { get; init; }
    public AppSettings Settings { get; init; } = new();
}

public sealed class StorageScanner
{
    private static readonly HashSet<string> DevArtifactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".next", "dist", "build", "coverage", ".cache", "target",
        "bin", "obj", "venv", ".venv", "__pycache__", ".gradle", ".turbo", ".parcel-cache"
    };

    public Task<ScanResult> ScanAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(request, progress, cancellationToken), cancellationToken);

    private ScanResult Scan(ScanRequest request, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        var result = new ScanResult { StartedUtc = DateTime.UtcNow };
        var settings = request.Settings;
        var roots = ResolveRoots(request);
        result.Roots.AddRange(roots);
        result.Drives.AddRange(DriveEnumerator.GetDrives());

        AggregateDriveTotals(request, roots, result);

        var state = new ScanState();
        var now = DateTime.UtcNow;
        var oldThreshold = now.AddDays(-180);

        Report(progress, "Preparing scan...", "", 0, 0, 0, 0);

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = 0,
            ReturnSpecialDirectories = false
        };

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = Math.Max(1, settings.Parallelism)
        };

        foreach (var root in roots)
        {
            ct.ThrowIfCancellationRequested();
            if (settings.ExcludedFolders.Any(ex => root.StartsWith(ex, StringComparison.OrdinalIgnoreCase)))
                continue;

            Report(progress, "Scanning...", root, state.FilesIndexed, state.FoldersIndexed, state.BytesIndexed, 5);

            try
            {
                WalkDirectory(root, root, options, settings, parallelOptions, state, progress, now, oldThreshold, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                state.AccessErrors.Add($"{root}: {ex.Message}");
            }
        }

        Report(progress, "Analyzing folders...", "", state.FilesIndexed, state.FoldersIndexed, state.BytesIndexed, 70);

        result.FileCount = Interlocked.Read(ref state.FilesIndexed);
        result.FolderCount = Interlocked.Read(ref state.FoldersIndexed);
        result.AccessErrors.AddRange(state.AccessErrors);
        result.SkippedJunctions.AddRange(state.SkippedJunctions.Take(200));

        BuildCategories(result, state);
        result.LargestFiles.AddRange(
            state.LargestFiles.OrderByDescending(x => x.Size).Take(settings.MaxLargestFiles).Select(x => x.Entry));

        Report(progress, "Building folder map...", "", result.FileCount, result.FolderCount, state.BytesIndexed, 80);
        result.RootTree = BuildFolderTree(state, roots);
        result.LargestFolders.AddRange(BuildLargestFolders(state, settings.MaxLargestFolders));

        result.OldFiles.AddRange(state.OldCandidates.OrderBy(f => f.ModifiedUtc).Take(200));
        result.RecentFiles.AddRange(state.RecentCandidates.OrderByDescending(f => f.ModifiedUtc).Take(200));

        Report(progress, "Analyzing Downloads...", "", result.FileCount, result.FolderCount, state.BytesIndexed, 85);
        result.Downloads = AnalyzeDownloads(state.DownloadsFiles);
        result.DevArtifacts.AddRange(state.DevArtifacts.OrderByDescending(d => d.SizeBytes).Take(200));
        result.Applications.AddRange(DetectApplications(state.FolderSizes));

        Report(progress, "Finding duplicates...", "", result.FileCount, result.FolderCount, state.BytesIndexed, 90);
        result.DuplicateGroups.AddRange(
            DuplicateDetector.FindDuplicates(state.SizeBuckets, settings.DuplicateMinSizeBytes, ct, maxGroups: 100));

        Report(progress, "Preparing cleanup suggestions...", "", result.FileCount, result.FolderCount, state.BytesIndexed, 95);
        result.CleanupSuggestions.AddRange(
            CleanupRecommendationEngine.Build(
                result.LargestFolders,
                result.LargestFiles.Concat(state.DownloadsFiles).Take(500),
                result.DevArtifacts));
        TryAddRecycleBinSuggestion(result);

        result.CompletedUtc = DateTime.UtcNow;
        Report(progress, "Scan complete", "", result.FileCount, result.FolderCount, state.BytesIndexed, 100);
        SaveHistory(settings, result);
        return result;
    }

    private static void AggregateDriveTotals(ScanRequest request, List<string> roots, ScanResult result)
    {
        long used = 0, free = 0, total = 0;
        foreach (var d in result.Drives.Where(d => d.IsReady))
        {
            var relevant = request.TargetKind == ScanTargetKind.EntirePc
                           || roots.Any(r => r.StartsWith(d.RootPath, StringComparison.OrdinalIgnoreCase));
            if (!relevant) continue;
            used += d.UsedBytes;
            free += d.FreeBytes;
            total += d.TotalBytes;
        }

        if (request.TargetKind == ScanTargetKind.Folder && !string.IsNullOrEmpty(request.SpecificPath))
        {
            try
            {
                var root = Path.GetPathRoot(request.SpecificPath);
                var drive = result.Drives.FirstOrDefault(d =>
                    d.RootPath.Equals(root, StringComparison.OrdinalIgnoreCase));
                if (drive != null)
                {
                    total = drive.TotalBytes;
                    free = drive.FreeBytes;
                    used = drive.UsedBytes;
                }
            }
            catch { /* ignore */ }
        }

        result.TotalSizeBytes = total;
        result.UsedBytes = used;
        result.FreeBytes = free;
    }

    private static void WalkDirectory(
        string directory,
        string scanRoot,
        EnumerationOptions options,
        AppSettings settings,
        ParallelOptions parallelOptions,
        ScanState state,
        IProgress<ScanProgress>? progress,
        DateTime now,
        DateTime oldThreshold,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var normalized = NormalizePath(directory);
        if (!state.VisitedDirs.TryAdd(normalized, 0))
            return;

        if (settings.ExcludedFolders.Any(ex => normalized.StartsWith(NormalizePath(ex), StringComparison.OrdinalIgnoreCase)))
            return;

        if (IsReparsePoint(directory))
        {
            state.SkippedJunctions.Add(directory);
            return;
        }

        Interlocked.Increment(ref state.FoldersIndexed);

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(LongPath(directory), "*", options);
        }
        catch (Exception ex)
        {
            state.AccessErrors.Add($"{directory}: {ex.Message}");
            return;
        }

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                ProcessFile(file, directory, scanRoot, settings, state, now, oldThreshold);
            }
            catch (Exception ex)
            {
                state.AccessErrors.Add($"{file}: {ex.Message}");
            }
        }

        var dirName = Path.GetFileName(directory.TrimEnd('\\', '/'));
        if (DevArtifactNames.Contains(dirName))
        {
            ProcessDevArtifact(directory, scanRoot, options, state, ct);
            MaybeReport(progress, state, directory);
            return;
        }

        List<string> dirList;
        try
        {
            dirList = Directory.EnumerateDirectories(LongPath(directory), "*", options).ToList();
        }
        catch (Exception ex)
        {
            state.AccessErrors.Add($"{directory}: {ex.Message}");
            return;
        }

        if (dirList.Count > 4)
        {
            Parallel.ForEach(dirList, parallelOptions, sub =>
            {
                try
                {
                    WalkDirectory(sub, scanRoot, options, settings, parallelOptions, state, progress, now, oldThreshold, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    state.AccessErrors.Add($"{sub}: {ex.Message}");
                }
            });
        }
        else
        {
            foreach (var sub in dirList)
            {
                WalkDirectory(sub, scanRoot, options, settings, parallelOptions, state, progress, now, oldThreshold, ct);
            }
        }

        MaybeReport(progress, state, directory);
    }

    private static void ProcessFile(
        string file,
        string directory,
        string scanRoot,
        AppSettings settings,
        ScanState state,
        DateTime now,
        DateTime oldThreshold)
    {
        var fi = new FileInfo(StripLongPath(file));
        if (!fi.Exists)
        {
            fi = new FileInfo(file);
            if (!fi.Exists) return;
        }
        if ((fi.Attributes & FileAttributes.ReparsePoint) != 0) return;

        var size = fi.Length;
        var ext = fi.Extension;
        var cleanPath = StripLongPath(fi.FullName);
        var category = FileClassifier.ClassifyPath(cleanPath, false, ext);
        var (risk, reason) = RiskEngine.Evaluate(cleanPath, false, category, fi.Name);

        var entry = new FileEntry
        {
            FullPath = cleanPath,
            Name = fi.Name,
            Extension = ext,
            DirectoryPath = StripLongPath(fi.DirectoryName ?? directory),
            SizeBytes = size,
            CreatedUtc = fi.CreationTimeUtc,
            ModifiedUtc = fi.LastWriteTimeUtc,
            AccessedUtc = fi.LastAccessTimeUtc,
            Category = category,
            Risk = risk,
            Reason = reason
        };

        state.CategoryBytes.AddOrUpdate(category, size, (_, old) => old + size);
        state.CategoryCounts.AddOrUpdate(category, 1, (_, old) => old + 1);
        AddLargest(state.LargestFiles, entry, settings.MaxLargestFiles * 3);
        AccumulateAncestors(state, entry.FullPath, size, fi.LastWriteTimeUtc, scanRoot);

        if (size >= settings.DuplicateMinSizeBytes)
            state.SizeBuckets.GetOrAdd(size, _ => new ConcurrentBag<string>()).Add(entry.FullPath);

        if (fi.LastWriteTimeUtc < oldThreshold && size >= 1024 * 1024)
            state.OldCandidates.Add(entry);
        if (fi.LastWriteTimeUtc >= now.AddDays(-14) && size >= 100 * 1024)
            state.RecentCandidates.Add(entry);
        if (FileClassifier.IsUnderDownloads(cleanPath))
            state.DownloadsFiles.Add(entry);

        Interlocked.Increment(ref state.FilesIndexed);
        Interlocked.Add(ref state.BytesIndexed, size);
    }

    private static void ProcessDevArtifact(string directory, string scanRoot, EnumerationOptions options, ScanState state, CancellationToken ct)
    {
        directory = StripLongPath(directory);
        var artifactSize = MeasureDirectorySize(directory, state.AccessErrors, ct);
        var key = directory.TrimEnd('\\');
        state.FolderSizes.AddOrUpdate(key, artifactSize, (_, _) => artifactSize);
        AccumulateAncestors(state, Path.Combine(directory, "_"), artifactSize, DateTime.UtcNow, scanRoot);

        var dirName = Path.GetFileName(directory.TrimEnd('\\', '/'));
        var project = Path.GetFileName(Path.GetDirectoryName(directory.TrimEnd('\\')) ?? "Project") ?? "Project";
        var canRegen = !dirName.Equals(".git", StringComparison.OrdinalIgnoreCase);
        var risk = RiskLevel.Review;
        state.DevArtifacts.Add(new DevArtifact
        {
            ProjectName = project,
            ArtifactName = dirName,
            FullPath = directory,
            SizeBytes = artifactSize,
            CanRegenerate = canRegen,
            Risk = risk,
            Recommendation = ExplanationEngine.RecommendedAction(risk, canRegen),
            Reason = ExplanationEngine.ExplainWhyLarge(directory)
        });

        Interlocked.Add(ref state.BytesIndexed, artifactSize);
    }

    private static long MeasureDirectorySize(string directory, ConcurrentBag<string> errors, CancellationToken ct)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(LongPath(directory), "*", new EnumerationOptions
                     {
                         IgnoreInaccessible = true,
                         RecurseSubdirectories = true,
                         AttributesToSkip = FileAttributes.ReparsePoint
                     }))
            {
                ct.ThrowIfCancellationRequested();
                try { total += new FileInfo(file).Length; }
                catch (Exception ex) { errors.Add($"{file}: {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{directory}: {ex.Message}");
        }
        return total;
    }

    private static void AccumulateAncestors(ScanState state, string filePath, long size, DateTime modified, string scanRoot)
    {
        filePath = StripLongPath(filePath);
        scanRoot = StripLongPath(scanRoot);
        var dir = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(dir))
        {
            var key = StripLongPath(dir).TrimEnd('\\');
            state.FolderSizes.AddOrUpdate(key, size, (_, old) => old + size);
            state.FolderFileCounts.AddOrUpdate(key, 1, (_, old) => old + 1);
            state.FolderLastModified.AddOrUpdate(key, modified, (_, old) => modified > old ? modified : old);

            if (key.Equals(scanRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                break;

            var parent = Path.GetDirectoryName(key);
            if (string.IsNullOrEmpty(parent) || parent.Equals(key, StringComparison.OrdinalIgnoreCase))
                break;
            dir = parent;
        }
    }

    private static void AddLargest(ConcurrentBag<(long Size, FileEntry Entry)> list, FileEntry entry, int capacity)
    {
        list.Add((entry.SizeBytes, entry));
        if (list.Count <= capacity * 2) return;

        var trimmed = list.OrderByDescending(x => x.Size).Take(capacity).ToList();
        // ConcurrentBag cannot Clear; replace by draining
        while (list.TryTake(out _)) { }
        foreach (var item in trimmed)
            list.Add(item);
    }

    private static void BuildCategories(ScanResult result, ScanState state)
    {
        var scannedBytes = state.CategoryBytes.Values.Sum();
        foreach (FileCategory cat in Enum.GetValues<FileCategory>())
        {
            state.CategoryBytes.TryGetValue(cat, out var size);
            state.CategoryCounts.TryGetValue(cat, out var count);
            result.Categories.Add(new CategoryBreakdown
            {
                Category = cat,
                SizeBytes = size,
                FileCount = count,
                PercentOfTotal = scannedBytes > 0 ? (double)size / scannedBytes : 0
            });
        }
        result.Categories.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
    }

    private static List<FolderNode> BuildLargestFolders(ScanState state, int max)
    {
        return state.FolderSizes
            .OrderByDescending(kv => kv.Value)
            .Take(max)
            .Select(kv =>
            {
                var name = Path.GetFileName(kv.Key.TrimEnd('\\', '/'));
                if (string.IsNullOrEmpty(name)) name = kv.Key;
                var category = FileClassifier.ClassifyPath(kv.Key, true);
                var (risk, reason) = RiskEngine.Evaluate(kv.Key, true, category, name);
                state.FolderFileCounts.TryGetValue(kv.Key, out var fc);
                state.FolderLastModified.TryGetValue(kv.Key, out var lm);
                return new FolderNode
                {
                    FullPath = kv.Key,
                    Name = name,
                    SizeBytes = kv.Value,
                    FileCount = fc,
                    LastModifiedUtc = lm,
                    Category = category,
                    Risk = risk,
                    Reason = reason
                };
            })
            .ToList();
    }

    private static FolderNode BuildFolderTree(ScanState state, List<string> roots)
    {
        var root = new FolderNode { FullPath = "This PC", Name = "This PC" };
        foreach (var r in roots)
        {
            var key = r.TrimEnd('\\');
            state.FolderSizes.TryGetValue(key, out var size);
            state.FolderFileCounts.TryGetValue(key, out var fc);
            state.FolderLastModified.TryGetValue(key, out var lm);
            var name = key.Length == 2 && key[1] == ':' ? key + @"\" : Path.GetFileName(key);
            if (string.IsNullOrEmpty(name)) name = key;
            var node = new FolderNode
            {
                FullPath = key,
                Name = name,
                SizeBytes = size,
                FileCount = fc,
                LastModifiedUtc = lm,
                Category = FileClassifier.ClassifyPath(key, true)
            };
            var (risk, reason) = RiskEngine.Evaluate(key, true, node.Category, name);
            node.Risk = risk;
            node.Reason = reason;
            root.Children[key] = node;
            root.SizeBytes += size;
            root.FileCount += fc;
        }
        return root;
    }

    private static DownloadsAnalysis AnalyzeDownloads(ConcurrentBag<FileEntry> files)
    {
        var downloadsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var analysis = new DownloadsAnalysis { Path = downloadsPath };
        foreach (var item in files.Where(f => f.FullPath.StartsWith(downloadsPath, StringComparison.OrdinalIgnoreCase)))
        {
            analysis.TotalBytes += item.SizeBytes;
            analysis.Items.Add(item);
            switch (item.Category)
            {
                case FileCategory.Installers: analysis.InstallerBytes += item.SizeBytes; break;
                case FileCategory.Archives: analysis.ArchiveBytes += item.SizeBytes; break;
                case FileCategory.Videos: analysis.VideoBytes += item.SizeBytes; break;
                case FileCategory.Documents: analysis.DocumentBytes += item.SizeBytes; break;
            }
            if (item.ModifiedUtc < DateTime.UtcNow.AddDays(-90))
                analysis.OldFileBytes += item.SizeBytes;
        }

        analysis.PotentialCleanupBytes = analysis.InstallerBytes + analysis.ArchiveBytes + analysis.OldFileBytes / 2;
        var top = analysis.Items.OrderByDescending(i => i.SizeBytes).Take(200).ToList();
        analysis.Items.Clear();
        analysis.Items.AddRange(top);
        return analysis;
    }

    private static List<AppStorageInfo> DetectApplications(ConcurrentDictionary<string, long> folderSizes)
    {
        var patterns = new (string Name, string[] Hints)[]
        {
            ("Google Chrome", new[] { @"\Google\Chrome" }),
            ("Microsoft Edge", new[] { @"\Microsoft\Edge" }),
            ("Mozilla Firefox", new[] { @"\Mozilla\Firefox" }),
            ("Steam", new[] { @"\Steam" }),
            ("Docker", new[] { @"\Docker" }),
            ("Visual Studio Code", new[] { @"\Microsoft VS Code", @"\Code\User" }),
            ("Android Studio", new[] { @"\Android", @"\Google\AndroidStudio" }),
            ("Adobe", new[] { @"\Adobe" }),
            ("Discord", new[] { @"\Discord" }),
            ("Spotify", new[] { @"\Spotify" }),
            ("npm / pnpm / Yarn caches", new[] { @"\npm-cache", @"\pnpm\store", @"\Yarn\Berry\cache" }),
        };

        var results = new List<AppStorageInfo>();
        foreach (var (name, hints) in patterns)
        {
            var info = new AppStorageInfo { ApplicationName = name };
            foreach (var kv in folderSizes)
            {
                if (!hints.Any(h => kv.Key.Contains(h, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (kv.Key.Count(c => c == '\\') > 8) continue;

                if (kv.Key.Contains("cache", StringComparison.OrdinalIgnoreCase))
                    info.CacheSizeBytes += kv.Value;
                else if (kv.Key.Contains("AppData", StringComparison.OrdinalIgnoreCase))
                    info.UserDataSizeBytes += kv.Value;
                else
                    info.InstalledSizeBytes += kv.Value;
                info.DetectedPaths.Add(kv.Key);
            }

            if (info.TotalBytes > 50L * 1024 * 1024)
                results.Add(info);
        }

        return results.OrderByDescending(a => a.TotalBytes).Take(30).ToList();
    }

    private static void TryAddRecycleBinSuggestion(ScanResult result)
    {
        try
        {
            var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
            if (sid == null) return;
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var bin = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin", sid);
                if (!Directory.Exists(bin)) continue;
                long size = 0;
                try
                {
                    size = Directory.EnumerateFiles(bin, "*", new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true
                    }).Select(f => { try { return new FileInfo(f).Length; } catch { return 0L; } }).Sum();
                }
                catch { /* ignore */ }

                if (size <= 0) continue;
                result.CleanupSuggestions.Insert(0, new CleanupSuggestion
                {
                    Id = Guid.NewGuid().ToString("N"),
                    DisplayName = $"Recycle Bin ({drive.Name.TrimEnd('\\')})",
                    FullPath = bin,
                    SizeBytes = size,
                    Category = FileCategory.Temporary,
                    Risk = RiskLevel.Safe,
                    Reason = "Recycle Bin contents. These are files you previously deleted and can usually be emptied safely.",
                    RecommendedAction = "Empty via Windows Recycle Bin after confirmation.",
                    CanRegenerate = false,
                    IsDirectory = true
                });
            }
        }
        catch { /* ignore */ }
    }

    private static void SaveHistory(AppSettings settings, ScanResult result)
    {
        if (!settings.EnableScanHistory) return;
        ScanHistoryStore.Append(new ScanHistoryEntry
        {
            TimestampUtc = result.CompletedUtc,
            TotalBytes = result.TotalSizeBytes,
            UsedBytes = result.UsedBytes,
            FreeBytes = result.FreeBytes,
            FileCount = result.FileCount,
            PotentialCleanupBytes = result.CleanupSuggestions
                .Where(s => s.Risk is RiskLevel.Safe or RiskLevel.Review)
                .Sum(s => s.SizeBytes),
            TopCategories = result.Categories.Take(8).ToDictionary(c => c.Category.ToString(), c => c.SizeBytes),
            TopFolders = result.LargestFolders.Take(10).Select(f => f.Name).ToList()
        });
    }

    private static List<string> ResolveRoots(ScanRequest request) => request.TargetKind switch
    {
        ScanTargetKind.Drive when !string.IsNullOrWhiteSpace(request.SpecificPath)
            => new List<string> { request.SpecificPath },
        ScanTargetKind.Folder when !string.IsNullOrWhiteSpace(request.SpecificPath)
            => new List<string> { request.SpecificPath },
        _ => DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network)
            .Select(d => d.RootDirectory.FullName)
            .ToList()
    };

    private static void MaybeReport(IProgress<ScanProgress>? progress, ScanState state, string directory)
    {
        var files = Interlocked.Read(ref state.FilesIndexed);
        if (files % 2000 > 20) return;
        Report(progress, "Scanning...", directory, files,
            Interlocked.Read(ref state.FoldersIndexed),
            Interlocked.Read(ref state.BytesIndexed),
            10 + Math.Min(55, files / 20000.0));
    }

    private static void Report(IProgress<ScanProgress>? progress, string phase, string path, long files, long folders, long bytes, double pct) =>
        progress?.Report(new ScanProgress
        {
            Phase = phase,
            CurrentPath = path,
            FilesIndexed = files,
            FoldersIndexed = folders,
            BytesIndexed = bytes,
            PercentEstimate = pct
        });

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd('\\').ToLowerInvariant(); }
        catch { return path.TrimEnd('\\').ToLowerInvariant(); }
    }

    private static string LongPath(string path)
    {
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path;
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
            return @"\\?\UNC\" + path.TrimStart('\\');
        return @"\\?\" + path;
    }

    private static string StripLongPath(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            return @"\\" + path[8..];
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            return path[4..];
        return path;
    }

    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(StripLongPath(path)) & FileAttributes.ReparsePoint) != 0; }
        catch
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch { return false; }
        }
    }
}

public static class DriveEnumerator
{
    public static List<DriveSummary> GetDrives()
    {
        var list = new List<DriveSummary>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                list.Add(new DriveSummary
                {
                    Name = d.Name,
                    RootPath = d.RootDirectory.FullName,
                    VolumeLabel = d.IsReady ? d.VolumeLabel : string.Empty,
                    DriveType = d.DriveType switch
                    {
                        DriveType.Removable => DriveTypeKind.Removable,
                        DriveType.Fixed => DriveTypeKind.Fixed,
                        DriveType.Network => DriveTypeKind.Network,
                        DriveType.CDRom => DriveTypeKind.CdRom,
                        DriveType.Ram => DriveTypeKind.Ram,
                        _ => DriveTypeKind.Unknown
                    },
                    TotalBytes = d.IsReady ? d.TotalSize : 0,
                    FreeBytes = d.IsReady ? d.AvailableFreeSpace : 0,
                    UsedBytes = d.IsReady ? d.TotalSize - d.AvailableFreeSpace : 0,
                    IsReady = d.IsReady
                });
            }
            catch
            {
                list.Add(new DriveSummary
                {
                    Name = d.Name,
                    RootPath = d.Name,
                    IsReady = false,
                    DriveType = DriveTypeKind.Unknown
                });
            }
        }
        return list;
    }
}

public static class DuplicateDetector
{
    public static List<DuplicateGroup> FindDuplicates(
        ConcurrentDictionary<long, ConcurrentBag<string>> sizeBuckets,
        long minSize,
        CancellationToken ct,
        int maxGroups = 100)
    {
        var groups = new List<DuplicateGroup>();

        foreach (var bucket in sizeBuckets.Where(b => b.Key >= minSize && b.Value.Count > 1))
        {
            ct.ThrowIfCancellationRequested();
            var paths = bucket.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (paths.Count < 2) continue;

            var byHash = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var hash = ComputeQuickHash(path);
                    if (!byHash.TryGetValue(hash, out var list))
                    {
                        list = new List<string>();
                        byHash[hash] = list;
                    }
                    list.Add(path);
                }
                catch { /* skip */ }
            }

            foreach (var (hash, list) in byHash.Where(kv => kv.Value.Count > 1))
            {
                var group = new DuplicateGroup
                {
                    GroupId = Guid.NewGuid().ToString("N")[..8],
                    FileSizeBytes = bucket.Key,
                    Hash = hash
                };
                foreach (var p in list)
                {
                    try
                    {
                        var fi = new FileInfo(p);
                        group.Files.Add(new FileEntry
                        {
                            FullPath = fi.FullName,
                            Name = fi.Name,
                            Extension = fi.Extension,
                            DirectoryPath = fi.DirectoryName ?? "",
                            SizeBytes = fi.Length,
                            CreatedUtc = fi.CreationTimeUtc,
                            ModifiedUtc = fi.LastWriteTimeUtc,
                            AccessedUtc = fi.LastAccessTimeUtc,
                            Category = FileClassifier.ClassifyByExtension(fi.Extension),
                            Risk = RiskLevel.Review,
                            Reason = "Duplicate candidate based on size and content hash. Choose which copy to keep.",
                            Hash = hash
                        });
                    }
                    catch { /* skip */ }
                }

                if (group.Files.Count > 1)
                    groups.Add(group);
                if (groups.Count >= maxGroups)
                    return groups.OrderByDescending(g => g.RecoverableBytes).ToList();
            }
        }

        return groups.OrderByDescending(g => g.RecoverableBytes).ToList();
    }

    public static string ComputeQuickHash(string path)
    {
        const int chunk = 1024 * 1024;
        var info = new FileInfo(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create();

        if (info.Length <= chunk * 2)
            return Convert.ToHexString(sha.ComputeHash(stream));

        var buffer = new byte[chunk];
        var read = stream.Read(buffer, 0, buffer.Length);
        sha.TransformBlock(buffer, 0, read, null, 0);
        stream.Seek(-Math.Min(chunk, info.Length - chunk), SeekOrigin.End);
        read = stream.Read(buffer, 0, buffer.Length);
        sha.TransformBlock(buffer, 0, read, null, 0);
        var sizeBytes = BitConverter.GetBytes(info.Length);
        sha.TransformFinalBlock(sizeBytes, 0, sizeBytes.Length);
        return Convert.ToHexString(sha.Hash!);
    }
}
