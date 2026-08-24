using Lumina.Core;

namespace Lumina.Dac;

/// <summary>
/// Placeholder for MediaLas USB-Box (FTDI VID 0403, PID 6001 / AA70 / 6010).
/// Talks D2XX once the streaming protocol is confirmed on hardware.
/// </summary>
public sealed class UsbBoxDac : ILaserDac
{
    public const int VendorId = 0x0403;
    public static readonly int[] ProductIds = [0x6001, 0xAA70, 0x6010];

    public DacDriverKind DriverKind => DacDriverKind.MediaLasUsbBox;
    public string DisplayName => Lumina.Core.DacColorCompatibility.DriverLabel(DriverKind);
    public DacCapabilities Capabilities { get; } = new()
    {
        Transport = DacTransportKind.Usb,
        SupportsTtl = true,
        SupportsAnalogRgb = true,
        Notes = "USB-Box MediaLas analogique ILDA, protocole encore non branché."
    };

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
