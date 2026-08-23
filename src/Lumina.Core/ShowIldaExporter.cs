namespace Lumina.Core;

public enum IldaExportMode
{
    /// <summary>Write the frames referenced by the show (FRA/ILDA) as an ILDA sequence.</summary>
    SourceFrames,
    /// <summary>Sample the timeline so timing and effects become an ILDA animation.</summary>
    FlattenTimeline
}

public sealed class IldaExportResult
{
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public required int FrameCount { get; init; }
    public required IldaExportMode Mode { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>Converts MediaLas MRS/XRS/XMB/SHW (and FRA) shows to ILDA format-5.</summary>
public static class ShowIldaExporter
{
    public static readonly string[] SourceExtensions = [".mrs", ".xrs", ".xmb", ".shw", ".fra", ".ild"];

    public static IldaExportResult Export(string sourcePath, string destinationPath, IldaExportMode? mode = null, int framesPerSecond = 25)
    {
        var ext = Path.GetExtension(sourcePath);
        if (!SourceExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Conversion ILDA : extension non reconnue ({ext}).");
        }

        var show = ShowLoader.Open(sourcePath);
        var chosen = mode ?? ChooseMode(show);
        var frames = chosen == IldaExportMode.FlattenTimeline
            ? Flatten(show, framesPerSecond)
            : show.Frames.ToList();

        var warnings = new List<string>(show.Warnings);
        if (frames.Count == 0)
        {
            throw new InvalidDataException("Aucun frame à exporter. Placez les .fra / .ild à côté du fichier MRS.");
        }

        if (frames.Count > 65535)
        {
            warnings.Add($"L’ILDA est limité à 65535 frames ; troncature de {frames.Count} à 65535.");
            frames = frames.Take(65535).ToList();
        }

        warnings.AddRange(show.Colors.DriverWarnings());
        if (show.Colors.Enabled || show.Colors.IsRgy)
        {
            frames = LaserColorConverter.ApplyAllForDac(frames, show.Colors).ToList();
            if (show.Colors.IsRgy)
            {
                warnings.Add(show.Colors.AnalogMapSummary());
            }
        }

        for (var i = 0; i < frames.Count; i++)
        {
            if (frames[i].Points.Count <= 65535) continue;
            warnings.Add($"{frames[i].Name} : {frames[i].Points.Count} points, limité à 65535.");
            frames[i] = new LaserFrame
            {
                Name = frames[i].Name,
                Company = frames[i].Company,
                FrameNumber = i,
                TotalFrames = frames.Count,
                Projector = frames[i].Projector,
                Format = 5,
                Points = frames[i].Points.Take(65535).ToList()
            };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath)) ?? ".");
        IldaWriter.WriteFile(destinationPath, frames);
        return new IldaExportResult
        {
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            FrameCount = frames.Count,
            Mode = chosen,
            Warnings = warnings
        };
    }

    internal static IldaExportMode ChooseMode(LaserShow show)
    {
        if (show.Timeline.Clips.Count > 1) return IldaExportMode.FlattenTimeline;
        if (show.Frames.Count > 1) return IldaExportMode.SourceFrames;
        if (show.TimelineDuration > TimeSpan.FromSeconds(2) && show.Frames.Count > 0)
        {
            return IldaExportMode.FlattenTimeline;
        }

        return IldaExportMode.SourceFrames;
    }

    internal static List<LaserFrame> Flatten(LaserShow show, int framesPerSecond)
    {
        var fps = Math.Clamp(framesPerSecond, 1, 120);
        var duration = show.TimelineDuration;
        if (duration < TimeSpan.FromMilliseconds(200))
        {
            duration = TimeSpan.FromSeconds(Math.Max(1, show.Frames.Count / (double)fps));
        }

        var count = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds * fps));
        count = Math.Min(count, 65535);
        var frames = new List<LaserFrame>(count);
        for (var i = 0; i < count; i++)
        {
            var t = TimeSpan.FromSeconds(i / (double)fps);
            var frame = show.FrameAt(t);
            frames.Add(frame ?? BlankFrame(i, count));
        }

        return frames;
    }

    private static LaserFrame BlankFrame(int index, int total) => new()
    {
        Name = "blank",
        Company = "Lumina",
        FrameNumber = index,
        TotalFrames = total,
        Format = 5,
        Points = [new LaserPoint(0, 0, 0, 0, 0, 0, 0, true)]
    };
}
