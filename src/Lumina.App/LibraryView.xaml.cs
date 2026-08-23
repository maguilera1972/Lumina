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
    public event Action<PatternItem>? PlaceRequested;
    public event Action<PatternItem>? PatternSelected;

    public PatternItem? SelectedPattern => _selected;

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

        Rebuild();
        PushPreview();
    }

    public void SetColors(ColorConversionProfile? colors)
    {
        _colors = colors;
        PushPreview();
    }

    public void PromptImport() => ImportClicked?.Invoke();

    private void Import_Click(object sender, RoutedEventArgs e) => ImportClicked?.Invoke();

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

        foreach (var item in _show.Library.Items)
        {
            var pattern = item;
            var selected = ReferenceEquals(pattern, _selected);
            var btn = new Button
            {
                Content = $"{pattern.Name}\n{pattern.Frames.Count} fr.",
                Width = 108,
                Height = 56,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(6),
                Tag = pattern,
                BorderBrush = selected
                    ? (TryFindResource("BoothAccent") as Brush ?? Brushes.Cyan)
                    : Brushes.Transparent,
                BorderThickness = new Thickness(2)
            };
            btn.Click += (_, _) => Select(pattern);
            btn.MouseDoubleClick += (_, _) => PlaceRequested?.Invoke(pattern);
            PadHost.Children.Add(btn);
        }
    }

    public void Select(PatternItem pattern)
    {
        _selected = pattern;
        Rebuild();
        PushPreview();
        PatternSelected?.Invoke(pattern);
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
        PadPreview.ShowFrame(LaserColorConverter.Apply(_selected.Frames[0], _colors));
    }
}
