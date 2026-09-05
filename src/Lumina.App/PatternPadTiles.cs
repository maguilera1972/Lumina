using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lumina.Core;

namespace Lumina.App;

/// <summary>Shared visual ILDA pad tiles for Library and Live surfaces.</summary>
public static class PatternPadTiles
{
    public static FrameworkElement Build(
        PatternItem pattern,
        int index,
        bool selected,
        ColorConversionProfile? colors,
        FrameworkElement? resourceHost,
        Action<int>? onPress,
        Action<int>? onRelease,
        Action<int>? onSelect,
        Action<PatternItem>? onDoubleClick = null)
    {
        var visual = LaserColorConverter.Apply(
            pattern.FrameAtProgress(pattern.Frames.Count > 1 ? 0.18 : 0) ?? pattern.Frames.FirstOrDefault(),
            colors);

        var preview = new FrameCanvas
        {
            Width = 120,
            Height = 84,
            Margin = new Thickness(0, 0, 0, 6),
            StrokeThickness = 1.0,
            Frame = visual,
            IsHitTestVisible = false
        };

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
            Text = $"{pattern.Frames.Count} fr · pad {index + 1}",
            Foreground = Brush(resourceHost, "BoothMuted", Color.FromRgb(0x9B, 0xB4, 0xC4)),
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
            Background = Brush(resourceHost, "BoothPanel", Color.FromRgb(0x12, 0x1A, 0x26)),
            BorderBrush = selected
                ? (Brush(resourceHost, "BoothAccent", Color.FromRgb(0x00, 0xE5, 0xFF)))
                : (Brush(resourceHost, "BoothEdge", Color.FromRgb(0x3A, 0xA8, 0xB8))),
            BorderThickness = new Thickness(selected ? 2 : 1),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Tag = index,
            ToolTip = pattern.SourcePath is null ? pattern.Name : $"{pattern.Name}\n{pattern.SourcePath}"
        };

        void Press(int slot)
        {
            onSelect?.Invoke(slot);
            onPress?.Invoke(slot);
        }

        void Release(int slot) => onRelease?.Invoke(slot);

        if (onPress is not null || onRelease is not null)
        {
            btn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                Press(index);
                e.Handled = true;
            };
            btn.PreviewMouseLeftButtonUp += (_, _) => Release(index);

            btn.PreviewTouchDown += (_, e) =>
            {
                Press(index);
                e.Handled = true;
            };
            btn.PreviewTouchUp += (_, _) => Release(index);
        }
        else
        {
            btn.Click += (_, _) => onSelect?.Invoke(index);
        }

        if (onDoubleClick is not null)
        {
            btn.MouseDoubleClick += (_, _) => onDoubleClick(pattern);
        }

        return btn;
    }

    private static Brush Brush(FrameworkElement? host, string key, Color fallback)
    {
        if (host?.TryFindResource(key) is Brush b) return b;
        return new SolidColorBrush(fallback);
    }
}
