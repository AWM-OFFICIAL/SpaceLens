using System.Globalization;

namespace SpaceLens.Core.Utilities;

public static class SizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes)
    {
        if (bytes < 0) bytes = 0;
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} {Units[unit]}"
            : string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", value, Units[unit]);
    }

    public static string FormatPercent(double fraction) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.#}%", fraction * 100);
}
