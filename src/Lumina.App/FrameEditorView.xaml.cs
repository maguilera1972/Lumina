using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lumina.Core;
using Microsoft.Win32;

namespace Lumina.App;

/// <summary>Color swatch item shown in the toolbar palette strip.</summary>
public sealed record ColorSwatch(string Name, Color Color);

/// <summary>
/// Full frame editor panel: toolbar, frame list, interactive canvas, point inspector.
/// </summary>
public partial class FrameEditorView : UserControl
{
    private FrameEditSession _session = FrameEditSession.CreateEmpty();
    private bool _refreshingList;
    private bool _refreshingName;

    public event Action<PatternItem>? SaveRequested;

    // Laser colour swatches available in the toolbar
    private static readonly ColorSwatch[] Swatches =
    [
        new("Blanc",    Color.FromRgb(255, 255, 255)),
        new("Cyan",     Color.FromRgb(0,   229, 255)),
        new("Vert",     Color.FromRgb(0,   255, 80)),
        new("Jaune",    Color.FromRgb(255, 230, 0)),
        new("Rouge",    Color.FromRgb(255, 30,  30)),
        new("Rose",     Color.FromRgb(255, 0,   180)),
        new("Violet",   Color.FromRgb(160, 0,   255)),
        new("Bleu",     Color.FromRgb(30,  80,  255)),
    ];

    public FrameEditorView()
    {
        InitializeComponent();
        SwatchList.ItemsSource = Swatches;
        SetActiveColor(Swatches[0].Color);
        Bind(_session);

        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Z &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            {
                _session.Undo(); RefreshUndoState();
            }
            else if ((e.Key == System.Windows.Input.Key.Y || e.Key == System.Windows.Input.Key.Z) &&
                     (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0 &&
                     (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0)
            {
                _session.Redo(); RefreshUndoState();
            }
        };
        Focusable = true;
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void NewSession(string firstFrameName = "Frame 1")
    {
        Bind(FrameEditSession.CreateEmpty(firstFrameName));
    }

    public void LoadPattern(PatternItem pattern)
    {
        Bind(FrameEditSession.FromPattern(pattern));
    }

    // ── Binding ───────────────────────────────────────────────────────────

    private void Bind(FrameEditSession session)
    {
        _session = session;
        _session.FrameListChanged += RefreshFrameList;
        _session.CurrentFrameChanged += RefreshFrameList;
        _session.PointsChanged += RefreshPointInfo;

        EditorCanvas.Bind(_session);
        RefreshFrameList();
        RefreshPointInfo();
        RefreshUndoState();
    }

    private void RefreshFrameList()
    {
        _refreshingList = true;
        FrameListBox.Items.Clear();
        foreach (var f in _session.Frames)
        {
            FrameListBox.Items.Add($"{f.Name}  ({f.Points.Count} pts)");
        }
        if (_session.CurrentIndex >= 0 && _session.CurrentIndex < FrameListBox.Items.Count)
            FrameListBox.SelectedIndex = _session.CurrentIndex;
        _refreshingList = false;

        RefreshPointInfo();
    }

    private void RefreshPointInfo()
    {
        var f = _session.CurrentFrame;
        PointCountText.Text = f is null ? "" : $"{f.Points.Count} points";
        if (f is not null)
        {
            _refreshingName = true;
            if (FrameNameBox.Text != f.Name) FrameNameBox.Text = f.Name;
            _refreshingName = false;
        }
        // Update mini preview
        MiniPreview.ShowFrame(f?.ToLaserFrame());
        // Status bar
        StatusBar.Text = $"Frame {_session.CurrentIndex + 1}/{_session.Frames.Count}" +
                         (_session.IsDirty ? "  •  modifié" : "");
    }

    private void RefreshUndoState()
    {
        UndoBtn.IsEnabled = _session.CanUndo;
        RedoBtn.IsEnabled = _session.CanRedo;
    }

    private void SetActiveColor(Color c)
    {
        EditorCanvas.PenColor = c;
        ActiveColorRect.Fill = new SolidColorBrush(c);
    }

    // ── Toolbar events ────────────────────────────────────────────────────

    private void PenBtn_Checked(object s, RoutedEventArgs e)
    {
        EditorCanvas.ActiveTool = EditorTool.Pen;
        MoveBtn.IsChecked = EraseBtn.IsChecked = false;
        EditorCanvas.Cursor = System.Windows.Input.Cursors.Cross;
    }

    private void MoveBtn_Checked(object s, RoutedEventArgs e)
    {
        EditorCanvas.ActiveTool = EditorTool.Move;
        PenBtn.IsChecked = EraseBtn.IsChecked = false;
        EditorCanvas.Cursor = System.Windows.Input.Cursors.Arrow;
    }

    private void EraseBtn_Checked(object s, RoutedEventArgs e)
    {
        EditorCanvas.ActiveTool = EditorTool.Erase;
        PenBtn.IsChecked = MoveBtn.IsChecked = false;
        EditorCanvas.Cursor = System.Windows.Input.Cursors.No;
    }

    private void BlankedCheck_Changed(object s, RoutedEventArgs e)
    {
        EditorCanvas.BlankedMode = BlankedCheck.IsChecked == true;
    }

    private void Swatch_Click(object s, RoutedEventArgs e)
    {
        if (((Button)s).Tag is ColorSwatch sw)
            SetActiveColor(sw.Color);
    }

    private void Undo_Click(object s, RoutedEventArgs e) { _session.Undo(); RefreshUndoState(); }
    private void Redo_Click(object s, RoutedEventArgs e) { _session.Redo(); RefreshUndoState(); }

    private void ClearPoints_Click(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show("Effacer tous les points de cette frame ?", "Effacer",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            _session.ClearPoints();
    }

    // ── Frame list events ─────────────────────────────────────────────────

    private void FrameListBox_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (_refreshingList) return;
        _session.CurrentIndex = FrameListBox.SelectedIndex;
    }

    private void AddFrame_Click(object s, RoutedEventArgs e) => _session.AddFrame();
    private void DupFrame_Click(object s, RoutedEventArgs e) => _session.DuplicateCurrentFrame();
    private void DelFrame_Click(object s, RoutedEventArgs e) => _session.DeleteCurrentFrame();
    private void FrameUp_Click(object s, RoutedEventArgs e) => _session.MoveFrameUp();
    private void FrameDown_Click(object s, RoutedEventArgs e) => _session.MoveFrameDown();

    private void FrameNameBox_TextChanged(object s, TextChangedEventArgs e)
    {
        if (_refreshingName || _session.CurrentFrame is null) return;
        _session.CurrentFrame.Name = FrameNameBox.Text;
        StatusBar.Text = $"Frame {_session.CurrentIndex + 1}/{_session.Frames.Count}  •  modifié";
    }

    // ── Export ────────────────────────────────────────────────────────────

    private void SaveToLibrary_Click(object s, RoutedEventArgs e)
    {
        var name = _session.Frames.Count > 0
            ? _session.Frames[0].Name
            : "Nouveau motif";

        var dlg = new RenameDialog(name) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        var pattern = _session.ToPatternItem(dlg.ResultName);
        SaveRequested?.Invoke(pattern);
        StatusBar.Text = $"Motif « {dlg.ResultName} » sauvegardé dans la bibliothèque.";
        _session = FrameEditSession.FromPattern(pattern);
        Bind(_session);
    }

    private void ExportIlda_Click(object s, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Fichiers ILDA (*.ild)|*.ild|Tous les fichiers (*.*)|*.*",
            DefaultExt = ".ild",
            FileName = _session.Frames.FirstOrDefault()?.Name ?? "export"
        };
        if (dlg.ShowDialog() != true) return;

        var pattern = _session.ToPatternItem("export");
        var frames = pattern.Frames;
        IldaWriter.WriteFile(dlg.FileName, frames);
        StatusBar.Text = $"Exporté : {System.IO.Path.GetFileName(dlg.FileName)}  ({frames.Count} frames)";
    }
}
