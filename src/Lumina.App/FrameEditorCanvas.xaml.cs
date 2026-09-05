using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Lumina.Core;

namespace Lumina.App;

public enum EditorTool { Pen, Move, Erase }

/// <summary>
/// Interactive WPF drawing surface for the laser frame editor.
/// Coordinates map to the ILDA range ±32767 (Y axis inverted: screen-down = ILDA-negative).
/// </summary>
public partial class FrameEditorCanvas : UserControl
{
    // ── State ────────────────────────────────────────────────────────────────

    private FrameEditSession? _session;
    public EditorTool ActiveTool { get; set; } = EditorTool.Pen;
    public Color PenColor { get; set; } = Color.FromRgb(0, 229, 255);
    public bool BlankedMode { get; set; }

    // Per-point visual handles (filled circles)
    private readonly List<(Ellipse Handle, EditablePoint Point)> _handles = [];
    private EditablePoint? _dragging;
    private Point _dragStart;
    private short _dragOrigX, _dragOrigY;

    // ── Init ─────────────────────────────────────────────────────────────────

    public FrameEditorCanvas() => InitializeComponent();

    public void Bind(FrameEditSession session)
    {
        _session = session;
        _session.PointsChanged += Refresh;
        _session.CurrentFrameChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        DrawCanvas.Children.Clear();
        _handles.Clear();

        var frame = _session?.CurrentFrame;
        if (frame is null || DrawCanvas.ActualWidth < 4) return;

        DrawLines(frame);
        DrawHandles(frame);
    }

    // ── Coordinate mapping ────────────────────────────────────────────────────

    private double W => DrawCanvas.ActualWidth;
    private double H => DrawCanvas.ActualHeight;

    private double MapX(short x) => (x + 32768.0) / 65535.0 * W;
    private double MapY(short y) => (1.0 - (y + 32768.0) / 65535.0) * H;

    private short IldaX(double cx) => (short)Math.Clamp((int)Math.Round(cx / W * 65535.0 - 32768.0), -32767, 32767);
    private short IldaY(double cy) => (short)Math.Clamp((int)Math.Round((1.0 - cy / H) * 65535.0 - 32768.0), -32767, 32767);

    // ── Drawing ───────────────────────────────────────────────────────────────

    private void DrawLines(EditableFrame frame)
    {
        Point? last = null;
        foreach (var p in frame.Points)
        {
            var pt = new Point(MapX(p.X), MapY(p.Y));
            if (last is { } from && !p.Blanked)
            {
                var col = p.Blanked
                    ? Color.FromArgb(60, 255, 255, 255)
                    : Color.FromRgb(p.R, p.G, p.B);
                DrawCanvas.Children.Add(new Line
                {
                    X1 = from.X, Y1 = from.Y,
                    X2 = pt.X, Y2 = pt.Y,
                    Stroke = new SolidColorBrush(col),
                    StrokeThickness = 1.8,
                    IsHitTestVisible = false
                });
            }
            last = pt;
        }
    }

    private void DrawHandles(EditableFrame frame)
    {
        const double r = 5;
        for (var i = 0; i < frame.Points.Count; i++)
        {
            var p = frame.Points[i];
            var col = p.Blanked
                ? Color.FromArgb(120, 255, 255, 255)
                : Color.FromRgb(p.R, p.G, p.B);

            var el = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Fill = new SolidColorBrush(col),
                Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                StrokeThickness = 0.8,
                Cursor = ActiveTool == EditorTool.Move ? Cursors.SizeAll
                       : ActiveTool == EditorTool.Erase ? Cursors.No
                       : Cursors.Hand
            };
            var cp = p;
            el.Tag = cp;

            Canvas.SetLeft(el, MapX(p.X) - r);
            Canvas.SetTop(el, MapY(p.Y) - r);
            DrawCanvas.Children.Add(el);

            _handles.Add((el, cp));

            el.MouseLeftButtonDown += (s, e) =>
            {
                if (ActiveTool == EditorTool.Erase)
                {
                    _session?.RemovePoint(cp);
                    e.Handled = true;
                    return;
                }
                if (ActiveTool == EditorTool.Move)
                {
                    _dragging = cp;
                    _dragStart = e.GetPosition(DrawCanvas);
                    _dragOrigX = cp.X;
                    _dragOrigY = cp.Y;
                    DrawCanvas.CaptureMouse();
                    e.Handled = true;
                }
            };
        }
    }

    // ── Mouse events ──────────────────────────────────────────────────────────

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_session?.CurrentFrame is null) return;
        var pos = e.GetPosition(DrawCanvas);

        if (ActiveTool == EditorTool.Pen)
        {
            _session.AddPoint(IldaX(pos.X), IldaY(pos.Y),
                BlankedMode ? (byte)0 : PenColor.R,
                BlankedMode ? (byte)0 : PenColor.G,
                BlankedMode ? (byte)0 : PenColor.B,
                BlankedMode);
            e.Handled = true;
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging is not null)
        {
            DrawCanvas.ReleaseMouseCapture();
            _dragging = null;
        }
    }

    private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Right-click on canvas erases last point in Pen mode
        if (ActiveTool == EditorTool.Pen && _session?.CurrentFrame?.Points.Count > 0)
        {
            var last = _session.CurrentFrame.Points[^1];
            _session.RemovePoint(last);
            e.Handled = true;
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(DrawCanvas);

        // Crosshair (only in Pen mode)
        if (ActiveTool == EditorTool.Pen)
        {
            CrossH.Visibility = CrossV.Visibility = Visibility.Visible;
            CrossH.X1 = 0; CrossH.Y1 = pos.Y; CrossH.X2 = W; CrossH.Y2 = pos.Y;
            CrossV.X1 = pos.X; CrossV.Y1 = 0; CrossV.X2 = pos.X; CrossV.Y2 = H;
        }
        else
        {
            CrossH.Visibility = CrossV.Visibility = Visibility.Collapsed;
        }

        CoordText.Text = $"X:{IldaX(pos.X):+0000;-0000}  Y:{IldaY(pos.Y):+0000;-0000}";

        // Drag move
        if (_dragging is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            _session?.MovePoint(_dragging, IldaX(pos.X), IldaY(pos.Y));

            // Move handle visually without full redraw for responsiveness
            foreach (var (h, pt) in _handles)
            {
                if (pt == _dragging)
                {
                    Canvas.SetLeft(h, MapX(pt.X) - 5);
                    Canvas.SetTop(h, MapY(pt.Y) - 5);
                }
            }
            // Redraw lines
            DrawCanvas.Children.OfType<Line>().ToList().ForEach(l => DrawCanvas.Children.Remove(l));
            if (_session?.CurrentFrame is not null) DrawLines(_session.CurrentFrame);
        }
    }

    private void Canvas_MouseLeave(object sender, MouseEventArgs e)
    {
        CrossH.Visibility = CrossV.Visibility = Visibility.Collapsed;
        CoordText.Text = "";
    }

    private void DrawCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Refresh();
}
