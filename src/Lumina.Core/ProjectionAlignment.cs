namespace Lumina.Core;

/// <summary>Geometric fit of the ILDA field onto the projection surface (size, position, rotation).</summary>
public sealed class ProjectionAlignment
{
    /// <summary>Horizontal scale of the ILDA field (1 = full scanner range).</summary>
    public double Width { get; set; } = 1;

    /// <summary>Vertical scale of the ILDA field (1 = full scanner range).</summary>
    public double Height { get; set; } = 1;

    /// <summary>Horizontal shift in ILDA units (−1 … 1).</summary>
    public double OffsetX { get; set; }

    /// <summary>Vertical shift in ILDA units (−1 … 1).</summary>
    public double OffsetY { get; set; }

    /// <summary>Rotation in degrees, around the centre of the field.</summary>
    public double Rotation { get; set; }

    public bool FlipX { get; set; }
    public bool FlipY { get; set; }

    public bool IsIdentity =>
        Math.Abs(Width - 1) < 0.0005 &&
        Math.Abs(Height - 1) < 0.0005 &&
        Math.Abs(OffsetX) < 0.0005 &&
        Math.Abs(OffsetY) < 0.0005 &&
        Math.Abs(Rotation) < 0.05 &&
        !FlipX && !FlipY;

    public ProjectionAlignment Clone() => new()
    {
        Width = Width,
        Height = Height,
        OffsetX = OffsetX,
        OffsetY = OffsetY,
        Rotation = Rotation,
        FlipX = FlipX,
        FlipY = FlipY
    };

    public void CopyFrom(ProjectionAlignment other)
    {
        Width = other.Width;
        Height = other.Height;
        OffsetX = other.OffsetX;
        OffsetY = other.OffsetY;
        Rotation = other.Rotation;
        FlipX = other.FlipX;
        FlipY = other.FlipY;
    }

    public void Reset()
    {
        Width = 1;
        Height = 1;
        OffsetX = 0;
        OffsetY = 0;
        Rotation = 0;
        FlipX = false;
        FlipY = false;
    }
}
