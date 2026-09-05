using Lumina.Core;
using Xunit;

namespace Lumina.Core.Tests;

public class SidecarResilienceTests
{
    [Fact]
    public void ApplyTo_does_not_throw_when_pattern_source_is_mamba_container()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumina-sidecar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var ild = Path.Combine(dir, "shape.ild");
            var mrs = Path.Combine(dir, "show.mrs");
            // Minimal single-frame ILDA format 5
            IldaWriter.WriteFile(ild,
            [
                new LaserFrame
                {
                    Name = "S",
                    Format = 5,
                    Points = [new LaserPoint(0, 0, 0, 255, 0, 0, 255, false)]
                }
            ]);
            File.WriteAllBytes(mrs, "not-an-ilda-file"u8.ToArray());

            var show = ShowLoader.FromFrames("show", mrs, ShowKind.MambaShow,
                IldaReader.Read(ild), TimeSpan.FromSeconds(2));
            // Simulate the old bug: pattern SourcePath points at the .mrs container.
            show.Library.Items[0].SourcePath = mrs;

            var sidecar = new LuminaSidecar
            {
                Patterns =
                [
                    new SidecarPattern
                    {
                        Id = show.Library.Items[0].Id,
                        Name = show.Library.Items[0].Name,
                        SourcePath = mrs // old bug: container path
                    }
                ],
                Clips = [SidecarClip.From(show.Timeline.Clips[0])]
            };
            sidecar.Save(mrs);

            // Re-open path: primary load then ApplyTo must not throw.
            var reopened = new LaserShow
            {
                Title = "show",
                SourcePath = mrs,
                Kind = ShowKind.MambaShow,
                Frames = IldaReader.Read(ild).ToList()
            };
            var pattern = reopened.Library.Add("show", reopened.Frames, ild);
            reopened.Timeline.PlaceClip(pattern, TimeSpan.Zero, TimeSpan.FromSeconds(2));

            LuminaSidecar.ApplyTo(reopened);

            Assert.True(reopened.Library.Items.Count > 0);
            Assert.True(reopened.Frames.Count > 0);
            Assert.NotNull(reopened.FrameAt(TimeSpan.FromSeconds(0.1)));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* temp cleanup */ }
        }
    }

    [Fact]
    public void SidecarPattern_From_strips_non_frame_sources()
    {
        var item = new PatternItem
        {
            Id = "abc",
            Name = "INFINITY",
            SourcePath = @"C:\shows\INFINITY.mrs",
            Frames = []
        };
        var saved = SidecarPattern.From(item);
        Assert.Null(saved.SourcePath);
    }
}
