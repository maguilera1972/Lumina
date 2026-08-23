namespace Lumina.Core;

public sealed class ClipEffects
{
    public double Intensity { get; set; } = 1;
    public double Red { get; set; } = 1;
    public double Green { get; set; } = 1;
    public double Blue { get; set; } = 1;
    public double Hue { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double Rotation { get; set; }
    public double Width { get; set; } = 1;
    public double Height { get; set; } = 1;

    public ClipEffects Clone() => (ClipEffects)MemberwiseClone();
}
