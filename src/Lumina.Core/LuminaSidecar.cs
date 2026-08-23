using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumina.Core;

/// <summary>Native Lumina project sidecar: audio, library ILDA, clips and effects.</summary>
public sealed class LuminaSidecar
{
    public const string Extension = ".lumina.json";

    public int Version { get; set; } = 2;
    public string? AudioPath { get; set; }
    public double AudioOffsetMs { get; set; }
    public double Volume { get; set; } = 1.0;
    public List<SidecarScene> Scenes { get; set; } = [];
    public List<SidecarPattern> Patterns { get; set; } = [];
    public List<SidecarClip> Clips { get; set; } = [];
    public ColorConversionProfile? Colors { get; set; }

    public static bool IsProjectFile(string path) =>
        path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    public static string PathFor(string showPath)
    {
        if (IsProjectFile(showPath)) return showPath;
        return Path.Combine(
            Path.GetDirectoryName(showPath) ?? ".",
            Path.GetFileNameWithoutExtension(showPath) + Extension);
    }

    public static LuminaSidecar? TryLoad(string showPath)
    {
        var path = PathFor(showPath);
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, LuminaJson.Default.LuminaSidecar);
    }

    public void Save(string showPath)
    {
        var path = PathFor(showPath);
        var json = JsonSerializer.Serialize(this, LuminaJson.Default.LuminaSidecar);
        File.WriteAllText(path, json);
    }

    public static void SaveShow(LaserShow show)
    {
        if (show.SourcePath is null)
        {
            throw new InvalidOperationException("Enregistrez d’abord le show dans un fichier.");
        }

        var existing = TryLoad(show.SourcePath) ?? new LuminaSidecar();
        existing.Version = 2;
        existing.AudioPath = show.Audio.LinkedPath ?? existing.AudioPath;
        existing.AudioOffsetMs = show.Audio.Offset.TotalMilliseconds;
        existing.Volume = show.Audio.Volume;
        existing.Scenes = show.Timeline.Scenes.Select(SidecarScene.From).ToList();
        existing.Patterns = show.Library.Items.Select(SidecarPattern.From).ToList();
        existing.Clips = show.Timeline.Clips.Select(SidecarClip.From).ToList();
        existing.Colors = show.Colors.Clone();
        existing.Save(show.SourcePath);
    }

    public static void ApplyTo(LaserShow show)
    {
        if (show.SourcePath is null) return;
        var sidecar = TryLoad(show.SourcePath);
        if (sidecar is null) return;

        if (sidecar.Patterns.Count > 0)
        {
            show.Library.Items.Clear();
            foreach (var pattern in sidecar.Patterns)
            {
                var path = Resolve(show.SourcePath, pattern.SourcePath);
                if (path is null || !File.Exists(path)) continue;
                IReadOnlyList<LaserFrame> frames = path.EndsWith(".fra", StringComparison.OrdinalIgnoreCase)
                    ? FraReader.Read(path)
                    : IldaReader.Read(path);
                show.Library.Add(pattern.Name, frames, path, pattern.Id);
            }

            show.Frames = show.Library.Items.SelectMany(p => p.Frames).ToList();
        }

        if (sidecar.Clips.Count > 0)
        {
            show.Timeline.Clips.Clear();
            foreach (var clip in sidecar.Clips)
            {
                show.Timeline.Clips.Add(clip.ToClip());
            }
        }

        if (sidecar.Scenes.Count > 0)
        {
            show.Timeline.ReplaceAll(sidecar.Scenes.Select(s => s.ToScene()));
        }

        var remembered = AudioLocator.FromSidecar(show.SourcePath);
        if (remembered.Status != AudioStatus.NotLinked)
        {
            show.Audio.LinkedPath = remembered.LinkedPath;
            show.Audio.ResolvedPath = remembered.ResolvedPath;
            show.Audio.Offset = remembered.Offset;
            show.Audio.Volume = remembered.Volume;
            show.Audio.Status = remembered.Status;
        }

        if (sidecar.Colors is not null)
        {
            show.Colors = sidecar.Colors;
            if (show.Colors.Entries.Count == 0)
            {
                show.Colors.ResetTableToIlda();
            }

            show.Colors.ApplyProjector(show.Colors.Projector);
            show.Colors.Alignment ??= new ProjectionAlignment();
        }
    }

    private static string? Resolve(string showPath, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;
        if (File.Exists(relative)) return relative;
        var folder = Path.GetDirectoryName(showPath) ?? ".";
        return Path.GetFullPath(Path.Combine(folder, relative));
    }
}

public sealed class SidecarScene
{
    public string Name { get; set; } = "Scène";
    public double StartMs { get; set; }
    public double EndMs { get; set; }
    public int FrameStart { get; set; }
    public int FrameEnd { get; set; }

    public static SidecarScene From(ShowScene scene) => new()
    {
        Name = scene.Name,
        StartMs = scene.Start.TotalMilliseconds,
        EndMs = scene.End.TotalMilliseconds,
        FrameStart = scene.FrameStart,
        FrameEnd = scene.FrameEnd
    };

    public ShowScene ToScene() => new()
    {
        Name = Name,
        Start = TimeSpan.FromMilliseconds(StartMs),
        End = TimeSpan.FromMilliseconds(EndMs),
        FrameStart = FrameStart,
        FrameEnd = FrameEnd
    };
}

public sealed class SidecarPattern
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? SourcePath { get; set; }

    public static SidecarPattern From(PatternItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        SourcePath = item.SourcePath
    };
}

public sealed class SidecarClip
{
    public string PatternId { get; set; } = "";
    public string Name { get; set; } = "Clip";
    public double StartMs { get; set; }
    public double EndMs { get; set; }
    public int Track { get; set; }
    public ClipEffects Effects { get; set; } = new();

    public static SidecarClip From(TimelineClip clip) => new()
    {
        PatternId = clip.PatternId,
        Name = clip.Name,
        StartMs = clip.Start.TotalMilliseconds,
        EndMs = clip.End.TotalMilliseconds,
        Track = clip.Track,
        Effects = clip.Effects.Clone()
    };

    public TimelineClip ToClip() => new()
    {
        PatternId = PatternId,
        Name = Name,
        Start = TimeSpan.FromMilliseconds(StartMs),
        End = TimeSpan.FromMilliseconds(EndMs),
        Track = Track,
        Effects = Effects.Clone()
    };
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LuminaSidecar))]
[JsonSerializable(typeof(SidecarScene))]
[JsonSerializable(typeof(SidecarPattern))]
[JsonSerializable(typeof(SidecarClip))]
[JsonSerializable(typeof(ClipEffects))]
[JsonSerializable(typeof(List<SidecarScene>))]
[JsonSerializable(typeof(List<SidecarPattern>))]
[JsonSerializable(typeof(List<SidecarClip>))]
[JsonSerializable(typeof(ColorConversionProfile))]
[JsonSerializable(typeof(ProjectionAlignment))]
[JsonSerializable(typeof(PaletteEntry))]
[JsonSerializable(typeof(List<PaletteEntry>))]
[JsonSerializable(typeof(IntensityCurve))]
internal partial class LuminaJson : JsonSerializerContext;
