using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lumina.Core;

namespace Lumina.App;

public partial class LibraryView : UserControl
{
    private LaserShow? _show;
    private PatternItem? _selected;
    private ColorConversionProfile? _colors;

    public event Action? ImportClicked;
    public event Action? DetachRequested;
    public event Action<PatternItem>? PlaceRequested;
    public event Action<PatternItem>? PatternSelected;

    public PatternItem? SelectedPattern => _selected;
    public string? SelectedPatternId => _selected?.Id;

    public LibraryView()
    {
        InitializeComponent();
    }

    public void Bind(LaserShow? show, ColorConversionProfile? colors = null)
    {
        _show = show;
        _colors = colors;
        if (_selected is not null && show?.Library.Get(_selected.Id) is null)
        {
            _selected = show?.Library.Items.FirstOrDefault();
        }
        else if (_selected is null)
        {
            _selected = show?.Library.Items.FirstOrDefault();
        }

        Rebuild();
        PushPreview();
    }

    public void SetColors(ColorConversionProfile? colors)
    {
        _colors = colors;
        Rebuild();
        PushPreview();
    }

    public void SelectById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || _show is null) return;
        var pattern = _show.Library.Get(id);
        if (pattern is not null) Select(pattern, propagate: false);
    }

    public void PromptImport() => ImportClicked?.Invoke();

    private void Import_Click(object sender, RoutedEventArgs e) => ImportClicked?.Invoke();

    private void Detach_Click(object sender, RoutedEventArgs e) => DetachRequested?.Invoke();

    private void Place_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is not null) PlaceRequested?.Invoke(_selected);
    }

    private void Rebuild()
    {
        PadHost.Children.Clear();
        if (_show is null || _show.Library.Items.Count == 0)
        {
            PadHost.Children.Add(new TextBlock
            {
                Text = "Aucun motif. Importez un .ild / .fra, ou ouvrez un show.",
                Foreground = TryFindResource("BoothMuted") as Brush ?? Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4)
            });
            return;
        }

        for (var i = 0; i < _show.Library.Items.Count; i++)
        {
            var pattern = _show.Library.Items[i];
            var tile = BuildTile(pattern, i, ReferenceEquals(pattern, _selected));
            PadHost.Children.Add(tile);
        }
    }

    private FrameworkElement BuildTile(PatternItem pattern, int index, bool selected)
    {
        var visual = LaserColorConverter.Apply(pattern.FrameAtProgress(pattern.Frames.Count > 1 ? 0.18 : 0) ?? pattern.Frames.FirstOrDefault(), _colors);
        var preview = new LaserPreview
        {
            Width = 120,
            Height = 84,
            Margin = new Thickness(0, 0, 0, 6)
        };
        preview.ShowFrame(visual);

        var title = new TextBlock
        {
            Text = pattern.Name,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
            MaxHeight = 34
        };

        var meta = new TextBlock
        {
            Text = $"{pattern.Frames.Count} frame(s)  ·  pad {index + 1}",
            Foreground = TryFindResource("BoothMuted") as Brush ?? Brushes.LightGray,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };

        var stack = new StackPanel();
        stack.Children.Add(preview);
        stack.Children.Add(title);
        stack.Children.Add(meta);

        var btn = new Button
        {
            Width = 150,
            Height = 152,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(8),
            Content = stack,
            Background = TryFindResource("BoothPanel") as Brush,
            BorderBrush = selected
                ? (TryFindResource("BoothAccent") as Brush ?? Brushes.Cyan)
                : (TryFindResource("BoothEdge") as Brush ?? Brushes.Transparent),
            BorderThickness = new Thickness(selected ? 2 : 1),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Tag = pattern,
            ToolTip = pattern.SourcePath is null ? pattern.Name : $"{pattern.Name}\n{pattern.SourcePath}"
        };
        btn.Click += (_, _) => Select(pattern);
        btn.MouseDoubleClick += (_, _) => PlaceRequested?.Invoke(pattern);
        return btn;
    }

    public void Select(PatternItem pattern) => Select(pattern, propagate: true);

    private void Select(PatternItem pattern, bool propagate)
    {
        _selected = pattern;
        Rebuild();
        PushPreview();
        if (propagate) PatternSelected?.Invoke(pattern);
    }

    private void PushPreview()
    {
        if (_selected is null || _selected.Frames.Count == 0)
        {
            PadLabel.Text = "Aucun motif";
            PadPreview.Clear();
            return;
        }

        PadLabel.Text = $"{_selected.Name}  ({_selected.Frames.Count} frame(s))";
        var frame = _selected.FrameAtProgress(_selected.Frames.Count > 1 ? 0.18 : 0) ?? _selected.Frames[0];
        PadPreview.ShowFrame(LaserColorConverter.Apply(frame, _colors));
    }
}
