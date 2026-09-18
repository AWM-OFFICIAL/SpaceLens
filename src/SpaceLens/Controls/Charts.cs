using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SpaceLens.Core.Models;
using SpaceLens.Core.Utilities;
using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace SpaceLens.Controls;

public sealed class DonutChart : FrameworkElement
{
    public DonutChart()
    {
        ChartTheme.Changed += OnThemeChanged;
        Unloaded += (_, _) => ChartTheme.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged() => InvalidateVisual();

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IEnumerable<CategoryBreakdown>), typeof(DonutChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsChanged));

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DonutChart chart)
            chart.InvalidateVisual();
    }

    public IEnumerable<CategoryBreakdown>? Items
    {
        get => (IEnumerable<CategoryBreakdown>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var cx = w / 2;
        var cy = h / 2;
        var radius = Math.Min(w, h) / 2 - 6;
        var thickness = Math.Max(14, radius * 0.28);
        var items = Items?.Where(i => i.SizeBytes > 0).Take(8).ToList() ?? new List<CategoryBreakdown>();
        var total = items.Sum(i => i.SizeBytes);

        DrawArc(dc, cx, cy, radius, thickness, -90, 360, ChartTheme.Track);

        if (total <= 0) return;

        double angle = -90;
        for (var i = 0; i < items.Count; i++)
        {
            var sweep = 360.0 * items[i].SizeBytes / total;
            var drawSweep = Math.Max(0, sweep - 1.2);
            DrawArc(dc, cx, cy, radius, thickness, angle + 0.6, drawSweep, ChartTheme.Category(i));
            angle += sweep;
        }
    }

    private static void DrawArc(DrawingContext dc, double cx, double cy, double radius, double thickness, double startDeg, double sweepDeg, MediaColor color)
    {
        if (sweepDeg <= 0.01) return;
        var startRad = startDeg * Math.PI / 180;
        var endRad = (startDeg + sweepDeg) * Math.PI / 180;
        var outer = radius;
        var inner = radius - thickness;

        var p1 = new WpfPoint(cx + Math.Cos(startRad) * outer, cy + Math.Sin(startRad) * outer);
        var p2 = new WpfPoint(cx + Math.Cos(endRad) * outer, cy + Math.Sin(endRad) * outer);
        var p3 = new WpfPoint(cx + Math.Cos(endRad) * inner, cy + Math.Sin(endRad) * inner);
        var p4 = new WpfPoint(cx + Math.Cos(startRad) * inner, cy + Math.Sin(startRad) * inner);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(p1, true, true);
            ctx.ArcTo(p2, new Size(outer, outer), 0, sweepDeg > 180, SweepDirection.Clockwise, true, false);
            ctx.LineTo(p3, true, false);
            ctx.ArcTo(p4, new Size(inner, inner), 0, sweepDeg > 180, SweepDirection.Counterclockwise, true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(new SolidColorBrush(color), null, geo);
    }
}

/// <summary>
/// Horizontal distribution rows: label | track+fill | size.
/// Labels never overlap bars (fixes prior strikethrough layout).
/// </summary>
public sealed class BarChart : FrameworkElement
{
    private const double LabelCol = 108;
    private const double SizeCol = 72;
    private const double Gap = 12;
    private const double RowH = 36;
    private const double BarH = 6;

    public BarChart()
    {
        ChartTheme.Changed += OnThemeChanged;
        Unloaded += (_, _) => ChartTheme.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged() => InvalidateVisual();

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IEnumerable<CategoryBreakdown>), typeof(BarChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure, OnItemsChanged));

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BarChart chart)
        {
            chart.InvalidateMeasure();
            chart.InvalidateVisual();
        }
    }

    public IEnumerable<CategoryBreakdown>? Items
    {
        get => (IEnumerable<CategoryBreakdown>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Items?.Count(i => i.SizeBytes > 0) ?? 0;
        count = Math.Min(count, 8);
        var h = count == 0 ? 80 : count * RowH;
        var w = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        return new Size(Math.Max(200, w), h);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var items = Items?.Where(i => i.SizeBytes > 0).Take(8).ToList() ?? new();
        if (items.Count == 0 || ActualWidth <= 0) return;
        var max = items.Max(i => i.SizeBytes);
        if (max <= 0) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var labelFace = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var monoFace = new Typeface(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var labelBrush = new SolidColorBrush(ChartTheme.TextSecondary);
        var mutedBrush = new SolidColorBrush(ChartTheme.TextMuted);
        var trackBrush = new SolidColorBrush(ChartTheme.Track);

        var barAreaW = Math.Max(40, ActualWidth - LabelCol - SizeCol - Gap * 2);

        for (var i = 0; i < items.Count; i++)
        {
            var y = i * RowH;
            var midY = y + (RowH - BarH) / 2;
            var fillBrush = new SolidColorBrush(ChartTheme.Category(i));

            var name = items[i].Category.ToString();
            var label = new FormattedText(name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                labelFace, 13, labelBrush, dpi)
            {
                MaxTextWidth = LabelCol - 4,
                Trimming = TextTrimming.CharacterEllipsis
            };
            dc.DrawText(label, new WpfPoint(0, y + (RowH - label.Height) / 2));

            var barX = LabelCol + Gap;
            dc.DrawRoundedRectangle(trackBrush, null, new Rect(barX, midY, barAreaW, BarH), 2, 2);
            var fill = barAreaW * items[i].SizeBytes / max;
            dc.DrawRoundedRectangle(fillBrush, null, new Rect(barX, midY, Math.Max(3, fill), BarH), 2, 2);

            var sizeStr = SizeFormatter.Format(items[i].SizeBytes);
            var sizeText = new FormattedText(sizeStr, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                monoFace, 12, mutedBrush, dpi);
            var sizeX = ActualWidth - sizeText.Width;
            dc.DrawText(sizeText, new WpfPoint(sizeX, y + (RowH - sizeText.Height) / 2));
        }
    }
}

public sealed class HistoryLineChart : FrameworkElement
{
    public HistoryLineChart()
    {
        ChartTheme.Changed += OnThemeChanged;
        Unloaded += (_, _) => ChartTheme.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged() => InvalidateVisual();

    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(nameof(Points), typeof(IList<WpfPoint>), typeof(HistoryLineChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IList<WpfPoint>? Points
    {
        get => (IList<WpfPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var pts = Points;
        if (pts == null || pts.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0) return;

        var minY = pts.Min(p => p.Y);
        var maxY = pts.Max(p => p.Y);
        if (Math.Abs(maxY - minY) < 1) maxY = minY + 1;

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i < pts.Count; i++)
            {
                var x = pts.Count == 1 ? ActualWidth / 2 : i * ActualWidth / (pts.Count - 1);
                var y = ActualHeight - ((pts[i].Y - minY) / (maxY - minY) * (ActualHeight - 8) + 4);
                if (i == 0) ctx.BeginFigure(new WpfPoint(x, y), false, false);
                else ctx.LineTo(new WpfPoint(x, y), true, false);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(ChartTheme.Accent), 2), geo);
    }
}
