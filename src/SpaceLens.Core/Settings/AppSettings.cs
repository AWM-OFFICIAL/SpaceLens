using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceLens.Core.Settings;

public sealed class AppSettings
{
    public bool HasCompletedFirstRun { get; set; }
    public bool HasSeenMinimizeTip { get; set; }
    public string Theme { get; set; } = "Dark"; // Dark | Light
    public List<string> ExcludedFolders { get; set; } = new()
    {
        @"C:\Windows\WinSxS",
        @"C:\System Volume Information",
        @"C:\$Recycle.Bin"
    };
    public List<string> ProtectedFolders { get; set; } = new();
    public long DuplicateMinSizeBytes { get; set; } = 1024 * 1024; // 1 MB
    public int MaxLargestFiles { get; set; } = 200;
    public int MaxLargestFolders { get; set; } = 100;
    public bool EnableScanHistory { get; set; } = true;
    public bool LaunchElevatedPreferred { get; set; }
    public int Parallelism { get; set; } = Math.Max(2, Environment.ProcessorCount / 2);
    public List<string> LastScanRoots { get; set; } = new();
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpaceLens");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");
    public static string HistoryPath => Path.Combine(SettingsDirectory, "scan-history.json");

    public static AppSettings Load()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}

public sealed class ScanHistoryEntry
{
    public DateTime TimestampUtc { get; set; }
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long FreeBytes { get; set; }
    public long FileCount { get; set; }
    public long PotentialCleanupBytes { get; set; }
    public Dictionary<string, long> TopCategories { get; set; } = new();
    public List<string> TopFolders { get; set; } = new();
}

public static class ScanHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static List<ScanHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(AppSettings.HistoryPath))
                return new List<ScanHistoryEntry>();
            var json = File.ReadAllText(AppSettings.HistoryPath);
            return JsonSerializer.Deserialize<List<ScanHistoryEntry>>(json, JsonOptions) ?? new();
        }
        catch
        {
            return new List<ScanHistoryEntry>();
        }
    }

    public static void Append(ScanHistoryEntry entry)
    {
        var list = Load();
        list.Add(entry);
        // Keep last 50 lightweight snapshots — no file contents
        if (list.Count > 50)
            list = list.OrderByDescending(x => x.TimestampUtc).Take(50).OrderBy(x => x.TimestampUtc).ToList();
        Directory.CreateDirectory(AppSettings.SettingsDirectory);
        File.WriteAllText(AppSettings.HistoryPath, JsonSerializer.Serialize(list, JsonOptions));
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(AppSettings.HistoryPath))
                File.Delete(AppSettings.HistoryPath);
        }
        catch
        {
            // ignore
        }
    }
}
