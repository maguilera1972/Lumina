using Lumina.Core;

namespace Lumina.Dac;

/// <summary>Pangolin FB4 placeholder with planned OSC/network backend.</summary>
public sealed class Fb4Dac : ILaserDac
{
    public DacDriverKind DriverKind => DacDriverKind.Fb4Network;
    public string DisplayName => DacColorCompatibility.DriverLabel(DriverKind);
    public DacCapabilities Capabilities { get; } = new()
    {
        Transport = DacTransportKind.Network,
        SupportsTtl = true,
        SupportsAnalogRgb = true,
        SupportsNetworkDiscovery = true,
        MaxProjectors = 8,
        Notes = "FB4 réseau : backend OSC / bridge à brancher."
    };

    public bool IsConnected { get; private set; }

    public void Connect()
    {
        throw new NotSupportedException(
            "FB4 réseau sélectionné. Le backend OSC / bridge n'est pas encore branché dans Lumina.");
    }

    public void WriteFrame(IReadOnlyList<DacPoint> points, int pointRate) =>
        throw new NotSupportedException("FB4 réseau non connecté.");

    public void Blank()
    {
    }

    public void Dispose() => IsConnected = false;
}
