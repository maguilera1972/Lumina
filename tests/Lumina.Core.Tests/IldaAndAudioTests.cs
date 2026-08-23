using Lumina.Core;
using Xunit;

namespace Lumina.Core.Tests;

public class IldaRoundtripTests
{
    [Fact]
    public void Writes_and_reads_true_color_frame()
    {
        var original = new LaserFrame
        {
            Name = "TEST",
            Points =
            [
                new LaserPoint(-1000, 0, 0, 255, 0, 0, 255, false),
                new LaserPoint(1000, 2000, 0, 0, 255, 0, 255, false),
                new LaserPoint(0, -2000, 0, 0, 0, 255, 255, true)
            ]
        };

        var bytes = IldaWriter.Write([original]);
        using var stream = new MemoryStream(bytes);
        var frames = IldaReader.Read(stream);

        Assert.Single(frames);
        Assert.Equal(3, frames[0].Points.Count);
        Assert.Equal(original.Points[0].X, frames[0].Points[0].X);
        Assert.Equal(original.Points[0].R, frames[0].Points[0].R);
        Assert.True(frames[0].Points[2].Blanked);
    }
}

public class AudioLocatorTests
{
    [Fact]
    public void Finds_sibling_mp3_with_same_basename()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-audio");
        try
        {
            var show = Path.Combine(dir.FullName, "ABBA.xrs");
            var mp3 = Path.Combine(dir.FullName, "ABBA.mp3");
            File.WriteAllText(show, "dummy");
            File.WriteAllBytes(mp3, [0x49, 0x44, 0x33]); // "ID3"

            var cue = AudioLocator.Locate(show);
            Assert.Equal(AudioStatus.Ready, cue.Status);
            Assert.Equal(mp3, cue.ResolvedPath);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Finds_sibling_wav_with_same_basename()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-audio-wav");
        try
        {
            var show = Path.Combine(dir.FullName, "Show.xrs");
            var wav = Path.Combine(dir.FullName, "Show.wav");
            File.WriteAllText(show, "dummy");
            File.WriteAllBytes(wav, "RIFF"u8.ToArray());

            var cue = AudioLocator.Locate(show);
            Assert.Equal(AudioStatus.Ready, cue.Status);
            Assert.Equal(wav, cue.ResolvedPath);
            Assert.Equal("WAV", AudioCue.FormatLabel(wav));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Accepts_mp3_and_wav_as_supported_audio()
    {
        Assert.True(AudioCue.IsSupportedFile(@"C:\mix\track.mp3"));
        Assert.True(AudioCue.IsSupportedFile(@"C:\mix\track.WAV"));
        Assert.False(AudioCue.IsSupportedFile(@"C:\mix\notes.txt"));
        Assert.Contains("*.wav", AudioCue.OpenDialogFilter, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sidecar_remembers_link_when_file_returns_later()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-sidecar");
        try
        {
            var show = Path.Combine(dir.FullName, "Tonight.xrs");
            var mp3 = Path.Combine(dir.FullName, "Tonight.mp3");
            File.WriteAllText(show, "dummy");
            File.WriteAllBytes(mp3, [1, 2, 3]);

            AudioLocator.Link(show, mp3, TimeSpan.FromMilliseconds(120));
            File.Delete(mp3);

            var missing = AudioLocator.Locate(show);
            Assert.Equal(AudioStatus.MissingFile, missing.Status);

            File.WriteAllBytes(mp3, [1, 2, 3]);
            var ready = AudioLocator.Locate(show);
            Assert.Equal(AudioStatus.Ready, ready.Status);
            Assert.Equal(120, ready.Offset.TotalMilliseconds);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Sidecar_path_is_remembered_but_not_ready_until_loaded()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-known");
        try
        {
            var show = Path.Combine(dir.FullName, "Tonight.xrs");
            var mp3 = Path.Combine(dir.FullName, "Tonight.mp3");
            File.WriteAllText(show, "dummy");
            File.WriteAllBytes(mp3, [1, 2, 3]);
            AudioLocator.Link(show, mp3, TimeSpan.Zero);

            var cue = AudioLocator.FromSidecar(show);
            Assert.Equal(AudioStatus.Known, cue.Status);
            Assert.False(cue.IsPlayable);
            Assert.Equal(mp3, cue.ResolvedPath);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Opening_a_show_does_not_auto_attach_a_sibling_mp3()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-no-autoload");
        try
        {
            var showPath = Path.Combine(dir.FullName, "demo.ild");
            var mp3 = Path.Combine(dir.FullName, "demo.mp3");
            File.WriteAllText(showPath, "dummy");
            File.WriteAllBytes(mp3, [1, 2, 3]);

            var show = ShowLoader.FromFrames("demo", showPath, ShowKind.IldaAnimation, []);
            Assert.NotEqual(AudioStatus.Ready, show.Audio.Status);
            Assert.False(show.Audio.IsPlayable);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Reports_not_linked_when_no_audio_present()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-none");
        try
        {
            var show = Path.Combine(dir.FullName, "demo.xrs");
            File.WriteAllText(show, "dummy");
            var cue = AudioLocator.Locate(show);
            Assert.Equal(AudioStatus.NotLinked, cue.Status);
        }
        finally
        {
            dir.Delete(true);
        }
    }
}

public class ShowClockTests
{
    [Fact]
    public void Uses_internal_clock_without_audio()
    {
        var show = new LaserShow
        {
            Title = "t",
            Frames = [new LaserFrame { Name = "a", Points = [] }]
        };
        show.Timeline.ReplaceAll([new ShowScene { Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(10), FrameStart = 0, FrameEnd = 0 }]);
        var clock = new ShowClock();
        clock.Bind(show, audioDuration: null, audioPosition: null);
        Assert.False(clock.AudioIsMaster);
        clock.Start();
        Assert.True(clock.IsRunning);
        clock.Stop();
        Assert.Equal(TimeSpan.Zero, clock.Position);
    }

    [Fact]
    public void Follows_audio_position_when_master()
    {
        var show = new LaserShow
        {
            Title = "t",
            Frames = [new LaserFrame { Name = "a", Points = [] }]
        };
        show.Timeline.ReplaceAll([new ShowScene { Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(3), FrameStart = 0, FrameEnd = 0 }]);
        var pos = TimeSpan.FromSeconds(1.5);
        var clock = new ShowClock();
        clock.Bind(show, TimeSpan.FromSeconds(4), () => pos);
        Assert.True(clock.AudioIsMaster);
        Assert.Equal(TimeSpan.FromSeconds(4), clock.TimelineDuration);
        clock.Start();
        Assert.Equal(pos, clock.Position);
    }
}

public class TimelineTests
{
    [Fact]
    public void FrameAt_maps_time_to_scene_frames()
    {
        var frames = new[]
        {
            new LaserFrame { Name = "a", Points = [] },
            new LaserFrame { Name = "b", Points = [] },
            new LaserFrame { Name = "c", Points = [] }
        };
        var show = new LaserShow { Title = "t", Frames = frames };
        show.Timeline.ReplaceAll(
        [
            new ShowScene { Name = "one", Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(2), FrameStart = 0, FrameEnd = 0 },
            new ShowScene { Name = "two", Start = TimeSpan.FromSeconds(2), End = TimeSpan.FromSeconds(4), FrameStart = 1, FrameEnd = 2 }
        ]);

        Assert.Equal("a", show.FrameAt(TimeSpan.FromSeconds(0.5))!.Name);
        Assert.Equal("b", show.FrameAt(TimeSpan.FromSeconds(2.0))!.Name);
        Assert.Equal(TimeSpan.FromSeconds(4), show.Timeline.Duration);
    }

    [Fact]
    public void Compact_packs_scenes_without_gaps()
    {
        var timeline = new Timeline();
        timeline.AddScene(4, TimeSpan.FromSeconds(1));
        timeline.AddScene(4, TimeSpan.FromSeconds(2));
        timeline.Scenes[0].Start = TimeSpan.FromSeconds(3);
        timeline.Scenes[0].End = TimeSpan.FromSeconds(5);
        timeline.Compact();
        Assert.Equal(TimeSpan.Zero, timeline.Scenes[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(2), timeline.Scenes[0].Duration);
        Assert.Equal(TimeSpan.FromSeconds(2), timeline.Scenes[1].Start);
    }

    [Fact]
    public void Untitled_show_has_empty_library_and_can_receive_a_clip()
    {
        var show = ShowLoader.CreateUntitled();
        Assert.Equal(ShowKind.LuminaShow, show.Kind);
        Assert.Empty(show.Library.Items);
        var pattern = show.Library.Add("star", [new LaserFrame { Name = "s", Points = [] }], null);
        var clip = show.Timeline.PlaceClip(pattern, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        Assert.Equal("star", clip.Name);
        Assert.Equal(TimeSpan.FromSeconds(1), clip.Start);
    }

    [Fact]
    public void Sidecar_path_is_stable_for_lumina_project_files()
    {
        var project = @"C:\shows\Tonight.lumina.json";
        Assert.True(LuminaSidecar.IsProjectFile(project));
        Assert.Equal(project, LuminaSidecar.PathFor(project));
    }

    [Fact]
    public void Clip_with_effects_scales_points()
    {
        var frame = new LaserFrame
        {
            Name = "p",
            Points = [new LaserPoint(1000, 0, 0, 255, 0, 0, 255, false)]
        };
        var pattern = new PatternItem { Id = "p1", Name = "p", Frames = [frame] };
        var show = new LaserShow { Title = "t" };
        show.Library.Items.Add(pattern);
        show.Timeline.PlaceClip(pattern, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        show.Timeline.Clips[0].Effects.Width = 2;
        var outFrame = show.FrameAt(TimeSpan.FromSeconds(0.1))!;
        Assert.Equal(2000, outFrame.Points[0].X);
    }

    [Fact]
    public void Beat_grid_snaps_to_nearest_beat()
    {
        var grid = new BeatGrid { Bpm = 120, FirstBeat = TimeSpan.Zero, Duration = TimeSpan.FromSeconds(8) };
        Assert.Equal(TimeSpan.FromSeconds(0.5), grid.BeatPeriod);
        Assert.Equal(TimeSpan.FromSeconds(1), grid.Snap(TimeSpan.FromSeconds(0.9)));
        Assert.Equal(TimeSpan.Zero, grid.Snap(TimeSpan.FromSeconds(0.2)));
    }

    [Fact]
    public void Rhythm_sync_stretches_clip_to_audio_then_quantizes()
    {
        var show = new LaserShow { Title = "t" };
        var pattern = show.Library.Add("p", [new LaserFrame { Name = "a", Points = [] }], null);
        show.Timeline.PlaceClip(pattern, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        var grid = new BeatGrid { Bpm = 120, FirstBeat = TimeSpan.Zero, Duration = TimeSpan.FromSeconds(4) };
        RhythmSync.Align(show, grid, TimeSpan.FromSeconds(4));
        Assert.Equal(TimeSpan.FromSeconds(4), show.Timeline.Clips[0].Duration);
    }
}
