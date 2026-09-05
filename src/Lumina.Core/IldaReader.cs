namespace Lumina.Core;

/// <summary>ILDA Image Data Transfer Format (revisions including true-color 4/5).</summary>
public static class IldaReader
{
    public static bool LooksLikeIlda(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length < 32) return false;
            using var stream = File.OpenRead(path);
            Span<byte> magic = stackalloc byte[4];
            var n = stream.Read(magic);
            return n == 4 && magic[0] == (byte)'I' && magic[1] == (byte)'L' && magic[2] == (byte)'D' && magic[3] == (byte)'A';
        }
        catch
        {
            return false;
        }
    }

    public static IReadOnlyList<LaserFrame> Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static IReadOnlyList<LaserFrame> Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        var frames = new List<LaserFrame>();
        var palette = IldaPalette.Default64.ToArray();

        while (stream.Position + 32 <= stream.Length)
        {
            var magic = reader.ReadBytes(4);
            if (magic.Length < 4 || magic[0] != (byte)'I' || magic[1] != (byte)'L' || magic[2] != (byte)'D' || magic[3] != (byte)'A')
            {
                // Trailing padding / non-ILDA bytes after valid frames: stop cleanly.
                if (frames.Count > 0) break;
                throw new InvalidDataException("Fichier ILDA invalide (en-tête ILDA manquant).");
            }

            reader.ReadBytes(3); // reserved
            var format = reader.ReadByte();
            var name = ReadFixed(reader, 8);
            var company = ReadFixed(reader, 8);
            var count = ReadUInt16Be(reader);
            var frameNumber = ReadUInt16Be(reader);
            var totalFrames = ReadUInt16Be(reader);
            var projector = reader.ReadByte();
            reader.ReadByte(); // reserved

            if (count == 0)
            {
                break;
            }

            switch (format)
            {
                case 0:
                    frames.Add(ReadIndexed(reader, name, company, frameNumber, totalFrames, projector, count, palette, format, threeD: true));
                    break;
                case 1:
                    frames.Add(ReadIndexed(reader, name, company, frameNumber, totalFrames, projector, count, palette, format, threeD: false));
                    break;
                case 2:
                    palette = ReadPalette(reader, count, palette);
                    break;
                case 4:
                    frames.Add(ReadTrueColor(reader, name, company, frameNumber, totalFrames, projector, count, format, threeD: true));
                    break;
                case 5:
                    frames.Add(ReadTrueColor(reader, name, company, frameNumber, totalFrames, projector, count, format, threeD: false));
                    break;
                default:
                    if (frames.Count > 0) return frames;
                    throw new InvalidDataException($"Format ILDA {format} non supporté.");
            }
        }

        return frames;
    }

    private static LaserFrame ReadIndexed(
        BinaryReader reader,
        string name,
        string company,
        int frameNumber,
        int totalFrames,
        int projector,
        int count,
        byte[][] palette,
        int format,
        bool threeD)
    {
        var points = new LaserPoint[count];
        for (var i = 0; i < count; i++)
        {
            var x = ReadInt16Be(reader);
            var y = ReadInt16Be(reader);
            var z = threeD ? ReadInt16Be(reader) : (short)0;
            var status = reader.ReadByte();
            var colorIndex = reader.ReadByte();
            var blanked = (status & 0x40) != 0;
            var rgb = palette[colorIndex % palette.Length];
            points[i] = new LaserPoint(x, y, z, rgb[0], rgb[1], rgb[2], blanked ? (byte)0 : (byte)255, blanked);
        }

        return new LaserFrame
        {
            Name = name,
            Company = company,
            FrameNumber = frameNumber,
            TotalFrames = totalFrames,
            Projector = projector,
            Format = format,
            Points = points
        };
    }

    private static LaserFrame ReadTrueColor(
        BinaryReader reader,
        string name,
        string company,
        int frameNumber,
        int totalFrames,
        int projector,
        int count,
        int format,
        bool threeD)
    {
        var points = new LaserPoint[count];
        for (var i = 0; i < count; i++)
        {
            var x = ReadInt16Be(reader);
            var y = ReadInt16Be(reader);
            var z = threeD ? ReadInt16Be(reader) : (short)0;
            var status = reader.ReadByte();
            var r = reader.ReadByte();
            var g = reader.ReadByte();
            var b = reader.ReadByte();
            var blanked = (status & 0x40) != 0;
            points[i] = new LaserPoint(x, y, z, r, g, b, blanked ? (byte)0 : (byte)255, blanked);
        }

        return new LaserFrame
        {
            Name = name,
            Company = company,
            FrameNumber = frameNumber,
            TotalFrames = totalFrames,
            Projector = projector,
            Format = format,
            Points = points
        };
    }

    private static byte[][] ReadPalette(BinaryReader reader, int count, byte[][] current)
    {
        var palette = current.ToArray();
        for (var i = 0; i < count; i++)
        {
            var r = reader.ReadByte();
            var g = reader.ReadByte();
            var b = reader.ReadByte();
            if (i < palette.Length)
            {
                palette[i] = [r, g, b];
            }
        }

        return palette;
    }

    private static string ReadFixed(BinaryReader reader, int length)
    {
        var bytes = reader.ReadBytes(length);
        var n = Array.IndexOf(bytes, (byte)0);
        if (n < 0) n = bytes.Length;
        return System.Text.Encoding.ASCII.GetString(bytes, 0, n).Trim();
    }

    private static ushort ReadUInt16Be(BinaryReader reader)
    {
        var hi = reader.ReadByte();
        var lo = reader.ReadByte();
        return (ushort)((hi << 8) | lo);
    }

    private static short ReadInt16Be(BinaryReader reader) => unchecked((short)ReadUInt16Be(reader));
}
