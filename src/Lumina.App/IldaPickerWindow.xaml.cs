using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Lumina.Core;
using IOPath = System.IO.Path;

namespace Lumina.App;

public sealed class IldaPickResult
{
    public required bool AsAnimation { get; init; }
    public required IReadOnlyList<int> Indices { get; init; }
}

public partial class IldaPickerWindow : Window
{
    private readonly IReadOnlyList<LaserFrame> _frames;
    private readonly ObservableCollection<FramePickItem> _items = [];
    private readonly DispatcherTimer _play = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private int _playIndex;
    private bool _suppressSlider;

    public IldaPickResult? Result { get; private set; }

    public IldaPickerWindow(string sourcePath, IReadOnlyList<LaserFrame> frames)
    {
        InitializeComponent();
        _frames = frames;
        Title = "Lumina — " + IOPath.GetFileName(sourcePath);
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            var summary = IldaInspection.Summarize(frame, i);
            var item = new FramePickItem(i, summary.Name, summary.VisibleCount, summary.PointCount, frame);
            item.PropertyChanged += (_, _) => RefreshSummary();
            _items.Add(item);
        }

        FrameList.ItemsSource = _items;
        FrameSlider.Maximum = Math.Max(0, frames.Count - 1);
        var many = frames.Count > 12;
        AnimationMode.IsChecked = many;
        ShapesMode.IsChecked = !many;
        foreach (var item in _items) item.Include = !many;
        if (!many && _items.Count > 0) _items[0].Include = true;
        if (many && _items.Count > 0) _items[0].Include = true;

        RefreshSummary();
        if (_items.Count > 0)
        {
            FrameList.SelectedIndex = 0;
            ShowIndex(0);
        }

        _play.Tick += (_, _) =>
        {
            if (_frames.Count == 0) return;
            _playIndex = (_playIndex + 1) % _frames.Count;
            ShowIndex(_playIndex);
        };
        Closed += (_, _) => _play.Stop();
    }

    private void FrameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FrameList.SelectedItem is FramePickItem item) ShowIndex(item.Index);
    }

    private void FrameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSlider) return;
        ShowIndex((int)e.NewValue);
    }

    private void ShowIndex(int index)
    {
        if (_frames.Count == 0) return;
        index = Math.Clamp(index, 0, _frames.Count - 1);
        _suppressSlider = true;
        FrameSlider.Value = index;
        if (FrameList.SelectedIndex != index) FrameList.SelectedIndex = index;
        _suppressSlider = false;
        var frame = _frames[index];
        var summary = IldaInspection.Summarize(frame, index);
        PreviewLabel.Text = $"{summary.Name}  ·  {summary.VisibleCount} points visibles  ·  {summary.FormatLabel}";
        Preview.ShowFrame(frame);
        FrameList.ScrollIntoView(_items[index]);
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items) item.Include = true;
        ShapesMode.IsChecked = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items) item.Include = false;
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_play.IsEnabled)
        {
            _play.Stop();
            PlayButton.Content = "Lire";
            return;
        }

        _playIndex = (int)FrameSlider.Value;
        _play.Start();
        PlayButton.Content = "Pause";
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _play.Stop();
        PlayButton.Content = "Lire";
        ShowIndex(0);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var animation = AnimationMode.IsChecked == true;
        var indices = animation
            ? Enumerable.Range(0, _frames.Count).ToArray()
            : _items.Where(i => i.Include).Select(i => i.Index).ToArray();
        if (indices.Length == 0)
        {
            MessageBox.Show(this, "Cochez au moins une forme, ou choisissez l’animation complète.", "Lumina",
                MessageBoxButton.OK);
            return;
        }

        Result = new IldaPickResult { AsAnimation = animation, Indices = indices };
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void RefreshSummary()
    {
        var n = _items.Count(i => i.Include);
        SummaryText.Text =
            $"{_frames.Count} frame(s) dans le fichier · {n} forme(s) cochée(s). " +
            "L’animation complète place un seul clip. Les formes cochées deviennent des clips successifs sur la timeline.";
    }
}

public sealed class FramePickItem : INotifyPropertyChanged
{
    private bool _include;

    public FramePickItem(int index, string name, int visible, int points, LaserFrame frame)
    {
        Index = index;
        Name = string.IsNullOrWhiteSpace(name) ? $"Frame {index + 1}" : name;
        Stats = $"{visible} visibles / {points} pts";
        Frame = frame;
    }

    public int Index { get; }
    public string Name { get; }
    public string Stats { get; }
    public LaserFrame Frame { get; }

    public bool Include
    {
        get => _include;
        set
        {
            if (_include == value) return;
            _include = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
