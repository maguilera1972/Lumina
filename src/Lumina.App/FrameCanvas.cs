using System.Windows;
using System.Windows.Media;
using Lumina.Core;

namespace Lumina.App;

/// <summary>Lightweight ILDA drawing via OnRender (no per-point Line controls).</summary>
public sealed class FrameCanvas : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame),
        typeof(LaserFrame),
        typeof(FrameCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFrameChanged));

    private static void OnFrameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameCanvas canvas) canvas.InvalidateVisual();
    }

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(FrameCanvas),
        new FrameworkPropertyMetadata(1.4, FrameworkPropertyMetadataOptions.AffectsRender));

    public LaserFrame? Frame
    {
        get => (LaserFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 2 || h < 2) return;

        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(0x07, 0x0B, 0x12)),
            new Pen(new SolidColorBrush(Color.FromRgb(0x1E, 0x6A, 0x78)), 1),
            new Rect(0.5, 0.5, w - 1, h - 1),
            8, 8);

        var frame = Frame;
        if (frame is null || frame.Points.Count == 0) return;

        double MapX(short x) => (x + 32768.0) / 65535.0 * w;
        double MapY(short y) => (1.0 - (y + 32768.0) / 65535.0) * h;

        var figures = new Dictionary<int, StreamGeometryContext>();
        var geos = new Dictionary<int, StreamGeometry>();
        StreamGeometryContext? current = null;
        var currentKey = int.MinValue;
        var started = false;
        Point last = default;

        void Flush()
        {
            current = null;
            currentKey = int.MinValue;
            started = false;
        }

        StreamGeometryContext Ctx(int key)
        {
            if (figures.TryGetValue(key, out var ctx)) return ctx;
            var geo = new StreamGeometry { FillRule = FillRule.EvenOdd };
            ctx = geo.Open();
            geos[key] = geo;
            figures[key] = ctx;
            return ctx;
        }

        foreach (var p in frame.Points)
        {
            var pt = new Point(MapX(p.X), MapY(p.Y));
            if (p.IsVisible && started)
            {
                var key = p.R << 16 | p.G << 8 | p.B;
                if (current is null || key != currentKey)
                {
                    current = Ctx(key);
                    currentKey = key;
                    current.BeginFigure(last, false, false);
                }

                current.LineTo(pt, true, true);
            }
            else
            {
                Flush();
            }

            last = pt;
            started = true;
        }

        foreach (var ctx in figures.Values) ctx.Close();

        var thickness = Math.Max(0.6, StrokeThickness);
        foreach (var (key, geo) in geos)
        {
            geo.Freeze();
            var r = (byte)(key >> 16);
            var g = (byte)((key >> 8) & 0xFF);
            var b = (byte)(key & 0xFF);
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(r, g, b)), thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }
    }
}
