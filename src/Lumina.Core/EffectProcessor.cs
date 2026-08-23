namespace Lumina.Core;

public static class EffectProcessor
{
    public static LaserFrame Apply(LaserFrame source, ClipEffects fx)
    {
        var points = new LaserPoint[source.Points.Count];
        var rot = fx.Rotation * Math.PI / 180.0;
        var cos = Math.Cos(rot);
        var sin = Math.Sin(rot);
        var intensity = Math.Clamp(fx.Intensity, 0, 2);
        var hue = fx.Hue;

        for (var i = 0; i < source.Points.Count; i++)
        {
            var p = source.Points[i];
            var x = p.X / 32767.0 * fx.Width;
            var y = p.Y / 32767.0 * fx.Height;
            var rx = x * cos - y * sin;
            var ry = x * sin + y * cos;
            rx += fx.OffsetX;
            ry += fx.OffsetY;

            var (r, g, b) = ShiftHue(p.R, p.G, p.B, hue);
            r = ClampByte(r * fx.Red * intensity);
            g = ClampByte(g * fx.Green * intensity);
            b = ClampByte(b * fx.Blue * intensity);
            var blanked = p.Blanked || intensity <= 0.001 || (r | g | b) == 0 && p.Blanked;

            points[i] = new LaserPoint(
                ToIlda(rx),
                ToIlda(ry),
                p.Z,
                r, g, b,
                blanked ? (byte)0 : (byte)Math.Clamp(p.Intensity * intensity, 0, 255),
                blanked);
        }

        return new LaserFrame
        {
            Name = source.Name,
            Company = source.Company,
            FrameNumber = source.FrameNumber,
            TotalFrames = source.TotalFrames,
            Projector = source.Projector,
            Format = source.Format,
            Points = points
        };
    }

    internal static short ToIlda(double v) =>
        (short)Math.Clamp((int)Math.Round(v * 32767.0), -32767, 32767);

    internal static byte ClampByte(double v) =>
        (byte)Math.Clamp((int)Math.Round(v), 0, 255);

    internal static (byte r, byte g, byte b) ShiftHue(byte r, byte g, byte b, double degrees)
    {
        if (Math.Abs(degrees) < 0.01) return (r, g, b);
        RgbToHsv(r / 255.0, g / 255.0, b / 255.0, out var h, out var s, out var v);
        h = (h + degrees / 360.0) % 1.0;
        if (h < 0) h += 1;
        HsvToRgb(h, s, v, out var rr, out var gg, out var bb);
        return (ClampByte(rr * 255), ClampByte(gg * 255), ClampByte(bb * 255));
    }

    private static void RgbToHsv(double r, double g, double b, out double h, out double s, out double v)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        v = max;
        var d = max - min;
        s = max <= 0 ? 0 : d / max;
        if (d <= 0) { h = 0; return; }
        if (max == r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6.0;
        else if (max == g) h = ((b - r) / d + 2) / 6.0;
        else h = ((r - g) / d + 4) / 6.0;
    }

    private static void HsvToRgb(double h, double s, double v, out double r, out double g, out double b)
    {
        var i = (int)Math.Floor(h * 6);
        var f = h * 6 - i;
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);
        switch (i % 6)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
    }
}
