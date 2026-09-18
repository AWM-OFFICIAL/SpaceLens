using System.Collections.Concurrent;
using SpaceLens.Core.Classification;
using SpaceLens.Core.Explanations;
using SpaceLens.Core.Models;
using SpaceLens.Core.Settings;

namespace SpaceLens.Scanner;

internal sealed class ScanState
{
    public long FilesIndexed;
    public long FoldersIndexed;
    public long BytesIndexed;
    public readonly ConcurrentDictionary<FileCategory, long> CategoryBytes = new();
    public readonly ConcurrentDictionary<FileCategory, long> CategoryCounts = new();
    public readonly ConcurrentBag<(long Size, FileEntry Entry)> LargestFiles = new();
    public readonly ConcurrentDictionary<string, long> FolderSizes = new(StringComparer.OrdinalIgnoreCase);
    public readonly ConcurrentDictionary<string, long> FolderFileCounts = new(StringComparer.OrdinalIgnoreCase);
    public readonly ConcurrentDictionary<string, DateTime> FolderLastModified = new(StringComparer.OrdinalIgnoreCase);
    public readonly ConcurrentDictionary<long, ConcurrentBag<string>> SizeBuckets = new();
    public readonly ConcurrentBag<string> AccessErrors = new();
    public readonly ConcurrentBag<string> SkippedJunctions = new();
    public readonly ConcurrentDictionary<string, byte> VisitedDirs = new(StringComparer.OrdinalIgnoreCase);
    public readonly ConcurrentBag<FileEntry> OldCandidates = new();
    public readonly ConcurrentBag<FileEntry> RecentCandidates = new();
    public readonly ConcurrentBag<FileEntry> DownloadsFiles = new();
    public readonly ConcurrentBag<DevArtifact> DevArtifacts = new();
}
