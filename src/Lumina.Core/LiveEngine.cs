namespace Lumina.Core;

public sealed class LiveVoice
{
    public required int Note { get; init; }
    public required PatternItem Pattern { get; init; }
    public required DateTime StartedUtc { get; init; }
    public required int Velocity { get; init; }
}

/// <summary>Live playback: MIDI notes trigger library patterns; CCs drive effects.</summary>
public sealed class LiveEngine
{
    public MidiMap Map { get; set; } = MidiMap.Default();
    public ClipEffects Effects { get; } = new();
    public bool Latch { get; set; }
    public LiveVoice? Current { get; private set; }

    private readonly Dictionary<int, LiveVoice> _held = [];
    private PatternLibrary _library = new();

    public void Attach(PatternLibrary library) => _library = library;

    public void NoteOn(int note, int velocity, int channel)
    {
        if (velocity <= 0)
        {
            NoteOff(note, channel);
            return;
        }

        var slot = Map.SlotForNote(note, channel);
        if (slot is null || slot.Value >= _library.Items.Count) return;
        var pattern = _library.Items[slot.Value];
        var voice = new LiveVoice
        {
            Note = note,
            Pattern = pattern,
            StartedUtc = DateTime.UtcNow,
            Velocity = Math.Clamp(velocity, 1, 127)
        };

        if (Latch && _held.ContainsKey(note))
        {
            _held.Remove(note);
            Current = _held.Values.LastOrDefault();
            return;
        }

        _held[note] = voice;
        Current = voice;
    }

    public void NoteOff(int note, int channel)
    {
        if (Latch) return;
        _held.Remove(note);
        Current = _held.Values.LastOrDefault();
    }

    public void ControlChange(int number, int value, int channel)
    {
        var v = Math.Clamp(value, 0, 127) / 127.0;
        foreach (var binding in Map.MatchesCc(number, channel))
        {
            ApplyTarget(binding.Target, v, value);
        }
    }

    public void PitchBend(int value14, int channel)
    {
        foreach (var binding in Map.Bindings)
        {
            if (binding.Source != MidiSourceKind.PitchBend) continue;
            if (!MidiMap.ChannelMatches(binding.Channel, channel)) continue;
            var bipolar = (value14 - 8192) / 8192.0;
            ApplyTarget(binding.Target, (bipolar + 1) / 2.0, value14);
        }
    }

    public void Panic()
    {
        _held.Clear();
        Current = null;
    }

    public void TriggerSlot(int slot, int velocity = 100)
    {
        if (slot < 0 || slot >= _library.Items.Count) return;
        NoteOn(Map.FirstNote + slot, velocity, channel: 1);
    }

    public void ReleaseSlot(int slot)
    {
        NoteOff(Map.FirstNote + slot, channel: 1);
    }

    public LaserFrame? Render()
    {
        if (Current is null) return null;
        var elapsed = (DateTime.UtcNow - Current.StartedUtc).TotalSeconds;
        var count = Math.Max(1, Current.Pattern.Frames.Count);
        var t01 = count == 1 ? 0 : elapsed * 25.0 / count % 1.0;
        var source = Current.Pattern.FrameAtProgress(t01);
        if (source is null) return null;
        var fx = Effects.Clone();
        fx.Intensity *= Current.Velocity / 127.0;
        return EffectProcessor.Apply(source, fx);
    }

    public void LearnNote(int note, int channel, int slotIndex)
    {
        Map.Bindings.RemoveAll(b => b.Source == MidiSourceKind.Note && b.Number == note);
        Map.Bindings.Add(new MidiBinding
        {
            Source = MidiSourceKind.Note,
            Channel = Map.Channel == 0 ? 0 : channel,
            Number = note,
            Target = MidiTargetKind.PatternSlot,
            SlotIndex = slotIndex
        });
    }

    public void LearnCc(int number, int channel, MidiTargetKind target)
    {
        Map.Bindings.RemoveAll(b => b.Source == MidiSourceKind.ControlChange && b.Number == number && b.Target == target);
        Map.Bindings.Add(new MidiBinding
        {
            Source = MidiSourceKind.ControlChange,
            Channel = Map.Channel == 0 ? 0 : channel,
            Number = number,
            Target = target
        });
    }

    private void ApplyTarget(MidiTargetKind target, double unit, int raw)
    {
        switch (target)
        {
            case MidiTargetKind.Intensity:
                Effects.Intensity = unit * 2;
                break;
            case MidiTargetKind.Red:
                Effects.Red = unit;
                break;
            case MidiTargetKind.Green:
                Effects.Green = unit;
                break;
            case MidiTargetKind.Blue:
                Effects.Blue = unit;
                break;
            case MidiTargetKind.Hue:
                Effects.Hue = unit * 360 - 180;
                break;
            case MidiTargetKind.Rotation:
                Effects.Rotation = unit * 360 - 180;
                break;
            case MidiTargetKind.Width:
                Effects.Width = 0.05 + unit * 2;
                break;
            case MidiTargetKind.Height:
                Effects.Height = 0.05 + unit * 2;
                break;
            case MidiTargetKind.OffsetX:
                Effects.OffsetX = unit * 2 - 1;
                break;
            case MidiTargetKind.OffsetY:
                Effects.OffsetY = unit * 2 - 1;
                break;
            case MidiTargetKind.Blackout:
                if (raw >= 64) Panic();
                break;
        }
    }
}
