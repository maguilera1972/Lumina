using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Lumina.Core;

namespace Lumina.App;

public partial class LaserPreview : UserControl
{
    private LaserFrame? _frame;
    private bool _showHint = true;

    public LaserPreview()
    {
        InitializeComponent();
    }

    public void ShowFrame(LaserFrame? frame)
    {
        _frame = frame;
        Redraw();
    }

    public void Clear(bool showHint = true)
    {
        _frame = null;
        _showHint = showHint;
        Redraw();
    }

    public void SetHintVisible(bool visible)
    {
        _showHint = visible;
        Redraw();
    }

    private void DrawCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        DrawCanvas.Children.Clear();
        HintText.Visibility = _showHint && _frame is null ? Visibility.Visible : Visibility.Collapsed;
        if (_frame is null || DrawCanvas.ActualWidth < 4 || DrawCanvas.ActualHeight < 4) return;

        var w = DrawCanvas.ActualWidth;
        var h = DrawCanvas.ActualHeight;
        double MapX(short x) => (x + 32768.0) / 65535.0 * w;
        double MapY(short y) => (1.0 - (y + 32768.0) / 65535.0) * h;

        Point? last = null;
        foreach (var p in _frame.Points)
        {
            var pt = new Point(MapX(p.X), MapY(p.Y));
            if (last is { } from && p.IsVisible)
            {
                DrawCanvas.Children.Add(new Line
                {
                    X1 = from.X,
                    Y1 = from.Y,
                    X2 = pt.X,
                    Y2 = pt.Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(p.R, p.G, p.B)),
                    StrokeThickness = 1.5
                });
            }

            last = pt;
        }
    }
}
