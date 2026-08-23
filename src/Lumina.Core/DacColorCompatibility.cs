namespace Lumina.Core;

/// <summary>
/// Keeps colour conversion aligned with the DAC that will actually stream points.
/// MediaLas USB-Box (FTDI D2XX) is a 3-channel ILDA analog RGB box: no fourth yellow pin.
/// </summary>
public static class DacColorCompatibility
{
    public const string UsbBoxDriverName = "MediaLas USB-Box (FTDI D2XX, analogique ILDA)";

    /// <summary>ILDA / USB-Box analogue: pin 5 red, pin 6 green, pin 7 blue.</summary>
    public static string AnalogMapSummary(ColorConversionProfile profile)
    {
        if (profile.Projector == ProjectorColorSet.Rgy)
        {
            return profile.YellowRouting == IldaYellowRouting.MixOnRedGreen
                ? "RGY · broche 5 = rouge, 6 = vert, 7 = muette (jaune approximé R+V)."
                : "RGY · broche 5 = rouge, 6 = vert, 7 = jaune (voie ILDA bleu → diode jaune).";
        }

        return "RGB · broche 5 = rouge, 6 = vert, 7 = bleu (USB-Box / ILDA 1:1).";
    }

    public static IReadOnlyList<string> Warnings(ColorConversionProfile profile)
    {
        var list = new List<string>();
        if (profile.Driver == DacDriverKind.MediaLasUsbBox &&
            profile.Projector == ProjectorColorSet.Rgy &&
            profile.YellowRouting != IldaYellowRouting.IldaBlueAsYellow)
        {
            list.Add(
                "USB-Box n’a que trois sorties analogiques couleur. Pour un projecteur RGY, le jaune doit aller sur la broche ILDA bleu.");
        }

        if (profile.Projector == ProjectorColorSet.Rgb && profile.UseYellow)
        {
            list.Add("Profil RGB : la diode jaune est ignorée. Passez en RGY si le projecteur n’a pas de bleu.");
        }

        if (profile.Projector == ProjectorColorSet.Rgy && profile.UseBlue)
        {
            list.Add("Profil RGY : pas de diode bleue. Le bleu source est replié vers le jaune.");
        }

        return list;
    }

    public static IldaYellowRouting RoutingForDriver(DacDriverKind driver, ProjectorColorSet projector)
    {
        if (projector == ProjectorColorSet.Rgy && driver == DacDriverKind.MediaLasUsbBox)
        {
            return IldaYellowRouting.IldaBlueAsYellow;
        }

        return projector == ProjectorColorSet.Rgy
            ? IldaYellowRouting.IldaBlueAsYellow
            : IldaYellowRouting.MixOnRedGreen;
    }
}
