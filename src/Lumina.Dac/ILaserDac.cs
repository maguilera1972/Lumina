namespace Lumina.Dac;

public readonly record struct DacPoint(short X, short Y, byte R, byte G, byte B, bool Blanked);

public interface ILaserDac : IDisposable
{
    string DisplayName { get; }
    bool IsConnected { get; }
    void Connect();
    void WriteFrame(IReadOnlyList<DacPoint> points, int pointRate);
    void Blank();
}

public static class DacConvert
{
    public static DacPoint[] From(Lumina.Core.LaserFrame? frame) =>
        From(frame, profile: null);

    /// <summary>
    /// Packs a frame for the DAC. With a colour profile, RGB stays 1:1;
    /// RGY puts yellow on the ILDA blue analog channel (USB-Box pin 7).
    /// </summary>
    public static DacPoint[] From(Lumina.Core.LaserFrame? frame, Lumina.Core.ColorConversionProfile? profile)
    {
        if (frame is null) return [];
        var converted = Lumina.Core.LaserColorConverter.ApplyForDac(frame, profile) ?? frame;
        var pts = new DacPoint[converted.Points.Count];
        for (var i = 0; i < converted.Points.Count; i++)
        {
            var p = converted.Points[i];
            pts[i] = new DacPoint(p.X, p.Y, p.R, p.G, p.B, p.Blanked || !p.IsVisible);
        }

        return pts;
    }
}
