namespace SpaceLens.Core.Classification;

/// <summary>
/// Inspectable protected-path rules. These paths must never appear as one-click SAFE deletes.
/// </summary>
public static class ProtectedPathRules
{
    private static readonly string[] ProtectedPrefixes =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Boot"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SystemApps"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "servicing"),
        @"C:\Recovery",
        @"C:\Boot",
        @"C:\EFI",
        @"C:\System Volume Information",
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Microsoft\Windows\Start Menu",
    };

    private static readonly HashSet<string> ProtectedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "pagefile.sys", "hiberfil.sys", "swapfile.sys", "ntuser.dat", "usrclass.dat",
        "sam", "security", "software", "system", "default", "bootmgr", "ntldr", "ntdetect.com"
    };

    private static readonly HashSet<string> ProtectedUserProfileFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        // Whole profile folders are protected as deletion targets (contents may still be REVIEW)
        "Desktop", "Documents", "Pictures", "Videos", "Music", "OneDrive", "Favorites", "Contacts"
    };

    public static bool IsProtectedPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return true;

        var path = fullPath.TrimEnd('\\', '/');
        var fileName = Path.GetFileName(path);

        if (ProtectedFileNames.Contains(fileName))
            return true;

        foreach (var prefix in ProtectedPrefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                continue;

            if (path.Equals(prefix.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(prefix.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Protect entire user profile root (e.g. C:\Users\Name) but not all children
        var users = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(users))
        {
            var profileRoot = users.TrimEnd('\\');
            if (path.Equals(profileRoot, StringComparison.OrdinalIgnoreCase)
                || path.Equals(Path.GetDirectoryName(profileRoot), StringComparison.OrdinalIgnoreCase))
                return true;

            var parent = Path.GetDirectoryName(path);
            if (parent != null
                && parent.Equals(profileRoot, StringComparison.OrdinalIgnoreCase)
                && ProtectedUserProfileFolders.Contains(fileName))
                return true;
        }

        if (path.Contains(@"\Windows\System32\drivers\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\Windows\System32\config\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\AppData\Local\Microsoft\Credentials\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\AppData\Roaming\Microsoft\Credentials\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\AppData\Roaming\Microsoft\Protect\", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static bool IsUserDocumentLike(string fullPath)
    {
        var n = fullPath.Replace('/', '\\');
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

        bool Under(string root) =>
            !string.IsNullOrEmpty(root) &&
            (n.Equals(root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
             || n.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));

        // Only treat the known Windows library roots as personal media/docs —
        // not any path segment that happens to be named Music/Documents/etc.
        return Under(docs) || Under(desktop) || Under(pictures) || Under(videos) || Under(music);
    }
}
