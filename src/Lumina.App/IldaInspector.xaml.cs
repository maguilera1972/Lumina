using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Lumina.Core;

namespace Lumina.App;

public partial class IldaInspector : UserControl
{
    private IReadOnlyList<LaserFrame> _frames = [];
    private bool _suppress;

    public ObservableCollection<IldaFrameRow> Rows { get; } = [];

    public IldaInspector()
    {
        InitializeComponent();
        FrameGrid.ItemsSource = Rows;
    }

    public void Load(string title, IReadOnlyList<LaserFrame> frames)
    {
        _frames = frames;
        HeaderText.Text = $"{title}  —  {frames.Count} frame(s)";
        Rows.Clear();
        for (var i = 0; i < frames.Count; i++)
        {
            Rows.Add(new IldaFrameRow(IldaInspection.Summarize(frames[i], i)));
        }

        FrameSlider.Maximum = Math.Max(0, frames.Count - 1);
        if (frames.Count > 0)
        {
            SelectIndex(0);
        }
        else
        {
            FramePreview.Clear();
            StatsText.Text = "Aucune frame.";
        }
    }

    public void Clear()
    {
        Load("Aucun fichier ILDA", []);
    }

    private void FrameGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (FrameGrid.SelectedItem is IldaFrameRow row)
        {
            SelectIndex(row.Index);
        }
    }

    private void FrameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppress) return;
        SelectIndex((int)e.NewValue);
    }

    private void SelectIndex(int index)
    {
        if (_frames.Count == 0) return;
        index = Math.Clamp(index, 0, _frames.Count - 1);
        _suppress = true;
        FrameSlider.Value = index;
        if (index < Rows.Count)
        {
            FrameGrid.SelectedIndex = index;
            FrameGrid.ScrollIntoView(Rows[index]);
        }

        _suppress = false;
        var frame = _frames[index];
        FramePreview.ShowFrame(frame);
        var s = IldaInspection.Summarize(frame, index);
        StatsText.Text =
            $"{s.Name}  ({s.FormatLabel})\n" +
            $"Points {s.PointCount}  visibles {s.VisibleCount}  blank {s.BlankedCount}\n" +
            $"{s.BoundsText}\n" +
            $"Projecteur {s.Projector}  n° {s.FrameNumber}/{s.TotalFrames}";
    }
}

public sealed class IldaFrameRow
{
    public IldaFrameRow(IldaFrameSummary summary)
    {
        Index = summary.Index;
        IndexDisplay = summary.Index + 1;
        Name = summary.Name;
        FormatLabel = summary.FormatLabel;
        PointCount = summary.PointCount;
        VisibleCount = summary.VisibleCount;
        BlankedCount = summary.BlankedCount;
    }

    public int Index { get; }
    public int IndexDisplay { get; }
    public string Name { get; }
    public string FormatLabel { get; }
    public int PointCount { get; }
    public int VisibleCount { get; }
    public int BlankedCount { get; }
}
