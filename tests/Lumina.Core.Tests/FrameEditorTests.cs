using Lumina.Core;
using Xunit;

namespace Lumina.Core.Tests;

public class FrameEditorTests
{
    // ── Session creation ─────────────────────────────────────────────────

    [Fact]
    public void CreateEmpty_has_one_frame_and_no_points()
    {
        var s = FrameEditSession.CreateEmpty("Test");
        Assert.Single(s.Frames);
        Assert.Equal("Test", s.Frames[0].Name);
        Assert.Empty(s.Frames[0].Points);
        Assert.Equal(0, s.CurrentIndex);
    }

    [Fact]
    public void FromPattern_copies_all_frames_and_points()
    {
        var pattern = new PatternItem
        {
            Id = "abc",
            Name = "Pattern",
            Frames =
            [
                new LaserFrame
                {
                    Name = "F1",
                    Points = [new LaserPoint(100, 200, 0, 255, 0, 0, 255, false)],
                    Format = 5
                }
            ]
        };
        var s = FrameEditSession.FromPattern(pattern);
        Assert.Single(s.Frames);
        Assert.Single(s.Frames[0].Points);
        Assert.Equal(100, s.Frames[0].Points[0].X);
    }

    // ── Point operations ─────────────────────────────────────────────────

    [Fact]
    public void AddPoint_increments_point_count_and_marks_dirty()
    {
        var s = FrameEditSession.CreateEmpty();
        s.AddPoint(1000, -500, 255, 255, 255);
        Assert.Single(s.CurrentFrame!.Points);
        Assert.True(s.IsDirty);
    }

    [Fact]
    public void RemovePoint_decrements_point_count()
    {
        var s = FrameEditSession.CreateEmpty();
        var p = s.AddPoint(0, 0, 255, 255, 255);
        s.AddPoint(100, 100, 255, 0, 0);
        s.RemovePoint(p);
        Assert.Single(s.CurrentFrame!.Points);
    }

    [Fact]
    public void MovePoint_updates_coordinates()
    {
        var s = FrameEditSession.CreateEmpty();
        var p = s.AddPoint(0, 0, 255, 255, 255);
        s.MovePoint(p, 1234, -5678);
        Assert.Equal(1234, p.X);
        Assert.Equal(-5678, p.Y);
    }

    [Fact]
    public void ClearPoints_empties_frame()
    {
        var s = FrameEditSession.CreateEmpty();
        s.AddPoint(0, 0, 255, 255, 255);
        s.AddPoint(100, 100, 0, 255, 0);
        s.ClearPoints();
        Assert.Empty(s.CurrentFrame!.Points);
    }

    [Fact]
    public void ToggleBlank_flips_blanked_flag()
    {
        var s = FrameEditSession.CreateEmpty();
        var p = s.AddPoint(0, 0, 255, 255, 255, blanked: false);
        Assert.False(p.Blanked);
        s.ToggleBlank(p);
        Assert.True(p.Blanked);
        Assert.Equal(0, p.Intensity);
        s.ToggleBlank(p);
        Assert.False(p.Blanked);
    }

    // ── Frame management ─────────────────────────────────────────────────

    [Fact]
    public void AddFrame_appends_and_selects()
    {
        var s = FrameEditSession.CreateEmpty();
        s.AddFrame("F2");
        Assert.Equal(2, s.Frames.Count);
        Assert.Equal(1, s.CurrentIndex);
    }

    [Fact]
    public void DuplicateCurrentFrame_clones_points()
    {
        var s = FrameEditSession.CreateEmpty();
        s.AddPoint(10, 20, 255, 0, 0);
        s.DuplicateCurrentFrame();
        Assert.Equal(2, s.Frames.Count);
        Assert.Single(s.CurrentFrame!.Points);
        Assert.Equal(10, s.CurrentFrame.Points[0].X);
    }

    [Fact]
    public void DeleteCurrentFrame_requires_at_least_one_frame()
    {
        var s = FrameEditSession.CreateEmpty();
        s.DeleteCurrentFrame();
        Assert.Single(s.Frames);
    }

    [Fact]
    public void MoveFrameUp_and_Down_reorder()
    {
        var s = FrameEditSession.CreateEmpty("A");
        s.AddFrame("B");
        s.MoveFrameUp();
        Assert.Equal("B", s.Frames[0].Name);
        Assert.Equal("A", s.Frames[1].Name);
        s.MoveFrameDown();
        Assert.Equal("A", s.Frames[0].Name);
    }

    // ── Undo / Redo ──────────────────────────────────────────────────────

    [Fact]
    public void Undo_removes_last_added_point()
    {
        var s = FrameEditSession.CreateEmpty();
        s.AddPoint(0, 0, 255, 255, 255);
        Assert.Single(s.CurrentFrame!.Points);
        s.Undo();
        Assert.Empty(s.CurrentFrame!.Points);
        Assert.True(s.CanRedo);
    }

    [Fact]
    public void Redo_reapplies_undone_change()
    {
        var s = FrameEditSession.CreateEmpty();
        s.AddPoint(0, 0, 255, 255, 255);
        s.Undo();
        s.Redo();
        Assert.Single(s.CurrentFrame!.Points);
    }

    [Fact]
    public void New_edit_after_undo_clears_redo_stack()
    {
        var s = FrameEditSession.CreateEmpty();
        s.AddPoint(0, 0, 255, 255, 255);
        s.Undo();
        Assert.True(s.CanRedo);
        s.AddPoint(100, 100, 0, 255, 0);
        Assert.False(s.CanRedo);
    }

    // ── Export ───────────────────────────────────────────────────────────

    [Fact]
    public void ToPatternItem_roundtrip_preserves_points()
    {
        var s = FrameEditSession.CreateEmpty("FTest");
        s.AddPoint(123, -456, 0, 200, 100);
        var item = s.ToPatternItem("MyMotif");
        Assert.Equal("MyMotif", item.Name);
        Assert.Single(item.Frames);
        Assert.Single(item.Frames[0].Points);
        var p = item.Frames[0].Points[0];
        Assert.Equal(123, p.X);
        Assert.Equal(-456, p.Y);
        Assert.Equal(0, p.R);
        Assert.Equal(200, p.G);
        Assert.Equal(100, p.B);
    }

    [Fact]
    public void ToLaserFrame_sets_format_5()
    {
        var f = new EditableFrame { Name = "X" };
        f.Points.Add(new EditablePoint { X = 10, Y = 20, R = 255, G = 0, B = 0, Intensity = 255 });
        var lf = f.ToLaserFrame(0, 1);
        Assert.Equal(5, lf.Format);
    }

    [Fact]
    public void IldaWriter_roundtrip_byte_array()
    {
        var s = FrameEditSession.CreateEmpty("W");
        s.AddPoint(1000, 2000, 255, 128, 64);
        var item = s.ToPatternItem("ExportTest");
        var bytes = IldaWriter.Write(item.Frames);
        Assert.NotEmpty(bytes);
        // ILDA magic at start
        Assert.Equal((byte)'I', bytes[0]);
        Assert.Equal((byte)'L', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'A', bytes[3]);
    }
}
