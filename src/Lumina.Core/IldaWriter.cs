namespace Lumina.Core;

/// <summary>Writes a minimal ILDA format-5 (2D true color) file. Used by tests and future export.</summary>
public static class IldaWriter
{
    public static byte[] Write(IReadOnlyList<LaserFrame> frames)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        var total = frames.Count;
        for (var i = 0; i < frames.Count; i++)
        {
            WriteFrame(writer, frames[i], i, total);
        }

        WriteHeader(writer, format: 5, name: "", company: "Lumina", count: 0, frameNumber: 0, totalFrames: total, projector: 0);
        return ms.ToArray();
    }

    public static void WriteFile(string path, IReadOnlyList<LaserFrame> frames) =>
        File.WriteAllBytes(path, Write(frames));

    private static void WriteFrame(BinaryWriter writer, LaserFrame frame, int index, int total)
    {
        WriteHeader(writer, 5, frame.Name, "Lumina", frame.Points.Count, index, total, frame.Projector);
        for (var i = 0; i < frame.Points.Count; i++)
        {
            var p = frame.Points[i];
            WriteInt16Be(writer, p.X);
            WriteInt16Be(writer, p.Y);
            var status = (byte)(p.Blanked ? 0x40 : 0x00);
            if (i == frame.Points.Count - 1)
            {
                status |= 0x80;
            }

            writer.Write(status);
            writer.Write(p.R);
            writer.Write(p.G);
            writer.Write(p.B);
        }
    }

    private static void WriteHeader(
        BinaryWriter writer,
        byte format,
        string name,
        string company,
        int count,
        int frameNumber,
        int totalFrames,
        int projector)
    {
        writer.Write("ILDA"u8);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write(format);
        WriteFixed(writer, name, 8);
        WriteFixed(writer, company, 8);
        WriteUInt16Be(writer, (ushort)count);
        WriteUInt16Be(writer, (ushort)frameNumber);
        WriteUInt16Be(writer, (ushort)totalFrames);
        writer.Write((byte)projector);
        writer.Write((byte)0);
    }

    private static void WriteFixed(BinaryWriter writer, string text, int length)
    {
        var bytes = new byte[length];
        var raw = System.Text.Encoding.ASCII.GetBytes(text ?? "");
        Array.Copy(raw, bytes, Math.Min(raw.Length, length));
        writer.Write(bytes);
    }

    private static void WriteUInt16Be(BinaryWriter writer, ushort value)
    {
        writer.Write((byte)(value >> 8));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteInt16Be(BinaryWriter writer, short value) => WriteUInt16Be(writer, unchecked((ushort)value));
}
