using Lumina.Core;
using Xunit;

namespace Lumina.Core.Tests;

public class TimelinePreviewTests
{
    private static LaserFrame F(int n, byte color = 255) => new()
    {
        Name = "F" + n,
        FrameNumber = n,
        TotalFrames = 10,
        Format = 5,
        Points = [new LaserPoint((short)n, 0, 0, color, 0, 0, 255, false)]
    };

    [Fact]
    public void FrameIndex_advances_with_clip_progress()
    {
        var frames = Enumerable.Range(0, 100).Select(i => F(i)).ToList();
        var show = ShowLoader.FromFrames("T", null, ShowKind.IldaAnimation, frames, TimeSpan.FromSeconds(10));
        Assert.Equal(0, show.FrameIndexAt(TimeSpan.Zero));
        Assert.InRange(show.FrameIndexAt(TimeSpan.FromSeconds(5)), 45, 55);
        Assert.InRange(show.FrameIndexAt(TimeSpan.FromSeconds(9.9)), 90, 99);
    }

    [Fact]
    public void FrameAt_loops_when_audio_time_exceeds_laser_timeline()
    {
        var frames = Enumerable.Range(0, 50).Select(i => F(i)).ToList();
        var show = ShowLoader.FromFrames("T", null, ShowKind.IldaAnimation, frames, TimeSpan.FromSeconds(5));
        var atStart = show.FrameAtForPreview(TimeSpan.Zero);
        var looped = show.FrameAtForPreview(TimeSpan.FromSeconds(5)); // exactly at end → loops to 0
        var mid = show.FrameAtForPreview(TimeSpan.FromSeconds(2.5));
        var midLooped = show.FrameAtForPreview(TimeSpan.FromSeconds(7.5)); // 7.5 % 5 = 2.5

        Assert.NotNull(atStart);
        Assert.NotNull(looped);
        Assert.NotNull(mid);
        Assert.NotNull(midLooped);
        Assert.Equal(atStart!.Points[0].X, looped!.Points[0].X);
        Assert.Equal(mid!.Points[0].X, midLooped!.Points[0].X);
    }

    [Fact]
    public void Missing_pattern_falls_back_to_show_frames()
    {
        var frames = Enumerable.Range(0, 20).Select(i => F(i)).ToList();
        var show = ShowLoader.FromFrames("T", null, ShowKind.IldaAnimation, frames, TimeSpan.FromSeconds(4));
        show.Timeline.Clips[0].PatternId = "does-not-exist";
        var frame = show.Timeline.FrameAt(TimeSpan.FromSeconds(1), show);
        Assert.NotNull(frame);
    }
}
