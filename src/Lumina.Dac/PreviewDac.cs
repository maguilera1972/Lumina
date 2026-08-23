namespace Lumina.Dac;

/// <summary>Software-only output used until the USB-Box protocol is validated on hardware.</summary>
public sealed class PreviewDac : ILaserDac
{
    public string DisplayName => "Prévisualisation (pas de DAC)";
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
