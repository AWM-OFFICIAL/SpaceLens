using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace SpaceLens.Controls;

/// <summary>Thin circular progress ring (0–100). Used sparingly for drive fill.</summary>
public sealed class RingGauge : FrameworkElement
{
    public RingGauge()
    {
        ChartTheme.Changed += OnThemeChanged;
        Unloaded += (_, _) => ChartTheme.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged() => InvalidateVisual();

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(RingGauge),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty =
        DependencyProperty.Register(nameof(Thickness), typeof(double), typeof(RingGauge),
            new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var cx = w / 2;
        var cy = h / 2;
        var radius = Math.Min(w, h) / 2 - Thickness / 2 - 1;

        DrawArc(dc, cx, cy, radius, Thickness, -90, 360, ChartTheme.Border);
        var pct = Math.Clamp(Value, 0, 100);
        if (pct > 0.05)
            DrawArc(dc, cx, cy, radius, Thickness, -90, 360.0 * pct / 100.0, ChartTheme.Accent);
    }

    private static void DrawArc(DrawingContext dc, double cx, double cy, double radius, double thickness, double startDeg, double sweepDeg, MediaColor color)
    {
        if (sweepDeg <= 0.01) return;
        var startRad = startDeg * Math.PI / 180;
        var endRad = (startDeg + sweepDeg) * Math.PI / 180;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            var p1 = new WpfPoint(cx + Math.Cos(startRad) * radius, cy + Math.Sin(startRad) * radius);
            var p2 = new WpfPoint(cx + Math.Cos(endRad) * radius, cy + Math.Sin(endRad) * radius);
            ctx.BeginFigure(p1, false, false);
            ctx.ArcTo(p2, new Size(radius, radius), 0, sweepDeg > 180, SweepDirection.Clockwise, true, false);
        }
        geo.Freeze();
        var pen = new Pen(new SolidColorBrush(color), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawGeometry(null, pen, geo);
    }
}
