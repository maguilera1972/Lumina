using System.Windows;
using System.Windows.Media;
using Lumina.Core;

namespace Lumina.App;

/// <summary>Lightweight ILDA thumbnail drawn in OnRender (no per-point Line controls).</summary>
public sealed class FrameCanvas : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame),
        typeof(LaserFrame),
        typeof(FrameCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public LaserFrame? Frame
    {
        get => (LaserFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
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

        Point? last = null;
        foreach (var p in frame.Points)
        {
            var pt = new Point(MapX(p.X), MapY(p.Y));
            if (last is { } from && p.IsVisible)
            {
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(p.R, p.G, p.B)), 1.2)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                dc.DrawLine(pen, from, pt);
            }

            last = pt;
        }
    }
}
