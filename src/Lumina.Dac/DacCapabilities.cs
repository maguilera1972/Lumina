namespace Lumina.Dac;

public enum DacTransportKind
{
    Software,
    Usb,
    Network,
    Bridge
}

public sealed class DacCapabilities
{
    public DacTransportKind Transport { get; init; }
    public bool SupportsTtl { get; init; }
    public bool SupportsAnalogRgb { get; init; } = true;
    public bool SupportsNetworkDiscovery { get; init; }
    public int MaxProjectors { get; init; } = 1;
    public string Notes { get; init; } = "";
}
