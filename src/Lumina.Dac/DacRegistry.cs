using Lumina.Core;

namespace Lumina.Dac;

public static class DacRegistry
{
    public static IReadOnlyList<DacDriverKind> SupportedDrivers { get; } =
    [
        DacDriverKind.Preview,
        DacDriverKind.MediaLasUsbBox,
        DacDriverKind.Fb3Usb,
        DacDriverKind.Fb4Network,
        DacDriverKind.OpenDacBridge
    ];

    public static ILaserDac Create(DacDriverKind kind) => kind switch
    {
        DacDriverKind.MediaLasUsbBox => new UsbBoxDac(),
        DacDriverKind.Fb3Usb => new Fb3Dac(),
        DacDriverKind.Fb4Network => new Fb4Dac(),
        DacDriverKind.OpenDacBridge => new OpenDacBridgeDac(),
        _ => new PreviewDac()
    };
}
