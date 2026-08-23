namespace Lumina.Core;

/// <summary>
/// Audio attached to a show. The soundtrack (MP3 or WAV) is often missing at import time;
/// it can be dropped next to the show later, or linked from the UI.
/// </summary>
public sealed class AudioCue
{
    public static readonly string[] Extensions = [".mp3", ".wav", ".wma", ".flac", ".m4a"];

    public const string OpenDialogFilter =
        "Audio (MP3, WAV)|*.mp3;*.wav|MP3 (*.mp3)|*.mp3|WAV (*.wav)|*.wav|Autres (WMA, M4A, FLAC)|*.wma;*.m4a;*.flac|Tous|*.*";

    /// <summary>Path stored in the Lumina sidecar, preferably relative to the show folder.</summary>
    public string? LinkedPath { get; set; }

    /// <summary>Resolved existing file, if any.</summary>
    public string? ResolvedPath { get; set; }

    /// <summary>Laser time = audio time + offset (positive = laser late).</summary>
    public TimeSpan Offset { get; set; }

    public double Volume { get; set; } = 1.0;

    public AudioStatus Status { get; set; } = AudioStatus.NotLinked;

    /// <summary>True only after the user has explicitly loaded the track this session.</summary>
    public bool IsPlayable => Status == AudioStatus.Ready && File.Exists(ResolvedPath ?? "");

    public static bool IsSupportedFile(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static string FormatLabel(string? path)
    {
        var ext = Path.GetExtension(path ?? "").TrimStart('.').ToUpperInvariant();
        return ext switch
        {
            "MP3" => "MP3",
            "WAV" => "WAV",
            "FLAC" => "FLAC",
            "M4A" => "M4A",
            "WMA" => "WMA",
            _ => "audio"
        };
    }
}

public enum AudioStatus
{
    NotLinked,
    /// <summary>Path remembered in the sidecar; the decoder is not open until the user loads it.</summary>
    Known,
    MissingFile,
    Ready
}
