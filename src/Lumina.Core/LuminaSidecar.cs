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
        LuminaSidecar? sidecar;
        try
        {
            sidecar = TryLoad(show.SourcePath);
        }
        catch (Exception ex)
        {
            show.Warnings.Add("Sidecar Lumina illisible : " + ex.Message);
            return;
        }

        if (sidecar is null) return;

        // Sidecars créés par d'anciennes versions pouvaient pointer un .mrs comme source ILDA.
        // On les ignore pour les motifs ; le contenu déjà chargé (Mamba) reste la référence.
        if (sidecar.Patterns.Count > 0)
        {
            var loaded = new List<(string Id, string Name, string Path, IReadOnlyList<LaserFrame> Frames)>();
            foreach (var pattern in sidecar.Patterns)
            {
                try
                {
                    var path = Resolve(show.SourcePath, pattern.SourcePath);
                    if (path is null || !File.Exists(path) || !IsLaserFrameFile(path) ||
                        (path.EndsWith(".ild", StringComparison.OrdinalIgnoreCase) && !IldaReader.LooksLikeIlda(path)))
                    {
                        var existing = FindExistingPattern(show, pattern);
                        if (existing is not null)
                        {
                            loaded.Add((pattern.Id, pattern.Name, existing.SourcePath ?? "", existing.Frames));
                        }

                        continue;
                    }

                    var frames = ReadLaserFrames(path);
                    if (frames.Count == 0) continue;
                    loaded.Add((pattern.Id, pattern.Name, path, frames));
                }
                catch (Exception ex)
                {
                    show.Warnings.Add($"Motif « {pattern.Name} » : {ex.Message}");
                }
            }

            // Ne remplacer la bibliothèque que si on a rechargé au moins un vrai fichier ILDA/FRA.
            // Sinon conserver le show principal (Mamba / ILDA d'origine).
            var fromDisk = loaded.Count(l => IsLaserFrameFile(l.Path));
            if (fromDisk > 0)
            {
                show.Library.Items.Clear();
                foreach (var item in loaded)
                {
                    show.Library.Add(item.Name, item.Frames, string.IsNullOrWhiteSpace(item.Path) ? null : item.Path, item.Id);
                }

                if (show.Library.Items.Count > 0)
                {
                    show.Frames = show.Library.Items.SelectMany(p => p.Frames).ToList();
                }
            }
        }

        if (sidecar.Clips.Count > 0)
        {
            var clips = new List<TimelineClip>();
            foreach (var clip in sidecar.Clips)
            {
                var built = clip.ToClip();
                if (show.Library.Get(built.PatternId) is null)
                {
                    // Réaligner sur le premier motif si l'id a changé (réouverture Mamba).
                    if (show.Library.Items.Count > 0)
                    {
                        built.PatternId = show.Library.Items[0].Id;
                    }
                    else
                    {
                        continue;
                    }
                }

                clips.Add(built);
            }

            if (clips.Count > 0)
            {
                show.Timeline.Clips.Clear();
                show.Timeline.Clips.AddRange(clips);
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
            ProjectionWorldBank.EnsureDefaults(show.Colors);
            ProjectionWorldBank.SyncPrimary(show.Colors);
        }
    }

    public static bool IsLaserFrameFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var ext = Path.GetExtension(path);
        return ext.Equals(".ild", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".fra", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<LaserFrame> ReadLaserFrames(string path) =>
        path.EndsWith(".fra", StringComparison.OrdinalIgnoreCase)
            ? FraReader.Read(path)
            : IldaReader.Read(path);

    private static PatternItem? FindExistingPattern(LaserShow show, SidecarPattern pattern)
    {
        if (!string.IsNullOrWhiteSpace(pattern.Id))
        {
            var byId = show.Library.Get(pattern.Id);
            if (byId is not null) return byId;
        }

        return show.Library.Items.FirstOrDefault(p =>
            p.Name.Equals(pattern.Name, StringComparison.OrdinalIgnoreCase));
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
        // Ne jamais persister un conteneur Mamba / show comme source ILDA.
        SourcePath = LuminaSidecar.IsLaserFrameFile(item.SourcePath) ? item.SourcePath : null
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
[JsonSerializable(typeof(ProjectionWorld))]
[JsonSerializable(typeof(List<ProjectionWorld>))]
[JsonSerializable(typeof(PaletteEntry))]
[JsonSerializable(typeof(List<PaletteEntry>))]
[JsonSerializable(typeof(IntensityCurve))]
internal partial class LuminaJson : JsonSerializerContext;
