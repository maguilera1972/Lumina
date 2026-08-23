namespace Lumina.Core;

/// <summary>
/// Finds an MP3/WAV for a show without requiring it to be embedded in the Mamba file.
/// Order: sidecar .lumina.json, paths mentioned in the show, same basename, then a single audio file in the folder.
/// </summary>
public static class AudioLocator
{
    public static AudioCue Locate(string showPath, IEnumerable<string>? mentionedNames = null)
    {
        var folder = Path.GetDirectoryName(showPath) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(showPath);
        var cue = new AudioCue();

        var sidecar = LuminaSidecar.TryLoad(showPath);
        if (sidecar?.AudioPath is { Length: > 0 } linked)
        {
            cue.LinkedPath = linked;
            cue.Offset = TimeSpan.FromMilliseconds(sidecar.AudioOffsetMs);
            cue.Volume = sidecar.Volume;
            var resolved = Resolve(folder, linked);
            if (File.Exists(resolved))
            {
                cue.ResolvedPath = resolved;
                cue.Status = AudioStatus.Ready;
                return cue;
            }

            cue.Status = AudioStatus.MissingFile;
        }

        foreach (var name in mentionedNames ?? [])
        {
            var resolved = Resolve(folder, name);
            if (!File.Exists(resolved)) continue;
            cue.LinkedPath = Path.GetFileName(resolved);
            cue.ResolvedPath = resolved;
            cue.Status = AudioStatus.Ready;
            return cue;
        }

        foreach (var ext in AudioCue.Extensions)
        {
            var candidate = Path.Combine(folder, stem + ext);
            if (!File.Exists(candidate)) continue;
            cue.LinkedPath = Path.GetFileName(candidate);
            cue.ResolvedPath = candidate;
            cue.Status = AudioStatus.Ready;
            return cue;
        }

        var audios = Directory.EnumerateFiles(folder)
            .Where(f => AudioCue.Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        if (audios.Count == 1)
        {
            cue.LinkedPath = Path.GetFileName(audios[0]);
            cue.ResolvedPath = audios[0];
            cue.Status = AudioStatus.Ready;
            return cue;
        }

        if (cue.Status != AudioStatus.MissingFile)
        {
            cue.Status = AudioStatus.NotLinked;
        }

        return cue;
    }

    public static AudioCue Link(string showPath, string audioPath, TimeSpan offset, double volume = 1.0)
    {
        if (!File.Exists(audioPath))
        {
            throw new FileNotFoundException("Fichier audio introuvable.", audioPath);
        }

        var folder = Path.GetDirectoryName(showPath) ?? ".";
        var relative = GetRelativePathSafe(folder, audioPath);
        var sidecar = new LuminaSidecar
        {
            AudioPath = relative,
            AudioOffsetMs = offset.TotalMilliseconds,
            Volume = volume
        };
        sidecar.Save(showPath);

        return new AudioCue
        {
            LinkedPath = relative,
            ResolvedPath = Path.GetFullPath(audioPath),
            Offset = offset,
            Volume = volume,
            Status = AudioStatus.Ready
        };
    }

    private static string Resolve(string folder, string raw)
    {
        raw = raw.Trim().Trim('\0');
        if (Path.IsPathRooted(raw))
        {
            if (File.Exists(raw)) return raw;
            return Path.Combine(folder, Path.GetFileName(raw));
        }

        return Path.GetFullPath(Path.Combine(folder, raw));
    }

    private static string GetRelativePathSafe(string folder, string audioPath)
    {
        try
        {
            return Path.GetRelativePath(folder, audioPath);
        }
        catch
        {
            return audioPath;
        }
    }
}
