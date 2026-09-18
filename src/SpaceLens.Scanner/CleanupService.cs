using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SpaceLens.Core.Models;

namespace SpaceLens.Scanner;

/// <summary>
/// Safe cleanup operations. Default is Recycle Bin. Permanent delete requires explicit flag + confirmation upstream.
/// </summary>
public static class CleanupService
{
    public sealed class CleanupResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public long BytesMoved { get; set; }
        public List<string> Errors { get; } = new();
        public bool UsedRecycleBin { get; set; } = true;
    }

    public static CleanupResult Execute(
        IEnumerable<CleanupSuggestion> selected,
        bool permanentDelete,
        Func<string, bool>? confirmEach = null)
    {
        var result = new CleanupResult { UsedRecycleBin = !permanentDelete };

        foreach (var item in selected)
        {
            if (item.Risk == RiskLevel.Protected)
            {
                result.FailCount++;
                result.Errors.Add($"Blocked protected item: {item.FullPath}");
                continue;
            }

            if (confirmEach != null && !confirmEach(item.FullPath))
                continue;

            try
            {
                if (item.IsDirectory || Directory.Exists(item.FullPath))
                {
                    if (permanentDelete)
                        Directory.Delete(item.FullPath, recursive: true);
                    else
                        Recycle(item.FullPath, isDirectory: true);
                }
                else if (File.Exists(item.FullPath))
                {
                    if (permanentDelete)
                        File.Delete(item.FullPath);
                    else
                        Recycle(item.FullPath, isDirectory: false);
                }
                else
                {
                    result.FailCount++;
                    result.Errors.Add($"Not found: {item.FullPath}");
                    continue;
                }

                result.SuccessCount++;
                result.BytesMoved += item.SizeBytes;
            }
            catch (Exception ex)
            {
                result.FailCount++;
                result.Errors.Add($"{item.FullPath}: {ex.Message}");
            }
        }

        return result;
    }

    public static void OpenInExplorer(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            return;
        }

        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            return;
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }

    public static void OpenFile(string path)
    {
        if (!File.Exists(path)) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public static void OpenRecycleBin()
    {
        Process.Start(new ProcessStartInfo("explorer.exe", "shell:RecycleBinFolder") { UseShellExecute = true });
    }

    private static void Recycle(string path, bool isDirectory)
    {
        // Shell32 FO_DELETE with FOF_ALLOWUNDO → Recycle Bin
        _ = isDirectory;
        ShellRecycle(path);
    }

    private static void ShellRecycle(string path)
    {
        var fs = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
        };
        var code = SHFileOperation(ref fs);
        if (code != 0)
            throw new IOException($"Shell recycle failed with code {code} for {path}");
    }

    private const int FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public int wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
}
