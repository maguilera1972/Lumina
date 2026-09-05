namespace Lumina.Core;

/// <summary>Editable show timeline: ILDA clips on audio time, plus legacy Mamba scenes.</summary>
public sealed class Timeline
{
    public List<ShowScene> Scenes { get; } = [];
    public List<TimelineClip> Clips { get; } = [];

    public TimeSpan Duration
    {
        get
        {
            var max = TimeSpan.Zero;
            foreach (var scene in Scenes)
            {
                if (scene.End > max) max = scene.End;
            }

            foreach (var clip in Clips)
            {
                if (clip.End > max) max = clip.End;
            }

            return max;
        }
    }

    public void Clear()
    {
        Scenes.Clear();
        Clips.Clear();
    }

    public void ReplaceAll(IEnumerable<ShowScene> scenes)
    {
        Scenes.Clear();
        Scenes.AddRange(scenes);
    }

    public TimelineClip PlaceClip(PatternItem pattern, TimeSpan start, TimeSpan? duration = null, int track = 0)
    {
        var length = duration ?? TimeSpan.FromSeconds(Math.Max(1.5, pattern.Frames.Count / 25.0));
        var clip = new TimelineClip
        {
            PatternId = pattern.Id,
            Name = pattern.Name,
            Start = start < TimeSpan.Zero ? TimeSpan.Zero : start,
            End = start + length,
            Track = track
        };
        Clips.Add(clip);
        return clip;
    }

    public ShowScene AddScene(int frameCount, TimeSpan? duration = null)
    {
        var start = Duration;
        var length = duration ?? TimeSpan.FromSeconds(2);
        var scene = new ShowScene
        {
            Name = $"Scène {Scenes.Count + 1}",
            Start = start,
            End = start + length,
            FrameStart = 0,
            FrameEnd = Math.Max(0, frameCount - 1)
        };
        Scenes.Add(scene);
        return scene;
    }

    public bool Remove(ShowScene scene) => Scenes.Remove(scene);
    public bool Remove(TimelineClip clip) => Clips.Remove(clip);

    public void Move(ShowScene scene, int delta)
    {
        var i = Scenes.IndexOf(scene);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= Scenes.Count) return;
        (Scenes[i], Scenes[j]) = (Scenes[j], Scenes[i]);
        Compact();
    }

    public void Compact()
    {
        var t = TimeSpan.Zero;
        foreach (var scene in Scenes)
        {
            var d = scene.Duration;
            if (d < TimeSpan.FromMilliseconds(50)) d = TimeSpan.FromMilliseconds(50);
            scene.Start = t;
            scene.End = t + d;
            t = scene.End;
        }
    }

    public TimelineClip? ClipAt(TimeSpan time)
    {
        TimelineClip? best = null;
        foreach (var clip in Clips)
        {
            if (time >= clip.Start && time < clip.End)
            {
                if (best is null || clip.Track >= best.Track) best = clip;
            }
        }

        return best;
    }

    public ShowScene? SceneAt(TimeSpan time)
    {
        foreach (var scene in Scenes)
        {
            if (time >= scene.Start && time < scene.End) return scene;
        }

        foreach (var scene in Scenes)
        {
            if (time >= scene.Start && time <= scene.End) return scene;
        }

        return null;
    }

    public LaserFrame? FrameAt(TimeSpan time, LaserShow show)
    {
        var clip = ClipAt(time);
        if (clip is not null)
        {
            var pattern = show.Library.Get(clip.PatternId);
            var source = pattern?.FrameAtProgress(clip.ProgressAt(time));
            if (source is not null)
            {
                return EffectProcessor.Apply(source, clip.Effects);
            }
            // Clip présent mais motif introuvable : retomber sur les frames du show.
        }

        if (show.Frames.Count == 0) return null;
        if (Scenes.Count == 0)
        {
            var i = (int)Math.Clamp(time.TotalSeconds * 25, 0, show.Frames.Count - 1);
            return show.Frames[i];
        }

        var scene = SceneAt(time);
        if (scene is null) return null;

        var span = scene.Duration.TotalSeconds;
        var local = span <= 0 ? 0 : (time - scene.Start).TotalSeconds / span;
        var count = Math.Max(1, scene.FrameEnd - scene.FrameStart + 1);
        var index = scene.FrameStart + (int)Math.Clamp(Math.Floor(local * count), 0, count - 1);
        return show.Frames[Math.Clamp(index, 0, show.Frames.Count - 1)];
    }

    /// <summary>
    /// Resolves the frame for preview/playback. When audio is longer than the laser timeline,
    /// loops the laser content so the preview keeps animating instead of going blank.
    /// </summary>
    public LaserFrame? FrameAtLooped(TimeSpan time, LaserShow show)
    {
        var content = Duration;
        if (content > TimeSpan.Zero && time >= content)
        {
            var ticks = content.Ticks;
            if (ticks > 0)
            {
                time = TimeSpan.FromTicks(time.Ticks % ticks);
            }
            else
            {
                time = TimeSpan.Zero;
            }
        }

        return FrameAt(time, show) ?? FrameAt(TimeSpan.Zero, show);
    }

    public int FrameIndexAt(TimeSpan time, LaserShow show)
    {
        var content = Duration;
        var t = time;
        if (content > TimeSpan.Zero && t >= content)
        {
            var ticks = content.Ticks;
            if (ticks > 0) t = TimeSpan.FromTicks(t.Ticks % ticks);
        }

        var clip = ClipAt(t);
        if (clip is not null)
        {
            var pattern = show.Library.Get(clip.PatternId);
            if (pattern is { Frames.Count: > 0 })
            {
                return (int)Math.Clamp(Math.Floor(clip.ProgressAt(t) * pattern.Frames.Count), 0, pattern.Frames.Count - 1);
            }
        }

        if (show.Frames.Count == 0) return 0;
        if (Scenes.Count == 0)
        {
            return (int)Math.Clamp(t.TotalSeconds * 25, 0, show.Frames.Count - 1);
        }

        var scene = SceneAt(t);
        if (scene is null) return 0;
        var span = scene.Duration.TotalSeconds;
        var local = span <= 0 ? 0 : (t - scene.Start).TotalSeconds / span;
        var count = Math.Max(1, scene.FrameEnd - scene.FrameStart + 1);
        return scene.FrameStart + (int)Math.Clamp(Math.Floor(local * count), 0, count - 1);
    }
}
