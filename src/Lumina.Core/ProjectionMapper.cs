namespace Lumina.Core;

/// <summary>Maps ILDA coordinates onto the configured projection zone.</summary>
public static class ProjectionMapper
{
    public static LaserFrame? Apply(LaserFrame? frame, ProjectionAlignment? alignment)
    {
        if (frame is null) return null;
        if (alignment is null || alignment.IsIdentity) return frame;

        var points = new LaserPoint[frame.Points.Count];
        for (var i = 0; i < frame.Points.Count; i++)
        {
            points[i] = Map(frame.Points[i], alignment);
        }

        return new LaserFrame
        {
            Name = frame.Name,
            Company = frame.Company,
            FrameNumber = frame.FrameNumber,
            TotalFrames = frame.TotalFrames,
            Projector = frame.Projector,
            Format = frame.Format,
            Points = points
        };
    }

    public static LaserPoint Map(LaserPoint point, ProjectionAlignment a)
    {
        if (a.IsIdentity) return point;

        var x = point.X / 32767.0;
        var y = point.Y / 32767.0;
        if (a.FlipX) x = -x;
        if (a.FlipY) y = -y;
        x *= Math.Clamp(a.Width, 0.05, 1.5);
        y *= Math.Clamp(a.Height, 0.05, 1.5);

        if (Math.Abs(a.Rotation) >= 0.05)
        {
            var rot = a.Rotation * Math.PI / 180.0;
            var cos = Math.Cos(rot);
            var sin = Math.Sin(rot);
            var nx = x * cos - y * sin;
            var ny = x * sin + y * cos;
            x = nx;
            y = ny;
        }

        x += Math.Clamp(a.OffsetX, -0.9, 0.9);
        y += Math.Clamp(a.OffsetY, -0.9, 0.9);
        return point with { X = ToIlda(x), Y = ToIlda(y) };
    }

    private static short ToIlda(double v) =>
        (short)Math.Clamp((int)Math.Round(v * 32767.0), -32767, 32767);
}
