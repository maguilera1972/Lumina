namespace Lumina.Core;

/// <summary>
/// Calibration frames inspired by Liberation: five brightness steps (100 / 75 / 50 / 25 / 0 %).
/// Used to set diode curves before a show.
/// </summary>
public static class ColorTestPatterns
{
    public static LaserFrame Red() => Channel("TEST-R", 255, 0, 0);
    public static LaserFrame Green() => Channel("TEST-G", 0, 255, 0);
    public static LaserFrame Blue() => Channel("TEST-B", 0, 0, 255);
    public static LaserFrame Yellow() => Channel("TEST-Y", 255, 255, 0);
    public static LaserFrame White() => Channel("TEST-W", 255, 255, 255);

    public static LaserFrame ForProjector(ProjectorColorSet projector, int index) =>
        projector == ProjectorColorSet.Rgy
            ? index switch
            {
                1 => Green(),
                2 => Yellow(),
                3 => White(),
                _ => Red()
            }
            : index switch
            {
                1 => Green(),
                2 => Blue(),
                3 => White(),
                _ => Red()
            };

    private static LaserFrame Channel(string name, byte r, byte g, byte b)
    {
        var points = new List<LaserPoint>(5 * 48);
        short[] ys = [18000, 9000, 0, -9000, -18000];
        byte[] levels = [255, 191, 128, 64, 0];
        for (var line = 0; line < ys.Length; line++)
        {
            var level = levels[line];
            var lr = Scale(r, level);
            var lg = Scale(g, level);
            var lb = Scale(b, level);
            var blanked = level == 0;
            const short x0 = -22000;
            const short x1 = 22000;
            const int steps = 40;
            for (var i = 0; i <= steps; i++)
            {
                var x = (short)(x0 + (x1 - x0) * i / steps);
                points.Add(new LaserPoint(x, ys[line], 0, lr, lg, lb, level, blanked || i == 0));
            }
        }

        return new LaserFrame
        {
            Name = name,
            Company = "Lumina",
            Format = 5,
            Points = points
        };
    }

    private static byte Scale(byte channel, byte level) =>
        (byte)Math.Clamp(channel * level / 255, 0, 255);
}
