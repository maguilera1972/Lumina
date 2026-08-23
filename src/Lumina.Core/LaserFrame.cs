namespace Lumina.Core;

public sealed class LaserFrame
{
    public required string Name { get; init; }
    public string Company { get; init; } = "";
    public int FrameNumber { get; init; }
    public int TotalFrames { get; init; }
    public int Projector { get; init; }
    public int Format { get; init; }
    public IReadOnlyList<LaserPoint> Points { get; init; } = [];

    public string FormatLabel => Format switch
    {
        0 => "ILDA 3D indexé",
        1 => "ILDA 2D indexé",
        4 => "ILDA 3D RGB",
        5 => "ILDA 2D RGB",
        -1 => "FRA MediaLas",
        _ => $"Format {Format}"
    };

    public int VisiblePointCount
    {
        get
        {
            var n = 0;
            foreach (var p in Points)
            {
                if (p.IsVisible) n++;
            }
            return n;
        }
    }
}
