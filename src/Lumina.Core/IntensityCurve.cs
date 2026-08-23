namespace Lumina.Core;

/// <summary>
/// Per-diode brightness curve (0 / 25 / 50 / 75 / 100 %), same idea as Liberation colour calibration.
/// Input and output are 0–1. Identity leaves the channel unchanged.
/// </summary>
public sealed class IntensityCurve
{
    public double C0 { get; set; }
    public double C25 { get; set; } = 0.25;
    public double C50 { get; set; } = 0.5;
    public double C75 { get; set; } = 0.75;
    public double C100 { get; set; } = 1;

    public bool IsIdentity =>
        C0 == 0 && C25 == 0.25 && C50 == 0.5 && C75 == 0.75 && C100 == 1;

    public double Map(double x)
    {
        x = Math.Clamp(x, 0, 1);
        if (x <= 0.25) return Lerp(C0, C25, x / 0.25);
        if (x <= 0.50) return Lerp(C25, C50, (x - 0.25) / 0.25);
        if (x <= 0.75) return Lerp(C50, C75, (x - 0.50) / 0.25);
        return Lerp(C75, C100, (x - 0.75) / 0.25);
    }

    public void Reset()
    {
        C0 = 0;
        C25 = 0.25;
        C50 = 0.5;
        C75 = 0.75;
        C100 = 1;
    }

    public IntensityCurve Clone() => new()
    {
        C0 = C0,
        C25 = C25,
        C50 = C50,
        C75 = C75,
        C100 = C100
    };

    private static double Lerp(double a, double b, double t) =>
        a + (b - a) * Math.Clamp(t, 0, 1);
}
