using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Text;

namespace Lumina.Core;

/// <summary>One decoded OSC message (address + typed arguments).</summary>
public sealed class OscMessage
{
    public required string Address { get; init; }
    public IReadOnlyList<object> Arguments { get; init; } = [];

    public float FloatArg(int index = 0, float fallback = 0f) =>
        index < Arguments.Count
            ? Arguments[index] switch
            {
                float f => f,
                int i => i,
                double d => (float)d,
                _ => fallback
            }
            : fallback;

    public int IntArg(int index = 0, int fallback = 0) =>
        index < Arguments.Count
            ? Arguments[index] switch
            {
                int i => i,
                float f => (int)Math.Round(f),
                double d => (int)Math.Round(d),
                _ => fallback
            }
            : fallback;
}

/// <summary>Minimal OSC 1.0 packet decoder (UDP datagrams, no bundles nested deeper than needed).</summary>
public static class OscDecoder
{
    public static IReadOnlyList<OscMessage> Decode(ReadOnlySpan<byte> data)
    {
        var list = new List<OscMessage>();
        if (data.Length < 4) return list;

        if (data[0] == (byte)'#')
        {
            // Bundle: "#bundle\0" + timetag (8) + elements
            DecodeBundle(data, list);
            return list;
        }

        var msg = DecodeMessage(data);
        if (msg is not null) list.Add(msg);
        return list;
    }

    private static void DecodeBundle(ReadOnlySpan<byte> data, List<OscMessage> list)
    {
        // "#bundle\0" = 8 bytes, then 8-byte timetag
        if (data.Length < 16) return;
        var offset = 16;
        while (offset + 4 <= data.Length)
        {
            var size = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
            offset += 4;
            if (size <= 0 || offset + size > data.Length) break;
            var element = data.Slice(offset, size);
            offset += size;
            if (element.Length > 0 && element[0] == (byte)'#')
                DecodeBundle(element, list);
            else
            {
                var msg = DecodeMessage(element);
                if (msg is not null) list.Add(msg);
            }
        }
    }

    private static OscMessage? DecodeMessage(ReadOnlySpan<byte> data)
    {
        var offset = 0;
        if (!TryReadString(data, ref offset, out var address) || string.IsNullOrEmpty(address))
            return null;
        if (!address.StartsWith('/')) return null;

        if (offset >= data.Length)
            return new OscMessage { Address = address, Arguments = [] };

        if (!TryReadString(data, ref offset, out var typeTag) || typeTag.Length == 0 || typeTag[0] != ',')
            return new OscMessage { Address = address, Arguments = [] };

        var args = new List<object>();
        for (var i = 1; i < typeTag.Length; i++)
        {
            switch (typeTag[i])
            {
                case 'i':
                    if (offset + 4 > data.Length) return new OscMessage { Address = address, Arguments = args };
                    args.Add(BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4)));
                    offset += 4;
                    break;
                case 'f':
                    if (offset + 4 > data.Length) return new OscMessage { Address = address, Arguments = args };
                    args.Add(BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4))));
                    offset += 4;
                    break;
                case 's':
                    if (!TryReadString(data, ref offset, out var s)) return new OscMessage { Address = address, Arguments = args };
                    args.Add(s);
                    break;
                case 'T':
                    args.Add(true);
                    break;
                case 'F':
                    args.Add(false);
                    break;
                case 'N':
                    args.Add(null!);
                    break;
                default:
                    // Unsupported type — stop parsing further args.
                    return new OscMessage { Address = address, Arguments = args };
            }
        }

        return new OscMessage { Address = address, Arguments = args };
    }

    private static bool TryReadString(ReadOnlySpan<byte> data, ref int offset, out string value)
    {
        value = "";
        var start = offset;
        while (offset < data.Length && data[offset] != 0) offset++;
        if (offset >= data.Length) return false;
        value = Encoding.ASCII.GetString(data.Slice(start, offset - start));
        offset++; // null
        // pad to 4-byte boundary
        while ((offset % 4) != 0) offset++;
        return true;
    }
}

/// <summary>
/// Maps OSC addresses to Live actions.
/// Supported:
///   /lumina/panic
///   /lumina/pad/{n}           float/int &gt;0 = press, 0 = release (TouchOSC style)
///   /lumina/pad/{n}/press     [velocity]
///   /lumina/pad/{n}/release
///   /lumina/note              note velocity [channel]
///   /lumina/cc                cc value [channel]
/// </summary>
public static class LiveOscRouter
{
    public static void Dispatch(
        OscMessage message,
        Action<int, int> onPadPress,
        Action<int> onPadRelease,
        Action onPanic,
        Action<int, int, int> onNoteOn,
        Action<int, int, int> onCc)
    {
        var addr = message.Address.TrimEnd('/');
        if (addr.Equals("/lumina/panic", StringComparison.OrdinalIgnoreCase) ||
            addr.Equals("/panic", StringComparison.OrdinalIgnoreCase))
        {
            onPanic();
            return;
        }

        if (TryMatchPad(addr, out var slot, out var mode))
        {
            switch (mode)
            {
                case PadMode.Toggle:
                {
                    var v = message.Arguments.Count == 0 ? 1f : message.FloatArg(0);
                    if (v > 0.001f)
                    {
                        var vel = message.Arguments.Count > 0 && message.Arguments[0] is int
                            ? Math.Clamp(message.IntArg(0), 1, 127)
                            : Math.Clamp((int)Math.Round(v * 127), 1, 127);
                        onPadPress(slot, vel);
                    }
                    else
                    {
                        onPadRelease(slot);
                    }
                    break;
                }
                case PadMode.Press:
                {
                    var vel = message.Arguments.Count == 0
                        ? 120
                        : Math.Clamp(message.IntArg(0, (int)Math.Round(message.FloatArg(0) * 127)), 1, 127);
                    onPadPress(slot, vel);
                    break;
                }
                case PadMode.Release:
                    onPadRelease(slot);
                    break;
            }
            return;
        }

        if (addr.Equals("/lumina/note", StringComparison.OrdinalIgnoreCase) ||
            addr.Equals("/note", StringComparison.OrdinalIgnoreCase))
        {
            var note = message.IntArg(0);
            var velocity = message.Arguments.Count > 1 ? message.IntArg(1) : 100;
            var channel = message.Arguments.Count > 2 ? message.IntArg(2, 1) : 1;
            onNoteOn(note, velocity, channel);
            return;
        }

        if (addr.Equals("/lumina/cc", StringComparison.OrdinalIgnoreCase) ||
            addr.Equals("/cc", StringComparison.OrdinalIgnoreCase))
        {
            var cc = message.IntArg(0);
            var value = message.Arguments.Count > 1 ? message.IntArg(1) : 0;
            // TouchOSC often sends float 0..1
            if (message.Arguments.Count > 1 && message.Arguments[1] is float f)
                value = (int)Math.Round(Math.Clamp(f, 0f, 1f) * 127);
            var channel = message.Arguments.Count > 2 ? message.IntArg(2, 1) : 1;
            onCc(cc, value, channel);
        }
    }

    private enum PadMode { Toggle, Press, Release }

    private static bool TryMatchPad(string address, out int slot, out PadMode mode)
    {
        slot = 0;
        mode = PadMode.Toggle;
        // /lumina/pad/3 or /lumina/pad/3/press or /pad/3
        var parts = address.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        var padIdx = -1;
        if (parts[0].Equals("lumina", StringComparison.OrdinalIgnoreCase) &&
            parts.Length >= 3 &&
            parts[1].Equals("pad", StringComparison.OrdinalIgnoreCase))
        {
            padIdx = 2;
        }
        else if (parts[0].Equals("pad", StringComparison.OrdinalIgnoreCase) && parts.Length >= 2)
        {
            padIdx = 1;
        }
        else return false;

        if (!int.TryParse(parts[padIdx], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 1)
            return false;

        slot = n - 1; // OSC pads are 1-based
        if (parts.Length > padIdx + 1)
        {
            var verb = parts[padIdx + 1];
            if (verb.Equals("press", StringComparison.OrdinalIgnoreCase)) mode = PadMode.Press;
            else if (verb.Equals("release", StringComparison.OrdinalIgnoreCase)) mode = PadMode.Release;
            else return false;
        }

        return true;
    }
}
