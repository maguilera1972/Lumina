using Lumina.Core;

namespace Lumina.Dac;

/// <summary>Software-only output used until a hardware DAC is selected.</summary>
public sealed class PreviewDac : ILaserDac
{
    public DacDriverKind DriverKind => DacDriverKind.Preview;
    public string DisplayName => "Prévisualisation (pas de DAC)";
    public DacCapabilities Capabilities { get; } = new()
    {
        Transport = DacTransportKind.Software,
        SupportsAnalogRgb = false,
        Notes = "Aperçu écran uniquement."
    };

    public bool IsConnected { get; private set; }
    public DacPoint[] LastFrame { get; private set; } = [];
    public event Action<DacPoint[]>? FrameArrived;

    public void Connect() => IsConnected = true;

    public void WriteFrame(IReadOnlyList<DacPoint> points, int pointRate)
    {
        LastFrame = points as DacPoint[] ?? points.ToArray();
        FrameArrived?.Invoke(LastFrame);
    }

    public void Blank()
    {
        LastFrame = [];
        FrameArrived?.Invoke(LastFrame);
    }

    public void Dispose() => IsConnected = false;
}
