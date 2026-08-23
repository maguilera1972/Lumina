using NAudio.Midi;

namespace Lumina.App;

public sealed class MidiDeviceInfo
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public override string ToString() => Name;
}

/// <summary>Windows MIDI input via NAudio. Events are raised on the capturing thread.</summary>
public sealed class MidiInputService : IDisposable
{
    private MidiIn? _input;

    public event Action<MidiLiveMessage>? MessageReceived;
    public string? DeviceName { get; private set; }
    public bool IsOpen => _input is not null;

    public static IReadOnlyList<MidiDeviceInfo> ListDevices()
    {
        var list = new List<MidiDeviceInfo>();
        for (var i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            list.Add(new MidiDeviceInfo { Index = i, Name = MidiIn.DeviceInfo(i).ProductName });
        }

        return list;
    }

    public void Open(int deviceIndex)
    {
        Close();
        _input = new MidiIn(deviceIndex);
        DeviceName = MidiIn.DeviceInfo(deviceIndex).ProductName;
        _input.MessageReceived += OnMessage;
        _input.ErrorReceived += (_, _) => { };
        _input.Start();
    }

    public void Close()
    {
        if (_input is null) return;
        try
        {
            _input.Stop();
            _input.MessageReceived -= OnMessage;
            _input.Dispose();
        }
        catch
        {
            // Device unplugged.
        }

        _input = null;
        DeviceName = null;
    }

    public void Dispose() => Close();

    private void OnMessage(object? sender, MidiInMessageEventArgs e)
    {
        var ev = MidiEvent.FromRawMessage(e.RawMessage);
        var channel = ev.Channel;
        switch (ev)
        {
            case NoteOnEvent noteOn:
                MessageReceived?.Invoke(new MidiLiveMessage(
                    noteOn.Velocity == 0 ? MidiLiveKind.NoteOff : MidiLiveKind.NoteOn,
                    channel,
                    noteOn.NoteNumber,
                    noteOn.Velocity));
                break;
            case NoteEvent noteOff when noteOff.CommandCode == MidiCommandCode.NoteOff:
                MessageReceived?.Invoke(new MidiLiveMessage(MidiLiveKind.NoteOff, channel, noteOff.NoteNumber, 0));
                break;
            case ControlChangeEvent cc:
                MessageReceived?.Invoke(new MidiLiveMessage(MidiLiveKind.ControlChange, channel, (int)cc.Controller, cc.ControllerValue));
                break;
            case PitchWheelChangeEvent pb:
                MessageReceived?.Invoke(new MidiLiveMessage(MidiLiveKind.PitchBend, channel, 0, pb.Pitch));
                break;
        }
    }
}

public enum MidiLiveKind
{
    NoteOn,
    NoteOff,
    ControlChange,
    PitchBend
}

public readonly record struct MidiLiveMessage(MidiLiveKind Kind, int Channel, int Number, int Value);
