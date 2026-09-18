using System.Text;
using System.Text.Json;
using SpaceLens.Core.Models;
using SpaceLens.Core.Utilities;

namespace SpaceLens.Scanner;

public static class ReportExporter
{
    public static void ExportJson(ScanResult result, string path)
    {
        var payload = BuildPayload(result);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static void ExportCsv(ScanResult result, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Section,Name,Path,SizeBytes,Category,Risk,Reason");
        foreach (var f in result.LargestFiles.Take(100))
            sb.AppendLine(Csv("LargestFile", f.Name, f.FullPath, f.SizeBytes, f.Category, f.Risk, f.Reason));
        foreach (var f in result.LargestFolders.Take(100))
            sb.AppendLine(Csv("LargestFolder", f.Name, f.FullPath, f.SizeBytes, f.Category, f.Risk, f.Reason));
        foreach (var c in result.CleanupSuggestions.Take(200))
            sb.AppendLine(Csv("Cleanup", c.DisplayName, c.FullPath, c.SizeBytes, c.Category, c.Risk, c.Reason));
        File.WriteAllText(path, sb.ToString());
    }

    public static void ExportHtml(ScanResult result, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>SpaceLens Report</title>");
        sb.AppendLine("<style>body{font-family:'Segoe UI',sans-serif;max-width:960px;margin:40px auto;color:#FFFFFF;background:#0A0A0A;line-height:1.5}");
        sb.AppendLine("h1,h2{font-weight:600;color:#FFFFFF}h1{letter-spacing:-0.02em}.accent{color:#00E5FF}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin:16px 0}th,td{border-bottom:1px solid #2C2C2C;padding:10px 8px;text-align:left;color:#D0D0D0}");
        sb.AppendLine("th{font-family:Consolas,'Cascadia Mono',monospace;font-size:12px;color:#00E5FF;text-transform:uppercase;letter-spacing:0.06em}");
        sb.AppendLine(".warn{background:#141414;border:1px solid #2C2C2C;border-left:2px solid #00E5FF;padding:14px 16px;border-radius:4px;margin:16px 0;color:#D0D0D0}</style></head><body>");
        sb.AppendLine("<h1>Space<span class='accent'>Lens</span> Storage Report</h1>");
        sb.AppendLine($"<p style='color:#B8B8B8'>Scan completed: {result.CompletedUtc:u}</p>");
        sb.AppendLine("<div class='warn'><strong style='color:#FFFFFF'>Privacy notice:</strong> This report includes file paths that may contain personal information. Keep it private.</div>");
        sb.AppendLine($"<p>Used {SizeFormatter.Format(result.UsedBytes)} / {SizeFormatter.Format(result.TotalSizeBytes)} — Free <span class='accent'>{SizeFormatter.Format(result.FreeBytes)}</span></p>");
        sb.AppendLine($"<p style='color:#B8B8B8'>Files: {result.FileCount:N0} · Folders: {result.FolderCount:N0}</p>");

        sb.AppendLine("<h2>Categories</h2><table><tr><th>Category</th><th>Size</th><th>Files</th></tr>");
        foreach (var c in result.Categories.Where(c => c.SizeBytes > 0))
            sb.AppendLine($"<tr><td>{c.Category}</td><td>{SizeFormatter.Format(c.SizeBytes)}</td><td>{c.FileCount:N0}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Largest Folders</h2><table><tr><th>Folder</th><th>Size</th><th>Risk</th></tr>");
        foreach (var f in result.LargestFolders.Take(25))
            sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(f.FullPath)}</td><td>{SizeFormatter.Format(f.SizeBytes)}</td><td>{f.Risk}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Cleanup Suggestions</h2><table><tr><th>Item</th><th>Size</th><th>Risk</th><th>Reason</th></tr>");
        foreach (var c in result.CleanupSuggestions.Take(50))
            sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(c.DisplayName)}</td><td>{SizeFormatter.Format(c.SizeBytes)}</td><td>{c.Risk}</td><td>{System.Net.WebUtility.HtmlEncode(c.Reason)}</td></tr>");
        sb.AppendLine("</table></body></html>");
        File.WriteAllText(path, sb.ToString());
    }

    private static object BuildPayload(ScanResult result) => new
    {
        result.StartedUtc,
        result.CompletedUtc,
        result.TotalSizeBytes,
        result.UsedBytes,
        result.FreeBytes,
        result.FileCount,
        result.FolderCount,
        Categories = result.Categories.Select(c => new { c.Category, c.SizeBytes, c.FileCount }),
        LargestFiles = result.LargestFiles.Take(100).Select(f => new { f.Name, f.FullPath, f.SizeBytes, f.Category, f.Risk }),
        LargestFolders = result.LargestFolders.Take(100).Select(f => new { f.Name, f.FullPath, f.SizeBytes, f.Category, f.Risk }),
        Cleanup = result.CleanupSuggestions.Take(200).Select(c => new { c.DisplayName, c.FullPath, c.SizeBytes, c.Risk, c.Reason }),
        Duplicates = result.DuplicateGroups.Take(50).Select(g => new { g.GroupId, g.RecoverableBytes, Count = g.Files.Count }),
        Notice = "Paths may contain personal information. Analysis was performed locally."
    };

    private static string Csv(string section, string name, string path, long size, object category, object risk, string reason)
    {
        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        return $"{Q(section)},{Q(name)},{Q(path)},{size},{category},{risk},{Q(reason)}";
    }
}
