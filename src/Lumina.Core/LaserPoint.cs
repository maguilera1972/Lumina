namespace Lumina.Core;

/// <summary>One ILDA-style vector point. Coordinates use the ILDA range ±32767.</summary>
public readonly record struct LaserPoint(
    short X,
    short Y,
    short Z,
    byte R,
    byte G,
    byte B,
    byte Intensity,
    bool Blanked)
{
    public bool IsVisible => !Blanked && Intensity > 0 && (R | G | B) != 0;
}
