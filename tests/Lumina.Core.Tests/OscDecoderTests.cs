using System.Buffers.Binary;
using System.Text;
using Lumina.Core;

namespace Lumina.Core.Tests;

public class OscDecoderTests
{
    [Fact]
    public void Decode_FloatPadPress_RoutesToPress()
    {
        var packet = Encode("/lumina/pad/2", ",f", 1f);
        var messages = OscDecoder.Decode(packet);
        Assert.Single(messages);
        Assert.Equal("/lumina/pad/2", messages[0].Address);

        int? pressed = null;
        LiveOscRouter.Dispatch(
            messages[0],
            onPadPress: (slot, _) => pressed = slot,
            onPadRelease: _ => { },
            onPanic: () => { },
            onNoteOn: (_, _, _) => { },
            onCc: (_, _, _) => { });

        Assert.Equal(1, pressed); // pad 2 → slot index 1
    }

    [Fact]
    public void Decode_PadRelease_FloatZero()
    {
        var packet = Encode("/pad/1", ",f", 0f);
        var messages = OscDecoder.Decode(packet);
        int? released = null;
        LiveOscRouter.Dispatch(
            messages[0],
            onPadPress: (_, _) => { },
            onPadRelease: slot => released = slot,
            onPanic: () => { },
            onNoteOn: (_, _, _) => { },
            onCc: (_, _, _) => { });
        Assert.Equal(0, released);
    }

    [Fact]
    public void Decode_Panic_NoArgs()
    {
        var packet = EncodeAddressOnly("/lumina/panic");
        var panic = false;
        LiveOscRouter.Dispatch(
            OscDecoder.Decode(packet)[0],
            onPadPress: (_, _) => { },
            onPadRelease: _ => { },
            onPanic: () => panic = true,
            onNoteOn: (_, _, _) => { },
            onCc: (_, _, _) => { });
        Assert.True(panic);
    }

    private static byte[] Encode(string address, string typeTag, float value)
    {
        using var ms = new MemoryStream();
        WriteString(ms, address);
        WriteString(ms, typeTag);
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, BitConverter.SingleToInt32Bits(value));
        ms.Write(buf);
        return ms.ToArray();
    }

    private static byte[] EncodeAddressOnly(string address)
    {
        using var ms = new MemoryStream();
        WriteString(ms, address);
        WriteString(ms, ",");
        return ms.ToArray();
    }

    private static void WriteString(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes);
        stream.WriteByte(0);
        var pad = (4 - ((bytes.Length + 1) % 4)) % 4;
        for (var i = 0; i < pad; i++) stream.WriteByte(0);
    }
}
