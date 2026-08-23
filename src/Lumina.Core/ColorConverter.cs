namespace Lumina.Core;

/// <summary>Per-diode levels after conversion, before ILDA analog packing.</summary>
public readonly record struct DiodeLevels(byte R, byte G, byte B, byte Y)
{
    public static DiodeLevels Off => default;

    public bool IsOff => (R | G | B | Y) == 0;

    public (byte r, byte g, byte b) ToPreviewRgb(ProjectorColorSet projector)
    {
        if (projector == ProjectorColorSet.Rgy)
        {
            return (
                (byte)Math.Min(255, R + Y),
                (byte)Math.Min(255, G + Y),
                0);
        }

        return (R, G, B);
    }

    public (byte r, byte g, byte b) ToIldaAnalog(ColorConversionProfile profile)
    {
        if (profile.Projector == ProjectorColorSet.Rgy)
        {
            if (profile.YellowRouting == IldaYellowRouting.MixOnRedGreen)
            {
                return (
                    (byte)Math.Min(255, R + Y),
                    (byte)Math.Min(255, G + Y),
                    0);
            }

            return (R, G, Y);
        }

        return (R, G, B);
    }
}

public static class LaserColorConverter
{
    public static LaserFrame? Apply(LaserFrame? frame, ColorConversionProfile? profile) =>
        Apply(frame, profile, analogForDac: false);

    public static LaserFrame? ApplyForDac(LaserFrame? frame, ColorConversionProfile? profile) =>
        Apply(frame, profile, analogForDac: true);

    public static IReadOnlyList<LaserFrame> ApplyAll(IEnumerable<LaserFrame> frames, ColorConversionProfile? profile) =>
        frames.Select(f => Apply(f, profile)!).ToList();

    public static IReadOnlyList<LaserFrame> ApplyAllForDac(IEnumerable<LaserFrame> frames, ColorConversionProfile? profile) =>
        frames.Select(f => ApplyForDac(f, profile)!).ToList();

    public static LaserPoint Apply(LaserPoint point, ColorConversionProfile profile) =>
        Pack(point, ConvertDiodes(point, profile), profile, analogForDac: false);

    public static LaserPoint ApplyForDac(LaserPoint point, ColorConversionProfile profile) =>
        Pack(point, ConvertDiodes(point, profile), profile, analogForDac: true);

    public static DiodeLevels ConvertDiodes(LaserPoint point, ColorConversionProfile profile)
    {
        if (point.Blanked) return DiodeLevels.Off;

        var r = point.R / 255.0;
        var g = point.G / 255.0;
        var b = point.B / 255.0;

        if (!profile.Enabled && profile.IsRgb)
        {
            return new DiodeLevels(point.R, point.G, point.B, 0);
        }

        if (profile.Enabled)
        {
            if (profile.Mode == ColorConversionMode.PaletteTable && profile.Entries.Count > 0)
            {
                var entry = NearestEntry(point.R, point.G, point.B, profile.Entries);
                r = entry.OutR / 255.0;
                g = entry.OutG / 255.0;
                b = entry.OutB / 255.0;
            }
            else if (profile.Mode == ColorConversionMode.NearestPrimary)
            {
                (r, g, b) = SnapPrimary(r, g, b, profile);
            }
            else if (profile.Mode == ColorConversionMode.AvailableMix)
            {
                (r, g, b) = MixAvailable(r, g, b, profile);
            }
        }

        return ToDiodes(r, g, b, profile);
    }

    private static LaserFrame? Apply(LaserFrame? frame, ColorConversionProfile? profile, bool analogForDac)
    {
        if (frame is null) return null;
        if (profile is null) return frame;
        var points = new LaserPoint[frame.Points.Count];
        for (var i = 0; i < frame.Points.Count; i++)
        {
            var mapped = ProjectionMapper.Map(frame.Points[i], profile.Alignment ?? new ProjectionAlignment());
            var diodes = ConvertDiodes(mapped, profile);
            points[i] = Pack(mapped, diodes, profile, analogForDac);
        }

        return new LaserFrame
        {
            Name = frame.Name,
            Company = frame.Company,
            FrameNumber = frame.FrameNumber,
            TotalFrames = frame.TotalFrames,
            Projector = frame.Projector,
            Format = frame.Format,
            Points = points
        };
    }

    private static LaserPoint Pack(LaserPoint point, DiodeLevels diodes, ColorConversionProfile profile, bool analogForDac)
    {
        var (r, g, b) = analogForDac
            ? diodes.ToIldaAnalog(profile)
            : diodes.ToPreviewRgb(profile.Projector);
        var blanked = point.Blanked || diodes.IsOff || (r | g | b) == 0;
        return point with
        {
            R = r,
            G = g,
            B = b,
            Intensity = blanked ? (byte)0 : point.Intensity,
            Blanked = blanked
        };
    }

    private static DiodeLevels ToDiodes(double r, double g, double b, ColorConversionProfile p)
    {
        var master = Math.Clamp(p.MasterBrightness, 0, 1);

        if (p.Projector == ProjectorColorSet.Rgy)
        {
            var y = (b * Math.Clamp(p.BlueFold, 0, 2)) +
                    (Math.Min(r, g) * Math.Clamp(p.YellowFromRedGreen, 0, 2));
            r = p.UseRed ? r * p.RedGain : 0;
            g = p.UseGreen ? g * p.GreenGain : 0;
            y = p.UseYellow ? y * p.YellowGain : 0;
            r = p.RedCurve.Map(r) * master;
            g = p.GreenCurve.Map(g) * master;
            y = p.YellowCurve.Map(y) * master;
            return new DiodeLevels(ClampByte(r * 255), ClampByte(g * 255), 0, ClampByte(y * 255));
        }

        r = p.UseRed ? r * p.RedGain : 0;
        g = p.UseGreen ? g * p.GreenGain : 0;
        b = p.UseBlue ? b * p.BlueGain : 0;
        r = p.RedCurve.Map(r) * master;
        g = p.GreenCurve.Map(g) * master;
        b = p.BlueCurve.Map(b) * master;
        return new DiodeLevels(ClampByte(r * 255), ClampByte(g * 255), ClampByte(b * 255), 0);
    }

    private static PaletteEntry NearestEntry(byte r, byte g, byte b, List<PaletteEntry> entries)
    {
        var best = entries[0];
        var bestD = int.MaxValue;
        foreach (var e in entries)
        {
            var dr = e.InR - r;
            var dg = e.InG - g;
            var db = e.InB - b;
            var d = dr * dr + dg * dg + db * db;
            if (d >= bestD) continue;
            bestD = d;
            best = e;
        }

        return best;
    }

    private static (double r, double g, double b) SnapPrimary(double r, double g, double b, ColorConversionProfile p)
    {
        var intensity = Math.Max(r, Math.Max(g, b));
        var candidates = new List<(double r, double g, double b, double score)>();
        if (p.UseRed) candidates.Add((1, 0, 0, r));
        if (p.UseGreen) candidates.Add((0, 1, 0, g));
        if (p.IsRgb && p.UseBlue) candidates.Add((0, 0, 1, b));
        if (p.IsRgy && p.UseYellow)
        {
            candidates.Add((0, 0, 1, Math.Max((r + g) * 0.5, b)));
        }

        if (candidates.Count == 0) return (0, 0, 0);
        var win = candidates[0];
        foreach (var c in candidates)
        {
            if (c.score > win.score) win = c;
        }

        return (win.r * intensity, win.g * intensity, win.b * intensity);
    }

    private static (double r, double g, double b) MixAvailable(double r, double g, double b, ColorConversionProfile p)
    {
        if (p.IsRgy)
        {
            return (r, g, b);
        }

        if (!p.UseBlue)
        {
            var lift = b * Math.Clamp(p.BlueFold, 0, 2);
            if (p.UseYellow || p.IsRgy)
            {
                r += lift * 0.5;
                g += lift * 0.5;
            }
            else if (p.UseGreen) g += b;
            else if (p.UseRed) r += b;
            b = 0;
        }

        if (!p.UseRed)
        {
            if (p.UseYellow || p.IsRgy) g += r * 0.5;
            else if (p.UseGreen) g += r;
            r = 0;
        }

        if (!p.UseGreen)
        {
            if (p.UseYellow || p.IsRgy) r += g * 0.5;
            else if (p.UseRed) r += g;
            g = 0;
        }

        return (r, g, b);
    }

    private static byte ClampByte(double v) => (byte)Math.Clamp((int)Math.Round(v), 0, 255);
}
