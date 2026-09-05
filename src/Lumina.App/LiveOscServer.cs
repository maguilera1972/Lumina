using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Lumina.Core;

namespace Lumina.App;

/// <summary>UDP OSC listener for Live pads (TouchOSC / similar).</summary>
public sealed class LiveOscServer : IDisposable
{
    private UdpClient? _udp;
    private CancellationTokenSource _cts = new();

    private readonly Action<int, int> _onPadPress;
    private readonly Action<int> _onPadRelease;
    private readonly Action _onPanic;
    private readonly Action<int, int, int> _onNoteOn;
    private readonly Action<int, int, int> _onCc;

    public int Port { get; private set; }
    public bool IsRunning => _udp is not null;

    public LiveOscServer(
        Action<int, int> onPadPress,
        Action<int> onPadRelease,
        Action onPanic,
        Action<int, int, int> onNoteOn,
        Action<int, int, int> onCc)
    {
        _onPadPress = onPadPress;
        _onPadRelease = onPadRelease;
        _onPanic = onPanic;
        _onNoteOn = onNoteOn;
        _onCc = onCc;
    }

    public void Start(int port)
    {
        if (port is <= 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        Stop();
        _cts = new CancellationTokenSource();
        Port = port;
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        _ = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts.Cancel();
        try { _udp?.Close(); } catch { /* ignore */ }
        _udp = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udp is not null)
        {
            try
            {
                var result = await _udp.ReceiveAsync(ct);
                foreach (var msg in OscDecoder.Decode(result.Buffer))
                {
                    LiveOscRouter.Dispatch(
                        msg,
                        _onPadPress,
                        _onPadRelease,
                        _onPanic,
                        _onNoteOn,
                        _onCc);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (ct.IsCancellationRequested) break;
                await Task.Delay(20, CancellationToken.None);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }

    /// <summary>Encode a simple float message (for tests / tooling).</summary>
    public static byte[] EncodeFloat(string address, float value)
    {
        using var ms = new MemoryStream();
        WritePaddedString(ms, address);
        WritePaddedString(ms, ",f");
        var bits = BitConverter.SingleToInt32Bits(value);
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, bits);
        ms.Write(buf);
        return ms.ToArray();
    }

    private static void WritePaddedString(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes);
        stream.WriteByte(0);
        var pad = (4 - ((bytes.Length + 1) % 4)) % 4;
        for (var i = 0; i < pad; i++) stream.WriteByte(0);
    }
}
