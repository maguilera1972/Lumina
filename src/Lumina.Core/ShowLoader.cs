namespace Lumina.Core;

public static class ShowLoader
{
    private static readonly HashSet<string> MambaExt =
    [
        ".xrs", ".xmb", ".mrs", ".shw"
    ];

    public static LaserShow Open(string path)
    {
        LaserShow show;
        var ext = Path.GetExtension(path);

        if (LuminaSidecar.IsProjectFile(path))
        {
            return OpenProject(path);
        }

        if (ext.Equals(".ild", StringComparison.OrdinalIgnoreCase))
        {
            show = FromFrames(Path.GetFileNameWithoutExtension(path), path, ShowKind.IldaAnimation, IldaReader.Read(path));
        }
        else if (ext.Equals(".fra", StringComparison.OrdinalIgnoreCase))
        {
            show = FromFrames(Path.GetFileNameWithoutExtension(path), path, ShowKind.MambaShow, FraReader.Read(path), TimeSpan.FromSeconds(5));
        }
        else if (MambaExt.Contains(ext.ToLowerInvariant()))
        {
            show = MambaShowReader.Open(path);
        }
        else
        {
            throw new NotSupportedException($"Extension non reconnue : {ext}");
        }

        LuminaSidecar.ApplyTo(show);
        EnsureLibrary(show);
        return show;
    }

    public static LaserShow CreateUntitled() => new()
    {
        Title = "Nouveau show",
        Kind = ShowKind.LuminaShow,
        Frames = []
    };

    public static LaserShow OpenProject(string path)
    {
        var stem = Path.GetFileName(path);
        if (stem.EndsWith(LuminaSidecar.Extension, StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[..^LuminaSidecar.Extension.Length];
        }

        var show = new LaserShow
        {
            Title = string.IsNullOrWhiteSpace(stem) ? "Show Lumina" : stem,
            SourcePath = path,
            Kind = ShowKind.LuminaShow,
            Frames = [],
            Audio = new AudioCue()
        };
        LuminaSidecar.ApplyTo(show);
        EnsureLibrary(show);
        if (show.Frames.Count == 0)
        {
            show.Frames = show.Library.Items.SelectMany(p => p.Frames).ToList();
        }

        return show;
    }

    private static void EnsureLibrary(LaserShow show)
    {
        if (show.Library.Items.Count > 0 || show.Frames.Count == 0) return;
        var pattern = show.Library.Add(show.Title, show.Frames, show.SourcePath);
        if (show.Timeline.Clips.Count == 0)
        {
            var length = show.Timeline.Duration > TimeSpan.Zero
                ? show.Timeline.Duration
                : TimeSpan.FromSeconds(Math.Max(2, show.Frames.Count / 25.0));
            show.Timeline.PlaceClip(pattern, TimeSpan.Zero, length);
        }
    }

    public static LaserShow FromFrames(
        string title,
        string? sourcePath,
        ShowKind kind,
        IReadOnlyList<LaserFrame> frames,
        TimeSpan? duration = null)
    {
        var length = duration ?? (frames.Count <= 1
            ? TimeSpan.FromSeconds(5)
            : TimeSpan.FromSeconds(Math.Max(5, frames.Count / 25.0)));

        var show = new LaserShow
        {
            Title = title,
            SourcePath = sourcePath,
            Kind = kind,
            Frames = frames,
            Audio = new AudioCue()
        };
        var pattern = show.Library.Add(title, frames, sourcePath);
        show.Timeline.PlaceClip(pattern, TimeSpan.Zero, length);
        show.Timeline.ReplaceAll(
        [
            new ShowScene
            {
                Name = title,
                Start = TimeSpan.Zero,
                End = length,
                FrameStart = 0,
                FrameEnd = Math.Max(0, frames.Count - 1)
            }
        ]);
        return show;
    }
}
