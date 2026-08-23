namespace Lumina.Core;

/// <summary>Best-effort MediaLas .fra reader. A file may hold one drawing or a framelist.</summary>
public static class FraReader
{
    private static readonly byte[] FrameMarker =
    [
        0x00, 0x00, 0x00, 0x00,
        0x64, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x00, 0x00
    ];

    public static IReadOnlyList<LaserFrame> Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var chunks = SplitFrames(bytes);
        if (chunks.Count == 0)
        {
            chunks.Add(bytes);
        }

        var frames = new List<LaserFrame>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            var points = ExtractPoints(chunks[i]);
            if (points.Count == 0 && chunks.Count > 1) continue;
            frames.Add(new LaserFrame
            {
                Name = chunks.Count == 1 ? name : $"{name}_{i + 1}",
                Company = "MediaLas",
                FrameNumber = i,
                TotalFrames = chunks.Count,
                Format = -1,
                Points = points
            });
        }

        if (frames.Count == 0)
        {
            frames.Add(new LaserFrame
            {
                Name = name,
                Company = "MediaLas",
                Format = -1,
                Points = []
            });
        }

        return frames;
    }

    internal static List<byte[]> SplitFrames(byte[] bytes)
    {
        var starts = new List<int>();
        for (var i = 0; i <= bytes.Length - FrameMarker.Length; i++)
        {
            var match = true;
            for (var j = 0; j < FrameMarker.Length; j++)
            {
                if (bytes[i + j] != FrameMarker[j])
                {
                    match = false;
                    break;
                }
            }

            if (match) starts.Add(i);
        }

        var chunks = new List<byte[]>();
        if (starts.Count <= 1)
        {
            return chunks;
        }

        for (var i = 0; i < starts.Count; i++)
        {
            var from = starts[i];
            var to = i + 1 < starts.Count ? starts[i + 1] : bytes.Length;
            var slice = new byte[to - from];
            Buffer.BlockCopy(bytes, from, slice, 0, slice.Length);
            chunks.Add(slice);
        }

        return chunks;
    }

    internal static IReadOnlyList<LaserPoint> ExtractPoints(byte[] bytes)
    {
        var points = new List<LaserPoint>();
        var i = 0;
        while (i + 10 < bytes.Length)
        {
            if (bytes[i + 4] == 0x00 && bytes[i + 5] == 0x08)
            {
                var x = BitConverter.ToInt16(bytes, i);
                var y = BitConverter.ToInt16(bytes, i + 2);
                var r = bytes[i + 6];
                var g = bytes[i + 7];
                var b = bytes[i + 8];
                var flags = bytes[i + 9];
                var blanked = r == 0 && g == 0 && b == 0;
                if ((flags & 0x01) != 0) blanked = r == 0 && g == 0 && b == 0;
                points.Add(new LaserPoint(x, y, 0, r, g, b, blanked ? (byte)0 : (byte)255, blanked));
                i += 11;
                continue;
            }

            i++;
        }

        return points;
    }
}
