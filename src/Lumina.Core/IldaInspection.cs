namespace Lumina.Core;

public static class IldaInspection
{
    public static IldaFrameSummary Summarize(LaserFrame frame, int index)
    {
        var summary = new IldaFrameSummary
        {
            Index = index,
            Name = string.IsNullOrWhiteSpace(frame.Name) ? $"(frame {index + 1})" : frame.Name,
            Company = frame.Company,
            Format = frame.Format,
            FormatLabel = frame.FormatLabel,
            PointCount = frame.Points.Count,
            VisibleCount = frame.VisiblePointCount,
            Projector = frame.Projector,
            FrameNumber = frame.FrameNumber,
            TotalFrames = frame.TotalFrames
        };

        if (frame.Points.Count == 0) return summary;

        short minX = short.MaxValue, maxX = short.MinValue, minY = short.MaxValue, maxY = short.MinValue;
        foreach (var p in frame.Points)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
            if (p.Blanked) summary.BlankedCount++;
        }

        summary.MinX = minX;
        summary.MaxX = maxX;
        summary.MinY = minY;
        summary.MaxY = maxY;
        return summary;
    }
}

public sealed class IldaFrameSummary
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Company { get; set; } = "";
    public int Format { get; set; }
    public string FormatLabel { get; set; } = "";
    public int PointCount { get; set; }
    public int VisibleCount { get; set; }
    public int BlankedCount { get; set; }
    public int Projector { get; set; }
    public int FrameNumber { get; set; }
    public int TotalFrames { get; set; }
    public short MinX { get; set; }
    public short MaxX { get; set; }
    public short MinY { get; set; }
    public short MaxY { get; set; }

    public string BoundsText => PointCount == 0 ? "—" : $"X {MinX}…{MaxX}  Y {MinY}…{MaxY}";
}
