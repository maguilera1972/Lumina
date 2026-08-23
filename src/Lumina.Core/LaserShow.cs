namespace Lumina.Core;

public sealed class LaserShow
{
    public required string Title { get; set; }
    public string? SourcePath { get; set; }
    public ShowKind Kind { get; set; }
    public IReadOnlyList<LaserFrame> Frames { get; set; } = [];
    public PatternLibrary Library { get; } = new();
    public Timeline Timeline { get; } = new();
    public AudioCue Audio { get; init; } = new();
    public ColorConversionProfile Colors { get; set; } = ColorConversionProfile.CreateDefault();
    public List<string> Warnings { get; } = [];

    public TimeSpan TimelineDuration => Timeline.Duration;

    public LaserFrame? FrameAt(TimeSpan time) => Timeline.FrameAt(time, this);

    public int IndexOf(LaserFrame frame)
    {
        for (var i = 0; i < Frames.Count; i++)
        {
            if (ReferenceEquals(Frames[i], frame)) return i;
        }

        return 0;
    }
}

public enum ShowKind
{
    IldaAnimation,
    MambaShow,
    LuminaShow
}
