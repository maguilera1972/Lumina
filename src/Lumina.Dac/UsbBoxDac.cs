namespace Lumina.Dac;

/// <summary>
/// Placeholder for MediaLas USB-Box (FTDI VID 0403, PID 6001 / AA70 / 6010).
/// Talks D2XX once the streaming protocol is confirmed on a physical unit.
/// Colour: three ILDA analog channels (R/G/B). RGY heads must map yellow onto the blue pin
/// via <see cref="Lumina.Core.ColorConversionProfile"/>.
/// </summary>
public sealed class UsbBoxDac : ILaserDac
{
    public const int VendorId = 0x0403;
    public static readonly int[] ProductIds = [0x6001, 0xAA70, 0x6010];

    public string DisplayName => Lumina.Core.DacColorCompatibility.UsbBoxDriverName;
    public bool IsConnected { get; private set; }

    public void Connect()
    {
        throw new NotSupportedException(
            "Le protocole USB-Box n'est pas encore branché. Utilisez la prévisualisation, ou branchez la Box pour la prochaine étape matériel.");
    }

    public void WriteFrame(IReadOnlyList<DacPoint> points, int pointRate) =>
        throw new NotSupportedException("USB-Box non connectée.");

    public void Blank()
    {
    }

    public void Dispose() => IsConnected = false;
}
