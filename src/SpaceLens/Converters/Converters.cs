using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SpaceLens.Core.Models;
using SpaceLens.Core.Utilities;
using MediaColor = System.Windows.Media.Color;

namespace SpaceLens.Converters;

public sealed class SizeFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is long l ? SizeFormatter.Format(l) : value is double d ? SizeFormatter.Format((long)d) : "—";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PercentFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double d ? SizeFormatter.FormatPercent(d) : "—";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class RiskToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        RiskLevel risk = value switch
        {
            RiskLevel r => r,
            string s when Enum.TryParse<RiskLevel>(s, true, out var parsed) => parsed,
            _ => RiskLevel.Unknown
        };
        return risk switch
        {
            RiskLevel.Safe => new SolidColorBrush(MediaColor.FromRgb(0x2F, 0x9E, 0x74)),
            RiskLevel.Review => new SolidColorBrush(MediaColor.FromRgb(0xD9, 0x8E, 0x24)),
            RiskLevel.Protected => new SolidColorBrush(MediaColor.FromRgb(0xD9, 0x4F, 0x4F)),
            _ => new SolidColorBrush(MediaColor.FromRgb(0x8B, 0x93, 0xA7))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (Invert) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not true;
}

/// <summary>Maps ItemsControl AlternationIndex to theme chart palette colors.</summary>
public sealed class CategoryIndexBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var i = value is int idx ? idx : 0;
        return new SolidColorBrush(SpaceLens.Controls.ChartTheme.Category(i));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var has = value != null;
        if (Invert) has = !has;
        return has ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DriveUsageWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var maxWidth = 200.0;
        if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var w))
            maxWidth = w;
        if (value is DriveSummary drive && drive.TotalBytes > 0)
            return Math.Max(4, maxWidth * drive.UsedBytes / drive.TotalBytes);
        return 4.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Compares bound string to ConverterParameter; returns Visibility.Visible when equal.</summary>
public sealed class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var left = value?.ToString() ?? "";
        var right = parameter?.ToString() ?? "";
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NavActiveBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var current = values.ElementAtOrDefault(0)?.ToString() ?? "";
        var item = values.ElementAtOrDefault(1)?.ToString() ?? "";
        var active = string.Equals(current, item, StringComparison.OrdinalIgnoreCase);
        if (!active) return Brushes.Transparent;

        if (System.Windows.Application.Current?.TryFindResource("AccentSoftBrush") is SolidColorBrush soft)
            return soft.IsFrozen ? soft.Clone() : soft.CloneCurrentValue();
        return new SolidColorBrush(MediaColor.FromArgb(0x33, 0x00, 0xE5, 0xFF));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NavActiveForegroundConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var current = values.ElementAtOrDefault(0)?.ToString() ?? "";
        var item = values.ElementAtOrDefault(1)?.ToString() ?? "";
        var active = string.Equals(current, item, StringComparison.OrdinalIgnoreCase);
        var key = active ? "TextPrimaryBrush" : "TextSecondaryBrush";
        if (System.Windows.Application.Current?.TryFindResource(key) is SolidColorBrush brush)
            return brush.IsFrozen ? brush.Clone() : brush.CloneCurrentValue();
        return active
            ? new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF))
            : new SolidColorBrush(MediaColor.FromRgb(0xD0, 0xD0, 0xD0));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NavActiveVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var current = values.ElementAtOrDefault(0)?.ToString() ?? "";
        var item = values.ElementAtOrDefault(1)?.ToString() ?? "";
        return string.Equals(current, item, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
