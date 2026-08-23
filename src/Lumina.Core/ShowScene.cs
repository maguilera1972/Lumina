namespace Lumina.Core;

public sealed class ShowScene
{
    public string Name { get; set; } = "Scène";
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public int FrameStart { get; set; }
    public int FrameEnd { get; set; }

    public TimeSpan Duration
    {
        get => End < Start ? TimeSpan.Zero : End - Start;
        set
        {
            if (value < TimeSpan.FromMilliseconds(50))
            {
                value = TimeSpan.FromMilliseconds(50);
            }

            End = Start + value;
        }
    }
}
