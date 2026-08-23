namespace Lumina.Core;

public sealed class TimelineClip
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..10];
    public required string PatternId { get; set; }
    public string Name { get; set; } = "Clip";
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public int Track { get; set; }
    public ClipEffects Effects { get; set; } = new();

    public TimeSpan Duration
    {
        get => End < Start ? TimeSpan.Zero : End - Start;
        set => End = Start + (value < TimeSpan.FromMilliseconds(50) ? TimeSpan.FromMilliseconds(50) : value);
    }

    public double ProgressAt(TimeSpan time)
    {
        var span = Duration.TotalSeconds;
        if (span <= 0) return 0;
        return Math.Clamp((time - Start).TotalSeconds / span, 0, 0.999);
    }
}
