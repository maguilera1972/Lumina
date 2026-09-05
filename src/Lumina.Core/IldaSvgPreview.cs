using System.Globalization;
using System.Text;

namespace Lumina.Core;

/// <summary>Renders a laser frame as a compact SVG preview for web / remote pads.</summary>
public static class IldaSvgPreview
{
    public static string ToSvg(LaserFrame? frame, int size = 160, double strokeWidth = 1.2)
    {
        var sb = new StringBuilder(2048);
        var s = Math.Clamp(size, 48, 512).ToString(CultureInfo.InvariantCulture);
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
          .Append(s).Append(' ').Append(s)
          .Append("\" width=\"").Append(s).Append("\" height=\"").Append(s).Append("\">");
        sb.Append("<rect width=\"100%\" height=\"100%\" rx=\"10\" fill=\"#070B12\" stroke=\"#1E6A78\" stroke-width=\"1\"/>");

        if (frame is null || frame.Points.Count == 0)
        {
            sb.Append("</svg>");
            return sb.ToString();
        }

        double MapX(short x) => (x + 32768.0) / 65535.0 * size;
        double MapY(short y) => (1.0 - (y + 32768.0) / 65535.0) * size;

        var inv = CultureInfo.InvariantCulture;
        string F(double v) => v.ToString("0.#", inv);

        var path = new StringBuilder();
        byte lastR = 0, lastG = 0, lastB = 0;
        var haveColor = false;
        var penDown = false;
        double lastX = 0, lastY = 0;

        void FlushPath()
        {
            if (path.Length == 0) return;
            var color = $"#{lastR:X2}{lastG:X2}{lastB:X2}";
            sb.Append("<path d=\"").Append(path).Append("\" fill=\"none\" stroke=\"")
              .Append(color).Append("\" stroke-width=\"")
              .Append(strokeWidth.ToString("0.##", inv))
              .Append("\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
            path.Clear();
            penDown = false;
        }

        foreach (var p in frame.Points)
        {
            var x = MapX(p.X);
            var y = MapY(p.Y);
            if (p.IsVisible && haveColor)
            {
                if (p.R != lastR || p.G != lastG || p.B != lastB)
                {
                    FlushPath();
                    lastR = p.R;
                    lastG = p.G;
                    lastB = p.B;
                    path.Append('M').Append(F(lastX)).Append(' ').Append(F(lastY));
                    penDown = true;
                }

                if (!penDown)
                {
                    path.Append('M').Append(F(lastX)).Append(' ').Append(F(lastY));
                    penDown = true;
                }

                path.Append('L').Append(F(x)).Append(' ').Append(F(y));
            }
            else
            {
                FlushPath();
            }

            lastX = x;
            lastY = y;
            lastR = p.R;
            lastG = p.G;
            lastB = p.B;
            haveColor = true;
        }

        FlushPath();
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>Downsample dense frames so remote SVG stays light.</summary>
    public static LaserFrame? SampleForPreview(LaserFrame? frame, int maxPoints = 800)
    {
        if (frame is null || frame.Points.Count <= maxPoints) return frame;
        var step = Math.Max(1, frame.Points.Count / maxPoints);
        var pts = new List<LaserPoint>(maxPoints + 8);
        for (var i = 0; i < frame.Points.Count; i += step)
            pts.Add(frame.Points[i]);
        var lastIdx = frame.Points.Count - 1;
        if ((lastIdx % step) != 0)
            pts.Add(frame.Points[lastIdx]);
        return new LaserFrame
        {
            Name = frame.Name,
            Company = frame.Company,
            FrameNumber = frame.FrameNumber,
            TotalFrames = frame.TotalFrames,
            Projector = frame.Projector,
            Format = frame.Format,
            Points = pts
        };
    }
}
