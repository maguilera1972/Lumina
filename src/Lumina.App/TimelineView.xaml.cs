using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Lumina.Core;

namespace Lumina.App;

public partial class TimelineView : UserControl
{
    private LaserShow? _show;
    private TimeSpan _duration = TimeSpan.FromSeconds(10);
    private TimeSpan _position;
    private ShowScene? _selected;
    private TimelineClip? _selectedClip;
    private bool _suppressFields;

    public event Action<TimeSpan>? SeekRequested;
    public event Action? TimelineChanged;
    public event Action<ShowScene?>? SceneSelected;
    public event Action<TimelineClip?>? ClipSelected;

    public TimelineView()
    {
        InitializeComponent();
    }

    public ShowScene? SelectedScene => _selected;
    public TimelineClip? SelectedClip => _selectedClip;

    public void Bind(LaserShow? show, TimeSpan duration)
    {
        _show = show;
        _duration = duration > TimeSpan.Zero ? duration : TimeSpan.FromSeconds(10);
        if (_selected is not null && show?.Timeline.Scenes.Contains(_selected) != true)
        {
            _selected = show?.Timeline.Scenes.FirstOrDefault();
        }

        if (_selectedClip is not null && show?.Timeline.Clips.Contains(_selectedClip) != true)
        {
            _selectedClip = show?.Timeline.Clips.FirstOrDefault();
        }

        Redraw();
        PushFields();
    }

    public void SetPosition(TimeSpan position)
    {
        _position = position;
        RedrawPlayhead();
    }

    public void Select(ShowScene? scene)
    {
        _selected = scene;
        Redraw();
        PushFields();
        SceneSelected?.Invoke(scene);
    }

    public void SelectClip(TimelineClip? clip)
    {
        _selectedClip = clip;
        Redraw();
        ClipSelected?.Invoke(clip);
    }

    private void LaserTrack_SizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Track_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var canvas = (Canvas)sender;
        var x = e.GetPosition(canvas).X;
        var t = TimeFromX(x, canvas.ActualWidth);
        if (ReferenceEquals(canvas, LaserTrack) && _show is not null)
        {
            foreach (var scene in _show.Timeline.Scenes)
            {
                var x0 = XFromTime(scene.Start, canvas.ActualWidth);
                var x1 = XFromTime(scene.End, canvas.ActualWidth);
                if (x >= x0 && x <= x1)
                {
                    Select(scene);
                    SeekRequested?.Invoke(t);
                    return;
                }
            }
        }

        SeekRequested?.Invoke(t);
    }

    private void ClipTrack_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var canvas = (Canvas)sender;
        var x = e.GetPosition(canvas).X;
        var t = TimeFromX(x, canvas.ActualWidth);
        if (_show is not null)
        {
            foreach (var clip in _show.Timeline.Clips)
            {
                var x0 = XFromTime(clip.Start, canvas.ActualWidth);
                var x1 = XFromTime(clip.End, canvas.ActualWidth);
                if (x >= x0 && x <= x1)
                {
                    SelectClip(clip);
                    SeekRequested?.Invoke(t);
                    return;
                }
            }
        }

        SeekRequested?.Invoke(t);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_show is null) return;
        var scene = _show.Timeline.AddScene(_show.Frames.Count);
        Select(scene);
        TimelineChanged?.Invoke();
        Redraw();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_show is null || _selected is null) return;
        _show.Timeline.Remove(_selected);
        Select(_show.Timeline.Scenes.LastOrDefault());
        TimelineChanged?.Invoke();
        Redraw();
    }

    private void Left_Click(object sender, RoutedEventArgs e)
    {
        if (_show is null || _selected is null) return;
        _show.Timeline.Move(_selected, -1);
        TimelineChanged?.Invoke();
        Redraw();
    }

    private void Right_Click(object sender, RoutedEventArgs e)
    {
        if (_show is null || _selected is null) return;
        _show.Timeline.Move(_selected, 1);
        TimelineChanged?.Invoke();
        Redraw();
    }

    private void Compact_Click(object sender, RoutedEventArgs e)
    {
        _show?.Timeline.Compact();
        TimelineChanged?.Invoke();
        Redraw();
        PushFields();
    }

    private void Fields_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressFields || _selected is null) return;
        _selected.Name = NameBox.Text.Trim().Length == 0 ? _selected.Name : NameBox.Text.Trim();
        if (double.TryParse(StartBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var start))
        {
            var d = _selected.Duration;
            _selected.Start = TimeSpan.FromSeconds(Math.Max(0, start));
            _selected.End = _selected.Start + d;
        }

        if (double.TryParse(DurationBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dur))
        {
            _selected.Duration = TimeSpan.FromSeconds(Math.Max(0.05, dur));
        }

        if (int.TryParse(FrameStartBox.Text, out var fs)) _selected.FrameStart = Math.Max(0, fs);
        if (int.TryParse(FrameEndBox.Text, out var fe)) _selected.FrameEnd = Math.Max(_selected.FrameStart, fe);
        TimelineChanged?.Invoke();
        Redraw();
    }

    private void PushFields()
    {
        _suppressFields = true;
        if (_selected is null)
        {
            NameBox.Text = StartBox.Text = DurationBox.Text = FrameStartBox.Text = FrameEndBox.Text = "";
        }
        else
        {
            NameBox.Text = _selected.Name;
            StartBox.Text = _selected.Start.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            DurationBox.Text = _selected.Duration.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            FrameStartBox.Text = _selected.FrameStart.ToString();
            FrameEndBox.Text = _selected.FrameEnd.ToString();
        }

        _suppressFields = false;
    }

    private void Redraw()
    {
        AudioTrack.Children.Clear();
        LaserTrack.Children.Clear();
        ClipTrack.Children.Clear();
        var w = LaserTrack.ActualWidth;
        if (w < 4) return;

        var bar = new Rectangle
        {
            Width = Math.Max(4, XFromTime(_duration, AudioTrack.ActualWidth > 0 ? AudioTrack.ActualWidth : w)),
            Height = 18,
            Fill = new SolidColorBrush(Color.FromRgb(0x12, 0x4A, 0x58)),
            RadiusX = 3,
            RadiusY = 3
        };
        Canvas.SetTop(bar, 5);
        AudioTrack.Children.Add(bar);

        if (_show is not null)
        {
            foreach (var scene in _show.Timeline.Scenes)
            {
                var x = XFromTime(scene.Start, w);
                var width = Math.Max(8, XFromTime(scene.End, w) - x);
                var selected = ReferenceEquals(scene, _selected);
                var rect = new Rectangle
                {
                    Width = width,
                    Height = 26,
                    Fill = new SolidColorBrush(selected ? Color.FromRgb(0x00, 0xE5, 0xFF) : Color.FromRgb(0x1A, 0x5A, 0x78)),
                    Stroke = selected ? Brushes.White : Brushes.Transparent,
                    RadiusX = 3,
                    RadiusY = 3
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, 5);
                LaserTrack.Children.Add(rect);
                var label = new TextBlock
                {
                    Text = scene.Name,
                    Foreground = selected ? Brushes.Black : Brushes.White,
                    FontSize = 11,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, x + 6);
                Canvas.SetTop(label, 8);
                LaserTrack.Children.Add(label);
            }

            foreach (var clip in _show.Timeline.Clips)
            {
                var x = XFromTime(clip.Start, w);
                var width = Math.Max(8, XFromTime(clip.End, w) - x);
                var selected = ReferenceEquals(clip, _selectedClip);
                var rect = new Rectangle
                {
                    Width = width,
                    Height = 22,
                    Fill = new SolidColorBrush(selected ? Color.FromRgb(0xFF, 0x4E, 0xC8) : Color.FromRgb(0x7A, 0x2A, 0x6A)),
                    Stroke = selected ? Brushes.White : Brushes.Transparent,
                    RadiusX = 3,
                    RadiusY = 3
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, 5);
                ClipTrack.Children.Add(rect);
                var label = new TextBlock
                {
                    Text = clip.Name,
                    Foreground = selected ? Brushes.Black : Brushes.White,
                    FontSize = 11,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, x + 6);
                Canvas.SetTop(label, 7);
                ClipTrack.Children.Add(label);
            }
        }

        DrawPlayhead();
    }

    private void RedrawPlayhead()
    {
        RemovePlayheads(AudioTrack);
        RemovePlayheads(LaserTrack);
        RemovePlayheads(ClipTrack);
        DrawPlayhead();
    }

    private static void RemovePlayheads(Canvas canvas)
    {
        for (var i = canvas.Children.Count - 1; i >= 0; i--)
        {
            if (canvas.Children[i] is Line { Tag: "playhead" }) canvas.Children.RemoveAt(i);
        }
    }

    private void DrawPlayhead()
    {
        DrawHead(AudioTrack, AudioTrack.ActualHeight);
        DrawHead(LaserTrack, LaserTrack.ActualHeight);
        DrawHead(ClipTrack, ClipTrack.ActualHeight);
    }

    private void DrawHead(Canvas canvas, double h)
    {
        var w = canvas.ActualWidth;
        if (w < 2 || h < 2) return;
        var x = XFromTime(_position, w);
        canvas.Children.Add(new Line
        {
            Tag = "playhead",
            X1 = x,
            X2 = x,
            Y1 = 0,
            Y2 = h,
            Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF)),
            StrokeThickness = 1.4
        });
    }

    private double XFromTime(TimeSpan t, double width) =>
        t.TotalSeconds / Math.Max(0.001, _duration.TotalSeconds) * width;

    private TimeSpan TimeFromX(double x, double width) =>
        TimeSpan.FromSeconds(Math.Clamp(x / Math.Max(1, width), 0, 1) * _duration.TotalSeconds);
}
