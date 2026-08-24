using Lumina.Core;

namespace Lumina.Dac;

/// <summary>Pangolin FB3 placeholder. Real output will require an SDK or bridge layer.</summary>
public sealed class Fb3Dac : ILaserDac
{
    public DacDriverKind DriverKind => DacDriverKind.Fb3Usb;
    public string DisplayName => DacColorCompatibility.DriverLabel(DriverKind);
    public DacCapabilities Capabilities { get; } = new()
    {
        Transport = DacTransportKind.Usb,
        SupportsTtl = true,
        SupportsAnalogRgb = true,
        Notes = "FB3 USB : prévoir SDK Pangolin ou bridge ouvert."
    };

    public bool IsConnected { get; private set; }

    public void Connect()
    {
        throw new NotSupportedException(
            "FB3 USB sélectionné. Le backend SDK/bridge n'est pas encore installé dans Lumina.");
    }

    public void WriteFrame(IReadOnlyList<DacPoint> points, int pointRate) =>
        throw new NotSupportedException("FB3 USB non connecté.");

    public void Blank()
    {
    }

    public void Dispose() => IsConnected = false;
}
