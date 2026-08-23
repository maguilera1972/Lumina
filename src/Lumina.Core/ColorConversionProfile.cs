namespace Lumina.Core;

public enum ColorConversionMode
{
    /// <summary>Keep source RGB, apply projector mapping and gains.</summary>
    Direct,
    /// <summary>Snap each point to the strongest installed diode.</summary>
    NearestPrimary,
    /// <summary>Fit the colour onto the diodes actually installed.</summary>
    AvailableMix,
    /// <summary>Map through the editable 64-colour table, then apply diodes/gains.</summary>
    PaletteTable
}

/// <summary>Physical laser head Lumina is driving. The two supported booth projectors.</summary>
public enum ProjectorColorSet
{
    /// <summary>Red + green + blue diodes. ILDA analog R/G/B pins 1:1.</summary>
    Rgb = 0,
    /// <summary>Red + green + yellow diodes. No blue. Yellow on ILDA blue analog (USB-Box).</summary>
    Rgy = 1
}

/// <summary>How the yellow diode is wired on a 3-channel ILDA / MediaLas USB-Box DAC.</summary>
public enum IldaYellowRouting
{
    /// <summary>USB-Box / ILDA pin 7 (blue analog) drives the yellow laser. Required for MediaLas.</summary>
    IldaBlueAsYellow = 0,
    /// <summary>No yellow pin: approximate yellow by lighting red+green together, mute blue.</summary>
    MixOnRedGreen = 1
}

public enum DacDriverKind
{
    Preview = 0,
    /// <summary>FTDI D2XX MediaLas USB-Box: three analog ILDA colour channels (R, G, B).</summary>
    MediaLasUsbBox = 1
}

public sealed class PaletteEntry
{
    public int Index { get; set; }
    public byte InR { get; set; }
    public byte InG { get; set; }
    public byte InB { get; set; }
    public byte OutR { get; set; }
    public byte OutG { get; set; }
    public byte OutB { get; set; }
}

/// <summary>How source ILDA/FRA colours are converted for the lasers and DAC actually used.</summary>
public sealed class ColorConversionProfile
{
    public string Name { get; set; } = "RGB";
    public bool Enabled { get; set; } = true;
    public ProjectorColorSet Projector { get; set; } = ProjectorColorSet.Rgb;
    public DacDriverKind Driver { get; set; } = DacDriverKind.MediaLasUsbBox;
    public IldaYellowRouting YellowRouting { get; set; } = IldaYellowRouting.IldaBlueAsYellow;
    public ColorConversionMode Mode { get; set; } = ColorConversionMode.Direct;

    public bool UseRed { get; set; } = true;
    public bool UseGreen { get; set; } = true;
    public bool UseBlue { get; set; } = true;
    public bool UseYellow { get; set; }

    public double RedGain { get; set; } = 1;
    public double GreenGain { get; set; } = 1;
    public double BlueGain { get; set; } = 1;
    public double YellowGain { get; set; } = 1;
    /// <summary>Master brightness 0–1, combined with per-diode gains (Liberation-style output level).</summary>
    public double MasterBrightness { get; set; } = 1;
    /// <summary>How much source blue is folded onto the yellow diode (RGY).</summary>
    public double BlueFold { get; set; } = 1;
    /// <summary>How much overlapping R+G also feeds the yellow diode (RGY).</summary>
    public double YellowFromRedGreen { get; set; } = 0.35;

    public IntensityCurve RedCurve { get; set; } = new();
    public IntensityCurve GreenCurve { get; set; } = new();
    public IntensityCurve BlueCurve { get; set; } = new();
    public IntensityCurve YellowCurve { get; set; } = new();

    public List<PaletteEntry> Entries { get; set; } = [];
    public ProjectionAlignment Alignment { get; set; } = new();

    public bool IsRgb => Projector == ProjectorColorSet.Rgb;
    public bool IsRgy => Projector == ProjectorColorSet.Rgy;

    public static ColorConversionProfile CreateDefault() => CreateRgb();

    public static ColorConversionProfile CreateRgb()
    {
        var profile = new ColorConversionProfile();
        profile.ApplyProjector(ProjectorColorSet.Rgb);
        profile.Mode = ColorConversionMode.Direct;
        profile.ResetTableToIlda();
        return profile;
    }

    public static ColorConversionProfile CreateRgy()
    {
        var profile = new ColorConversionProfile();
        profile.ApplyProjector(ProjectorColorSet.Rgy);
        profile.Mode = ColorConversionMode.AvailableMix;
        profile.ResetTableToIlda();
        return profile;
    }

    public void ApplyProjector(ProjectorColorSet projector)
    {
        Projector = projector;
        if (projector == ProjectorColorSet.Rgb)
        {
            if (Name is "RGY" or "") Name = "RGB";
            UseRed = UseGreen = UseBlue = true;
            UseYellow = false;
            YellowRouting = IldaYellowRouting.IldaBlueAsYellow;
        }
        else
        {
            if (Name is "RGB" or "") Name = "RGY";
            UseRed = UseGreen = UseYellow = true;
            UseBlue = false;
            if (Driver == DacDriverKind.MediaLasUsbBox)
            {
                YellowRouting = IldaYellowRouting.IldaBlueAsYellow;
            }
        }
    }

    public void ResetTableToIlda()
    {
        Entries.Clear();
        var table = IldaPalette.Default64;
        for (var i = 0; i < table.Length; i++)
        {
            Entries.Add(new PaletteEntry
            {
                Index = i,
                InR = table[i][0],
                InG = table[i][1],
                InB = table[i][2],
                OutR = table[i][0],
                OutG = table[i][1],
                OutB = table[i][2]
            });
        }
    }

    public IReadOnlyList<string> DriverWarnings() => DacColorCompatibility.Warnings(this);

    public string AnalogMapSummary() => DacColorCompatibility.AnalogMapSummary(this);

    public ColorConversionProfile Clone()
    {
        var copy = new ColorConversionProfile
        {
            Name = Name,
            Enabled = Enabled,
            Projector = Projector,
            Driver = Driver,
            YellowRouting = YellowRouting,
            Mode = Mode,
            UseRed = UseRed,
            UseGreen = UseGreen,
            UseBlue = UseBlue,
            UseYellow = UseYellow,
            RedGain = RedGain,
            GreenGain = GreenGain,
            BlueGain = BlueGain,
            YellowGain = YellowGain,
            MasterBrightness = MasterBrightness,
            BlueFold = BlueFold,
            YellowFromRedGreen = YellowFromRedGreen,
            RedCurve = RedCurve.Clone(),
            GreenCurve = GreenCurve.Clone(),
            BlueCurve = BlueCurve.Clone(),
            YellowCurve = YellowCurve.Clone(),
            Alignment = (Alignment ?? new ProjectionAlignment()).Clone()
        };
        foreach (var e in Entries)
        {
            copy.Entries.Add(new PaletteEntry
            {
                Index = e.Index,
                InR = e.InR,
                InG = e.InG,
                InB = e.InB,
                OutR = e.OutR,
                OutG = e.OutG,
                OutB = e.OutB
            });
        }

        return copy;
    }
}
