namespace Lumina.Core;

/// <summary>Stretch a laser timeline to the soundtrack length (MP3 or WAV), then snap clips/scenes onto the beat grid.</summary>
public static class RhythmSync
{
    public static string Align(LaserShow show, BeatGrid grid, TimeSpan audioDuration)
    {
        var laser = show.Timeline.Duration;
        var parts = new List<string> { $"{grid.Bpm:0.0} BPM", $"{grid.BeatCount} temps" };

        if (laser.TotalSeconds > 0.25 && audioDuration.TotalSeconds > 0.25)
        {
            var scale = audioDuration.TotalSeconds / laser.TotalSeconds;
            if (Math.Abs(scale - 1) > 0.015 && scale is >= 0.2 and <= 5)
            {
                Scale(show, scale);
                parts.Add($"durée laser × {scale:0.###}");
            }
        }

        Quantize(show, grid);
        parts.Add("clips et scènes calés sur le rythme");
        return string.Join(" · ", parts) + ".";
    }

    public static void Scale(LaserShow show, double scale)
    {
        scale = Math.Clamp(scale, 0.05, 10);
        foreach (var scene in show.Timeline.Scenes)
        {
            var duration = Math.Max(0.05, scene.Duration.TotalSeconds * scale);
            scene.Start = TimeSpan.FromSeconds(Math.Max(0, scene.Start.TotalSeconds * scale));
            scene.End = scene.Start + TimeSpan.FromSeconds(duration);
        }

        foreach (var clip in show.Timeline.Clips)
        {
            var duration = Math.Max(0.05, clip.Duration.TotalSeconds * scale);
            clip.Start = TimeSpan.FromSeconds(Math.Max(0, clip.Start.TotalSeconds * scale));
            clip.End = clip.Start + TimeSpan.FromSeconds(duration);
        }
    }

    public static void Quantize(LaserShow show, BeatGrid grid)
    {
        var min = grid.BeatPeriod;
        if (min < TimeSpan.FromMilliseconds(80)) min = TimeSpan.FromMilliseconds(80);

        foreach (var scene in show.Timeline.Scenes)
        {
            scene.Start = grid.Snap(scene.Start);
            var end = grid.Snap(scene.End);
            if (end <= scene.Start) end = scene.Start + min;
            scene.End = end;
        }

        foreach (var clip in show.Timeline.Clips)
        {
            clip.Start = grid.Snap(clip.Start);
            var end = grid.Snap(clip.End);
            if (end <= clip.Start) end = clip.Start + min;
            clip.End = end;
        }
    }
}
