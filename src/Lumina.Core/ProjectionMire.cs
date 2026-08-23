namespace Lumina.Core;

/// <summary>Geometric test pattern used to size and align the projection on the wall.</summary>
public static class ProjectionMire
{
    public static LaserFrame Grid()
    {
        var points = new List<LaserPoint>(512);
        const byte r = 255, g = 255, b = 255;
        const short outer = 30000;
        const short inner = 15000;

        Stroke(points, Rect(outer), r, g, b);
        Stroke(points, Rect(inner), r, g, b);
        Stroke(points, [(-outer, 0), (outer, 0)], r, g, b);
        Stroke(points, [(0, -outer), (0, outer)], r, g, b);

        for (var i = -2; i <= 2; i++)
        {
            if (i == 0) continue;
            var t = (short)(outer * i / 2);
            Stroke(points, [(t, (short)(-outer / 8)), (t, (short)(outer / 8))], r, g, b);
            Stroke(points, [((short)(-outer / 8), t), ((short)(outer / 8), t)], r, g, b);
        }

        Corner(points, -outer, -outer, 1, 1, r, g, b);
        Corner(points, outer, -outer, -1, 1, r, g, b);
        Corner(points, -outer, outer, 1, -1, r, g, b);
        Corner(points, outer, outer, -1, -1, r, g, b);

        const int circle = 48;
        var ring = new (short x, short y)[circle + 1];
        for (var i = 0; i <= circle; i++)
        {
            var a = i / (double)circle * Math.PI * 2;
            ring[i] = ((short)(inner * Math.Cos(a)), (short)(inner * Math.Sin(a)));
        }

        Stroke(points, ring, r, g, b);

        return new LaserFrame
        {
            Name = "MIRE",
            Company = "Lumina",
            Format = 5,
            Points = points
        };
    }

    private static (short x, short y)[] Rect(short s) =>
    [
        ((short)-s, (short)-s), (s, (short)-s), (s, s), ((short)-s, s), ((short)-s, (short)-s)
    ];

    private static void Corner(List<LaserPoint> points, short x, short y, int dx, int dy, byte r, byte g, byte b)
    {
        const short arm = 4000;
        Stroke(points, [(x, y), ((short)(x + dx * arm), y)], r, g, b);
        Stroke(points, [(x, y), (x, (short)(y + dy * arm))], r, g, b);
    }

    private static void Stroke(List<LaserPoint> points, IReadOnlyList<(short x, short y)> path, byte r, byte g, byte b)
    {
        for (var i = 0; i < path.Count; i++)
        {
            var (x, y) = path[i];
            points.Add(new LaserPoint(x, y, 0, r, g, b, 255, i == 0));
        }
    }
}
