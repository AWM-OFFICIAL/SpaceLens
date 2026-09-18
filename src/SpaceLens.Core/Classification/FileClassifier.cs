using SpaceLens.Core.Models;

namespace SpaceLens.Core.Classification;

/// <summary>
/// Rule-based file/folder classifier. Conservative: unknown stays Other/Unknown.
/// </summary>
public static class FileClassifier
{
    private static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tiff", ".tif", ".heic", ".ico", ".svg", ".raw", ".cr2", ".nef"
    };

    private static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".mov", ".avi", ".webm", ".wmv", ".m4v", ".flv", ".mpeg", ".mpg", ".ts", ".m2ts"
    };

    private static readonly HashSet<string> AudioExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".aiff", ".opus"
    };

    private static readonly HashSet<string> DocumentExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".odt", ".ods", ".odp",
        ".csv", ".md", ".epub", ".pages", ".numbers", ".key"
    };

    private static readonly HashSet<string> ArchiveExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".img", ".cab", ".lz4"
    };

    private static readonly HashSet<string> InstallerExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".msi", ".msix", ".appx", ".msixbundle", ".deb", ".rpm"
    };

    private static readonly HashSet<string> LogExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".log", ".etl"
    };

    private static readonly HashSet<string> DevFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".next", "dist", "build", "coverage", ".cache", "target",
        "bin", "obj", "venv", ".venv", "__pycache__", ".gradle", ".nuget", "bower_components",
        ".turbo", ".parcel-cache", "out", ".svelte-kit", "vendor"
    };

    private static readonly HashSet<string> TempFolderHints = new(StringComparer.OrdinalIgnoreCase)
    {
        "temp", "tmp", "cache", "caches", "crashdumps", "thumbnails", "inetcache", "temporary internet files"
    };

    public static FileCategory ClassifyByExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return FileCategory.Other;

        var ext = extension.StartsWith('.') ? extension : "." + extension;

        if (ImageExt.Contains(ext)) return FileCategory.Images;
        if (VideoExt.Contains(ext)) return FileCategory.Videos;
        if (AudioExt.Contains(ext)) return FileCategory.Audio;
        if (DocumentExt.Contains(ext)) return FileCategory.Documents;
        if (ArchiveExt.Contains(ext)) return FileCategory.Archives;
        if (InstallerExt.Contains(ext)) return FileCategory.Installers;
        if (LogExt.Contains(ext)) return FileCategory.Logs;
        if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return FileCategory.Installers; // often installers when large/in Downloads; risk engine refines

        return FileCategory.Other;
    }

    public static bool IsDevelopmentFolderName(string name) => DevFolderNames.Contains(name);

    public static bool LooksLikeTempFolder(string name) => TempFolderHints.Contains(name);

    public static FileCategory ClassifyPath(string fullPath, bool isDirectory, string? extension = null)
    {
        if (ProtectedPathRules.IsProtectedPath(fullPath))
            return FileCategory.System;

        var name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (isDirectory && IsDevelopmentFolderName(name))
            return FileCategory.Development;

        if (IsUnderDownloads(fullPath))
        {
            if (!isDirectory && !string.IsNullOrEmpty(extension))
            {
                var byExt = ClassifyByExtension(extension);
                return byExt == FileCategory.Other ? FileCategory.Downloads : byExt;
            }
            return FileCategory.Downloads;
        }

        if (IsUnderTempLocation(fullPath) || (isDirectory && LooksLikeTempFolder(name)))
            return FileCategory.Temporary;

        if (IsCachePath(fullPath))
            return FileCategory.Cache;

        if (IsGamePath(fullPath))
            return FileCategory.Games;

        if (IsApplicationPath(fullPath))
            return FileCategory.Applications;

        if (!isDirectory && !string.IsNullOrEmpty(extension))
            return ClassifyByExtension(extension);

        return FileCategory.Other;
    }

    public static bool IsUnderDownloads(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains(@"\downloads\", StringComparison.OrdinalIgnoreCase)
               || normalized.EndsWith(@"\downloads", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUnderTempLocation(string path)
    {
        var normalized = Normalize(path);
        var userTemp = Normalize(Path.GetTempPath());
        var winTemp = Normalize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));

        return normalized.StartsWith(userTemp, StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(winTemp, StringComparison.OrdinalIgnoreCase)
               || normalized.Contains(@"\appdata\local\temp\", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCachePath(string path)
    {
        var n = Normalize(path);
        return n.Contains(@"\cache\", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\caches\", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\code cache\", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\gpucache\", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\shadercache\", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\chromium\user data\default\cache", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\google\chrome\user data\default\cache", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\microsoft\edge\user data\default\cache", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\mozilla\firefox\profiles\", StringComparison.OrdinalIgnoreCase) && n.Contains(@"\cache", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGamePath(string path)
    {
        var n = Normalize(path);
        return n.Contains(@"\steam\steamapps\", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\epic games\", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\gog galaxy\games\", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\xboxgames\", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\riot games\", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsApplicationPath(string path)
    {
        var n = Normalize(path);
        return n.Contains(@"\program files\", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\program files (x86)\", StringComparison.OrdinalIgnoreCase)
               || n.Contains(@"\programdata\", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) =>
        path.Replace('/', '\\').TrimEnd('\\') + "\\";
}
