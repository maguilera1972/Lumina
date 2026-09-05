using Lumina.Core;

namespace Lumina.Core.Tests;

public class IldaSvgPreviewTests
{
    [Fact]
    public void ToSvg_EmptyFrame_StillValidSvg()
    {
        var svg = IldaSvgPreview.ToSvg(null);
        Assert.Contains("<svg", svg);
        Assert.Contains("</svg>", svg);
    }

    [Fact]
    public void ToSvg_VisibleSegment_ContainsPath()
    {
        var frame = new LaserFrame
        {
            Name = "t",
            Points =
            [
                new LaserPoint(0, 0, 0, 255, 0, 0, 255, Blanked: true),
                new LaserPoint(1000, 1000, 0, 255, 0, 0, 255, Blanked: false),
                new LaserPoint(2000, 0, 0, 255, 0, 0, 255, Blanked: false),
            ]
        };
        var svg = IldaSvgPreview.ToSvg(frame, size: 100);
        Assert.Contains("<path", svg);
        Assert.Contains("#FF0000", svg, StringComparison.OrdinalIgnoreCase);
    }
}
