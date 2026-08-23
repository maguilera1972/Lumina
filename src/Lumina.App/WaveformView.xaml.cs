using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Lumina.App;

public partial class WaveformView : UserControl
{
    private float[] _peaks = [];
    private IReadOnlyList<TimeSpan> _beats = [];
    private TimeSpan _duration = TimeSpan.FromSeconds(10);
    private TimeSpan _position;

    public event Action<TimeSpan>? SeekRequested;

    public WaveformView()
    {
        InitializeComponent();
    }

    public void SetAudio(float[] peaks, TimeSpan duration, IReadOnlyList<TimeSpan>? beats = null)
    {
        _peaks = peaks;
        _beats = beats ?? [];
        _duration = duration > TimeSpan.Zero ? duration : TimeSpan.FromSeconds(10);
        HintText.Visibility = peaks.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Redraw();
    }

    public void Clear(string? hint = null)
    {
        _peaks = [];
        _beats = [];
        if (hint is not null) HintText.Text = hint;
        HintText.Visibility = Visibility.Visible;
        Redraw();
    }

    public void SetPosition(TimeSpan position)
    {
        _position = position;
        RedrawPlayheadOnly();
    }

    private void WaveCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void WaveCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var x = e.GetPosition(WaveCanvas).X;
        var t = TimeSpan.FromSeconds(Math.Clamp(x / Math.Max(1, WaveCanvas.ActualWidth), 0, 1) * _duration.TotalSeconds);
        SeekRequested?.Invoke(t);
    }

    private void Redraw()
    {
        WaveCanvas.Children.Clear();
        RulerCanvas.Children.Clear();
        var w = WaveCanvas.ActualWidth;
        var h = WaveCanvas.ActualHeight;
        if (w < 4 || h < 4) return;

        DrawRuler(RulerCanvas.ActualWidth > 0 ? RulerCanvas.ActualWidth : w, RulerCanvas.ActualHeight);

        if (_peaks.Length > 0)
        {
            var mid = h / 2;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(0, mid), true, true);
                for (var i = 0; i < _peaks.Length; i++)
                {
                    var x = i / (double)Math.Max(1, _peaks.Length - 1) * w;
                    ctx.LineTo(new Point(x, mid - _peaks[i] * mid * 0.92), true, false);
                }

                for (var i = _peaks.Length - 1; i >= 0; i--)
                {
                    var x = i / (double)Math.Max(1, _peaks.Length - 1) * w;
                    ctx.LineTo(new Point(x, mid + _peaks[i] * mid * 0.92), true, false);
                }
            }

            geo.Freeze();
            WaveCanvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = geo,
                Fill = new SolidColorBrush(Color.FromRgb(0xB6, 0xF5, 0xC8)),
                Opacity = 0.9
            });
        }

        DrawBeats(w, h);
        DrawPlayhead(w, h);
    }

    private void DrawBeats(double w, double h)
    {
        if (_beats.Count == 0 || _duration.TotalSeconds <= 0) return;
        var periodGuess = _beats.Count > 1 ? (_beats[1] - _beats[0]).TotalSeconds : 0.5;
        for (var i = 0; i < _beats.Count; i++)
        {
            var x = _beats[i].TotalSeconds / _duration.TotalSeconds * w;
            var downbeat = periodGuess > 0 && Math.Abs((_beats[i].TotalSeconds / (periodGuess * 4)) % 1) < 0.08;
            WaveCanvas.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 0,
                Y2 = h,
                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x8A)),
                StrokeThickness = downbeat ? 1.6 : 0.8,
                Opacity = downbeat ? 0.85 : 0.45
            });
        }
    }

    private void RedrawPlayheadOnly()
    {
        for (var i = WaveCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (WaveCanvas.Children[i] is Line { Tag: "playhead" })
            {
                WaveCanvas.Children.RemoveAt(i);
            }
        }

        DrawPlayhead(WaveCanvas.ActualWidth, WaveCanvas.ActualHeight);
    }

    private void DrawPlayhead(double w, double h)
    {
        if (w < 2 || _duration.TotalSeconds <= 0) return;
        var x = _position.TotalSeconds / _duration.TotalSeconds * w;
        WaveCanvas.Children.Add(new Line
        {
            Tag = "playhead",
            X1 = x,
            X2 = x,
            Y1 = 0,
            Y2 = h,
            Stroke = Brushes.White,
            StrokeThickness = 1.5
        });
    }

    private void DrawRuler(double w, double h)
    {
        if (w < 2) return;
        var seconds = _duration.TotalSeconds;
        var step = seconds > 60 ? 10 : 5;
        for (var t = 0.0; t <= seconds + 0.01; t += step)
        {
            var x = t / Math.Max(seconds, 0.001) * w;
            RulerCanvas.Children.Add(new Line
            {
                X1 = x, X2 = x, Y1 = h - 6, Y2 = h,
                Stroke = new SolidColorBrush(Color.FromRgb(0xF0, 0xF3, 0xF7))
            });
            var label = TimeSpan.FromSeconds(t);
            var text = new TextBlock
            {
                Text = $"{(int)label.TotalMinutes:00}:{label.Seconds:00}",
                Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF3, 0xF7)),
                FontSize = 11
            };
            Canvas.SetLeft(text, x + 4);
            Canvas.SetTop(text, 2);
            RulerCanvas.Children.Add(text);
        }
    }
}
