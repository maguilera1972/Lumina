using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Lumina.Core;

namespace Lumina.App;

public partial class LiveView : UserControl
{
    private readonly LiveEngine _engine = new();
    private readonly MidiInputService _midi = new();
    private readonly DispatcherTimer _timer;
    private LaserShow? _show;
    private int _selectedSlot;
    private bool _learnNote;
    private MidiTargetKind _learnTarget = MidiTargetKind.Intensity;
    private bool _learnCc;

    public LiveView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => LivePreview.ShowFrame(_engine.Render());
        _timer.Start();
        _midi.MessageReceived += OnMidi;
        LearnCcBox.ItemsSource = new[]
        {
            MidiTargetKind.Intensity, MidiTargetKind.Hue, MidiTargetKind.Rotation,
            MidiTargetKind.Width, MidiTargetKind.Height, MidiTargetKind.OffsetX, MidiTargetKind.OffsetY,
            MidiTargetKind.Red, MidiTargetKind.Green, MidiTargetKind.Blue, MidiTargetKind.Blackout
        };
        LearnCcBox.SelectedIndex = 0;
        RefreshDevices();
        Unloaded += (_, _) => _midi.Dispose();
    }

    public void Attach(LaserShow? show)
    {
        _show = show;
        _engine.Attach(show?.Library ?? new PatternLibrary());
        RebuildPads();
    }

    public void Panic()
    {
        _engine.Panic();
        LivePreview.Clear();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshDevices();

    private void RefreshDevices()
    {
        DeviceBox.ItemsSource = MidiInputService.ListDevices();
        if (DeviceBox.Items.Count > 0 && DeviceBox.SelectedIndex < 0)
        {
            DeviceBox.SelectedIndex = 0;
        }
    }

    private void OpenMidi_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceBox.SelectedItem is not MidiDeviceInfo info)
        {
            MidiStatus.Text = "Aucun interface MIDI détecté.";
            return;
        }

        try
        {
            _midi.Open(info.Index);
            MidiStatus.Text = "Ouvert : " + info.Name;
        }
        catch (Exception ex)
        {
            MidiStatus.Text = "MIDI : " + ex.Message;
        }
    }

    private void Latch_Changed(object sender, RoutedEventArgs e) =>
        _engine.Latch = LatchBox.IsChecked == true;

    private void LearnNote_Click(object sender, RoutedEventArgs e)
    {
        _learnNote = true;
        MidiStatus.Text = "Learn note : jouez une touche pour le pad " + (_selectedSlot + 1);
    }

    private void LearnCc_Click(object sender, RoutedEventArgs e)
    {
        _learnCc = true;
        if (LearnCcBox.SelectedItem is MidiTargetKind kind) _learnTarget = kind;
        MidiStatus.Text = "Learn CC : bougez un encodeur pour " + _learnTarget;
    }

    private void OnMidi(MidiLiveMessage message)
    {
        Dispatcher.BeginInvoke(() => ApplyMidi(message));
    }

    private void ApplyMidi(MidiLiveMessage message)
    {
        if (_learnNote && message.Kind == MidiLiveKind.NoteOn)
        {
            _engine.LearnNote(message.Number, message.Channel, _selectedSlot);
            _learnNote = false;
            MidiStatus.Text = $"Pad {_selectedSlot + 1} ← note {message.Number} (ch {message.Channel})";
            return;
        }

        if (_learnCc && message.Kind == MidiLiveKind.ControlChange)
        {
            _engine.LearnCc(message.Number, message.Channel, _learnTarget);
            _learnCc = false;
            MidiStatus.Text = $"{_learnTarget} ← CC {message.Number}";
            return;
        }

        switch (message.Kind)
        {
            case MidiLiveKind.NoteOn:
                _engine.NoteOn(message.Number, message.Value, message.Channel);
                break;
            case MidiLiveKind.NoteOff:
                _engine.NoteOff(message.Number, message.Channel);
                break;
            case MidiLiveKind.ControlChange:
                _engine.ControlChange(message.Number, message.Value, message.Channel);
                break;
            case MidiLiveKind.PitchBend:
                _engine.PitchBend(message.Value, message.Channel);
                break;
        }

        MidiStatus.Text = $"{message.Kind} ch{message.Channel} #{message.Number} = {message.Value}";
    }

    private void RebuildPads()
    {
        PadGrid.Children.Clear();
        var items = _show?.Library.Items ?? [];
        LibraryHint.Text = items.Count == 0
            ? "Importez des ILDA ou ouvrez un show : chaque motif devient un pad MIDI (note 36+)."
            : $"{items.Count} motif(s) — notes MIDI { _engine.Map.FirstNote } et suivantes, ou clavier 1–8.";
        for (var i = 0; i < Math.Max(16, items.Count); i++)
        {
            var index = i;
            var name = i < items.Count ? items[i].Name : "—";
            var btn = new Button
            {
                Content = $"{i + 1}\n{name}",
                Margin = new Thickness(3),
                Height = 64,
                Background = i == _selectedSlot ? new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x97)) : new SolidColorBrush(Color.FromRgb(0x22, 0x28, 0x33)),
                Foreground = Brushes.White
            };
            btn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                _selectedSlot = index;
                _engine.TriggerSlot(index);
                RebuildPads();
                e.Handled = true;
            };
            btn.PreviewMouseLeftButtonUp += (_, _) => _engine.ReleaseSlot(index);
            PadGrid.Children.Add(btn);
        }
    }
}
