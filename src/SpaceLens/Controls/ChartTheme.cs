using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace SpaceLens.Controls;

/// <summary>Resolves chart colors from the active theme dictionary.</summary>
public static class ChartTheme
{
    public static event Action? Changed;

    public static void NotifyChanged() => Changed?.Invoke();

    public static MediaColor Accent =>
        ResolveColor("AccentColor", MediaColor.FromRgb(0x00, 0xE5, 0xFF));

    public static MediaColor AccentDim =>
        ResolveColor("AccentDimColor", MediaColor.FromRgb(0x00, 0xB8, 0xCC));

    public static MediaColor TextSecondary =>
        ResolveColor("TextSecondaryColor", MediaColor.FromRgb(0xD0, 0xD0, 0xD0));

    public static MediaColor TextMuted =>
        ResolveColor("TextMutedColor", MediaColor.FromRgb(0xB8, 0xB8, 0xB8));

    public static MediaColor Track =>
        ResolveColor("ChartTrackColor", MediaColor.FromRgb(0x1C, 0x1C, 0x1C));

    public static MediaColor Border =>
        ResolveColor("BorderColor", MediaColor.FromRgb(0x2C, 0x2C, 0x2C));

    /// <summary>Cyan → gray ladder shared by donut, bars, and legend dots.</summary>
    public static MediaColor Category(int index)
    {
        var palette = new[]
        {
            Accent,
            SoftAccent,
            AccentDim,
            TextMuted,
            MediaColor.FromRgb(0x7A, 0x7A, 0x7A),
            MediaColor.FromRgb(0x4A, 0x4A, 0x4A),
            TextSecondary,
            MediaColor.FromRgb(0x2E, 0x2E, 0x2E),
        };
        return palette[Math.Abs(index) % palette.Length];
    }

    private static MediaColor SoftAccent =>
        MediaColor.FromArgb(0xFF,
            (byte)Math.Min(255, Accent.R + 90),
            (byte)Math.Min(255, Accent.G + 9),
            Accent.B);

    private static MediaColor ResolveColor(string key, MediaColor fallback)
    {
        var res = Application.Current?.TryFindResource(key);
        return res switch
        {
            MediaColor c => c,
            SolidColorBrush b => b.Color,
            _ => fallback
        };
    }
}
