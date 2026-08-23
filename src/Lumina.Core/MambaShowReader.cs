using System.Text;
using System.Text.RegularExpressions;

namespace Lumina.Core;

/// <summary>Reads Mamba show containers (.xrs / .xmb / .mrs / .shw) without executing Mamba.</summary>
public static class MambaShowReader
{
    private static readonly Regex PathRegex = new(
        @"[A-Za-z]:\\[^\x00-\x1F]{3,240}?\.(ild|fra|ILD|FRA)|(?:\.[\\/])[^\x00-\x1F]{1,240}?\.(ild|fra|ILD|FRA)",
        RegexOptions.Compiled);

    private static readonly Regex AudioRegex = new(
        @"[^\x00-\x1F]{1,240}?\.(mp3|wav|wma|flac|m4a|MP3|WAV)",
        RegexOptions.Compiled);

    public static LaserShow Open(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var text = Encoding.ASCII.GetString(bytes);
        var folder = Path.GetDirectoryName(path) ?? ".";
        var title = Path.GetFileNameWithoutExtension(path);

        var framePaths = DiscoverFrameFiles(path, folder, text);
        var frames = new List<LaserFrame>();
        var warnings = new List<string>();

        foreach (var framePath in framePaths)
        {
            try
            {
                if (framePath.EndsWith(".fra", StringComparison.OrdinalIgnoreCase))
                {
                    frames.AddRange(FraReader.Read(framePath));
                }
                else
                {
                    frames.AddRange(IldaReader.Read(framePath));
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"{Path.GetFileName(framePath)} : {ex.Message}");
            }
        }

        if (frames.Count == 0)
        {
            warnings.Add("Aucun frame ILDA/FRA n'a pu être chargé. Vérifiez que les fichiers graphiques sont à côté du show.");
        }

        var duration = GuessDuration(frames);
        var scenes = frames.Count == 0
            ? Array.Empty<ShowScene>()
            : new[]
            {
                new ShowScene
                {
                    Start = TimeSpan.Zero,
                    End = duration,
                    FrameStart = 0,
                    FrameEnd = frames.Count - 1
                }
            };

        var mentionedAudio = AudioRegex.Matches(text)
            .Select(m => m.Value.Trim('\0', ' '))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var show = new LaserShow
        {
            Title = title,
            SourcePath = path,
            Kind = ShowKind.MambaShow,
            Frames = frames,
            Audio = new AudioCue()
        };
        show.Warnings.AddRange(warnings);
        if (mentionedAudio.Length > 0)
        {
            show.Warnings.Add("Une piste audio est citée dans le show — chargez-la manuellement (Audio…).");
        }
        var pattern = show.Library.Add(title, frames, path);
        if (scenes.Length > 0)
        {
            show.Timeline.ReplaceAll(scenes);
            show.Timeline.PlaceClip(pattern, TimeSpan.Zero, scenes[^1].End);
        }
        else
        {
            show.Timeline.PlaceClip(pattern, TimeSpan.Zero, GuessDuration(frames));
        }
        return show;
    }

    private static List<string> DiscoverFrameFiles(string showPath, string folder, string text)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string candidate)
        {
            if (!File.Exists(candidate)) return;
            var full = Path.GetFullPath(candidate);
            if (seen.Add(full)) found.Add(full);
        }

        foreach (Match match in PathRegex.Matches(text))
        {
            var raw = match.Value.Trim('\0');
            Add(ResolveBesideShow(folder, raw));
            Add(Path.Combine(folder, Path.GetFileName(raw)));
        }

        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var ext = Path.GetExtension(file);
            if (ext.Equals(".ild", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".fra", StringComparison.OrdinalIgnoreCase))
            {
                Add(file);
            }
        }

        // Prefer files whose name shares the show stem.
        var stem = Path.GetFileNameWithoutExtension(showPath);
        found.Sort((a, b) =>
        {
            var ascore = Path.GetFileName(a).StartsWith(stem, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            var bscore = Path.GetFileName(b).StartsWith(stem, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            return ascore != bscore ? ascore.CompareTo(bscore) : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });

        return found;
    }

    private static string ResolveBesideShow(string folder, string raw)
    {
        raw = raw.Replace('/', Path.DirectorySeparatorChar);
        if (raw.StartsWith(@".\", StringComparison.Ordinal) || raw.StartsWith("./", StringComparison.Ordinal))
        {
            return Path.Combine(folder, raw[2..]);
        }

        if (Path.IsPathRooted(raw))
        {
            return Path.Combine(folder, Path.GetFileName(raw));
        }

        return Path.Combine(folder, raw);
    }

    private static TimeSpan GuessDuration(IReadOnlyList<LaserFrame> frames)
    {
        if (frames.Count <= 1) return TimeSpan.FromSeconds(5);
        // Default 25 fps until the soundtrack duration becomes the master clock.
        return TimeSpan.FromSeconds(Math.Max(5, frames.Count / 25.0));
    }
}
