namespace Lumina.Core;

public enum MidiSourceKind
{
    Note,
    ControlChange,
    PitchBend
}

public enum MidiTargetKind
{
    PatternSlot,
    Intensity,
    Red,
    Green,
    Blue,
    Hue,
    Rotation,
    Width,
    Height,
    OffsetX,
    OffsetY,
    Blackout
}

public sealed class MidiBinding
{
    public MidiSourceKind Source { get; set; }
    /// <summary>0 = all channels, 1-16 = filter.</summary>
    public int Channel { get; set; }
    public int Number { get; set; }
    public MidiTargetKind Target { get; set; }
    public int SlotIndex { get; set; }
}

public sealed class MidiMap
{
    public int Channel { get; set; }
    public int FirstNote { get; set; } = 36;
    public List<MidiBinding> Bindings { get; set; } = [];

    public static MidiMap Default()
    {
        var map = new MidiMap { Channel = 0, FirstNote = 36 };
        void Cc(int number, MidiTargetKind target) =>
            map.Bindings.Add(new MidiBinding
            {
                Source = MidiSourceKind.ControlChange,
                Number = number,
                Target = target
            });

        Cc(1, MidiTargetKind.Intensity);
        Cc(11, MidiTargetKind.Intensity);
        Cc(16, MidiTargetKind.Hue);
        Cc(17, MidiTargetKind.Rotation);
        Cc(18, MidiTargetKind.Width);
        Cc(19, MidiTargetKind.Height);
        Cc(20, MidiTargetKind.OffsetX);
        Cc(21, MidiTargetKind.OffsetY);
        Cc(22, MidiTargetKind.Red);
        Cc(23, MidiTargetKind.Green);
        Cc(24, MidiTargetKind.Blue);
        Cc(120, MidiTargetKind.Blackout);
        Cc(123, MidiTargetKind.Blackout);
        map.Bindings.Add(new MidiBinding
        {
            Source = MidiSourceKind.PitchBend,
            Target = MidiTargetKind.OffsetX
        });
        return map;
    }

    public int? SlotForNote(int note, int channel)
    {
        foreach (var b in Bindings)
        {
            if (b.Source != MidiSourceKind.Note || b.Target != MidiTargetKind.PatternSlot) continue;
            if (!ChannelMatches(b.Channel, channel)) continue;
            if (b.Number == note) return b.SlotIndex;
        }

        var slot = note - FirstNote;
        return slot >= 0 ? slot : null;
    }

    public IEnumerable<MidiBinding> MatchesCc(int number, int channel)
    {
        foreach (var b in Bindings)
        {
            if (b.Source != MidiSourceKind.ControlChange) continue;
            if (b.Number != number) continue;
            if (!ChannelMatches(b.Channel, channel)) continue;
            yield return b;
        }
    }

    public static bool ChannelMatches(int bindingChannel, int incomingChannel) =>
        bindingChannel == 0 || bindingChannel == incomingChannel;
}
