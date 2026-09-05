using System.Collections.ObjectModel;

namespace Lumina.Core;

/// <summary>One mutable ILDA-style point used during frame editing.</summary>
public sealed class EditablePoint
{
    public short X { get; set; }
    public short Y { get; set; }
    public short Z { get; set; }
    public byte R { get; set; } = 255;
    public byte G { get; set; } = 255;
    public byte B { get; set; } = 255;
    public byte Intensity { get; set; } = 255;
    public bool Blanked { get; set; }

    public EditablePoint() { }

    public EditablePoint(LaserPoint src)
    {
        X = src.X; Y = src.Y; Z = src.Z;
        R = src.R; G = src.G; B = src.B;
        Intensity = src.Intensity;
        Blanked = src.Blanked;
    }

    public EditablePoint Clone() => new()
    {
        X = X, Y = Y, Z = Z,
        R = R, G = G, B = B,
        Intensity = Intensity,
        Blanked = Blanked
    };

    public LaserPoint ToLaserPoint() =>
        new(X, Y, Z, R, G, B, Intensity, Blanked);
}

/// <summary>One mutable frame inside a <see cref="FrameEditSession"/>.</summary>
public sealed class EditableFrame
{
    public string Name { get; set; } = "Frame";
    public List<EditablePoint> Points { get; } = [];

    public EditableFrame() { }

    public EditableFrame(LaserFrame src)
    {
        Name = src.Name;
        foreach (var p in src.Points)
            Points.Add(new EditablePoint(p));
    }

    public EditableFrame Clone()
    {
        var f = new EditableFrame { Name = Name };
        foreach (var p in Points) f.Points.Add(p.Clone());
        return f;
    }

    public LaserFrame ToLaserFrame(int frameNumber = 0, int totalFrames = 1)
    {
        return new LaserFrame
        {
            Name = Name,
            FrameNumber = frameNumber,
            TotalFrames = totalFrames,
            Format = 5,
            Points = Points.Select(p => p.ToLaserPoint()).ToList()
        };
    }
}

/// <summary>
/// Editing session for a multi-frame laser pattern.
/// Supports full undo/redo via snapshot-based commands.
/// </summary>
public sealed class FrameEditSession
{
    private readonly List<List<EditableFrame>> _undoStack = [];
    private readonly List<List<EditableFrame>> _redoStack = [];

    public ObservableCollection<EditableFrame> Frames { get; } = [];
    public EditableFrame? CurrentFrame => CurrentIndex >= 0 && CurrentIndex < Frames.Count
        ? Frames[CurrentIndex]
        : null;

    private int _currentIndex;
    public int CurrentIndex
    {
        get => _currentIndex;
        set
        {
            _currentIndex = Math.Clamp(value, Frames.Count == 0 ? -1 : 0, Frames.Count - 1);
            CurrentFrameChanged?.Invoke();
        }
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public bool IsDirty { get; private set; }

    public event Action? CurrentFrameChanged;
    public event Action? FrameListChanged;
    public event Action? PointsChanged;

    // ── Factory methods ─────────────────────────────────────────────────────

    public static FrameEditSession CreateEmpty(string frameName = "Frame 1")
    {
        var s = new FrameEditSession();
        s.Frames.Add(new EditableFrame { Name = frameName });
        s._currentIndex = 0;
        return s;
    }

    public static FrameEditSession FromPattern(PatternItem pattern)
    {
        var s = new FrameEditSession();
        foreach (var f in pattern.Frames)
            s.Frames.Add(new EditableFrame(f));
        s._currentIndex = s.Frames.Count > 0 ? 0 : -1;
        return s;
    }

    // ── Frame management ────────────────────────────────────────────────────

    public EditableFrame AddFrame(string? name = null)
    {
        SaveUndo();
        var f = new EditableFrame { Name = name ?? $"Frame {Frames.Count + 1}" };
        Frames.Add(f);
        CurrentIndex = Frames.Count - 1;
        IsDirty = true;
        FrameListChanged?.Invoke();
        return f;
    }

    public void DuplicateCurrentFrame()
    {
        if (CurrentFrame is null) return;
        SaveUndo();
        var clone = CurrentFrame.Clone();
        clone.Name = CurrentFrame.Name + " (copie)";
        Frames.Insert(CurrentIndex + 1, clone);
        CurrentIndex++;
        IsDirty = true;
        FrameListChanged?.Invoke();
    }

    public void DeleteCurrentFrame()
    {
        if (CurrentFrame is null || Frames.Count <= 1) return;
        SaveUndo();
        Frames.RemoveAt(CurrentIndex);
        CurrentIndex = Math.Min(CurrentIndex, Frames.Count - 1);
        IsDirty = true;
        FrameListChanged?.Invoke();
    }

    public void MoveFrameUp()
    {
        if (CurrentIndex <= 0) return;
        SaveUndo();
        (Frames[CurrentIndex - 1], Frames[CurrentIndex]) = (Frames[CurrentIndex], Frames[CurrentIndex - 1]);
        CurrentIndex--;
        IsDirty = true;
        FrameListChanged?.Invoke();
    }

    public void MoveFrameDown()
    {
        if (CurrentIndex >= Frames.Count - 1) return;
        SaveUndo();
        (Frames[CurrentIndex], Frames[CurrentIndex + 1]) = (Frames[CurrentIndex + 1], Frames[CurrentIndex]);
        CurrentIndex++;
        IsDirty = true;
        FrameListChanged?.Invoke();
    }

    // ── Point editing ────────────────────────────────────────────────────────

    public EditablePoint AddPoint(short x, short y, byte r, byte g, byte b, bool blanked = false)
    {
        if (CurrentFrame is null) return new EditablePoint();
        SaveUndo();
        var pt = new EditablePoint { X = x, Y = y, R = r, G = g, B = b, Intensity = blanked ? (byte)0 : (byte)255, Blanked = blanked };
        CurrentFrame.Points.Add(pt);
        IsDirty = true;
        PointsChanged?.Invoke();
        return pt;
    }

    public void RemovePoint(EditablePoint pt)
    {
        if (CurrentFrame is null) return;
        SaveUndo();
        CurrentFrame.Points.Remove(pt);
        IsDirty = true;
        PointsChanged?.Invoke();
    }

    public void MovePoint(EditablePoint pt, short x, short y)
    {
        pt.X = x;
        pt.Y = y;
        IsDirty = true;
        PointsChanged?.Invoke();
    }

    public void SetPointColor(EditablePoint pt, byte r, byte g, byte b)
    {
        SaveUndo();
        pt.R = r; pt.G = g; pt.B = b;
        if (!pt.Blanked) pt.Intensity = 255;
        IsDirty = true;
        PointsChanged?.Invoke();
    }

    public void ToggleBlank(EditablePoint pt)
    {
        SaveUndo();
        pt.Blanked = !pt.Blanked;
        pt.Intensity = pt.Blanked ? (byte)0 : (byte)255;
        IsDirty = true;
        PointsChanged?.Invoke();
    }

    public void ClearPoints()
    {
        if (CurrentFrame is null || CurrentFrame.Points.Count == 0) return;
        SaveUndo();
        CurrentFrame.Points.Clear();
        IsDirty = true;
        PointsChanged?.Invoke();
    }

    // ── Undo / Redo ──────────────────────────────────────────────────────────

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        _redoStack.Add(Snapshot());
        RestoreSnapshot(_undoStack[^1]);
        _undoStack.RemoveAt(_undoStack.Count - 1);
        IsDirty = true;
        FrameListChanged?.Invoke();
        PointsChanged?.Invoke();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        _undoStack.Add(Snapshot());
        RestoreSnapshot(_redoStack[^1]);
        _redoStack.RemoveAt(_redoStack.Count - 1);
        IsDirty = true;
        FrameListChanged?.Invoke();
        PointsChanged?.Invoke();
    }

    // ── Export ───────────────────────────────────────────────────────────────

    /// <summary>Build a read-only <see cref="PatternItem"/> from the current session.</summary>
    public PatternItem ToPatternItem(string name, string? id = null)
    {
        var frames = Frames
            .Select((f, i) => f.ToLaserFrame(i, Frames.Count))
            .ToList();
        return new PatternItem
        {
            Id = id ?? Guid.NewGuid().ToString("N")[..12],
            Name = name,
            Frames = frames
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SaveUndo()
    {
        _undoStack.Add(Snapshot());
        if (_undoStack.Count > 100) _undoStack.RemoveAt(0);
        _redoStack.Clear();
    }

    private List<EditableFrame> Snapshot() =>
        Frames.Select(f => f.Clone()).ToList();

    private void RestoreSnapshot(List<EditableFrame> snap)
    {
        Frames.Clear();
        foreach (var f in snap) Frames.Add(f);
        _currentIndex = Math.Clamp(_currentIndex, 0, Math.Max(0, Frames.Count - 1));
    }
}
