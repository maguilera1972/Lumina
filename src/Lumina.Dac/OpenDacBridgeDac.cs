using Lumina.Core;

namespace Lumina.Dac;

/// <summary>Generic hook for future open DAC bridges or network translators.</summary>
public sealed class OpenDacBridgeDac : ILaserDac
{
    public DacDriverKind DriverKind => DacDriverKind.OpenDacBridge;
    public string DisplayName => DacColorCompatibility.DriverLabel(DriverKind);
    public DacCapabilities Capabilities { get; } = new()
    {
        Transport = DacTransportKind.Bridge,
        SupportsTtl = true,
        SupportsAnalogRgb = true,
        SupportsNetworkDiscovery = true,
        MaxProjectors = 8,
        Notes = "Backend générique pour DAC ouverts / ponts réseau."
    };

    public bool IsConnected { get; private set; }

    public void Connect()
    {
        throw new NotSupportedException(
            "DAC ouvert / bridge sélectionné. Configurez ou implémentez d'abord le backend correspondant.");
    }

    public void WriteFrame(IReadOnlyList<DacPoint> points, int pointRate) =>
        throw new NotSupportedException("Bridge DAC ouvert non connecté.");

    public void Blank()
    {
    }

    public void Dispose() => IsConnected = false;
}
