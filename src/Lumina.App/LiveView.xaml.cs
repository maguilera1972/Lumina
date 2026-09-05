using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Lumina.Core;
using Lumina.Dac;
using Microsoft.Win32;

namespace Lumina.App;

public partial class LiveView : UserControl
{
    private readonly LiveEngine _engine = new();
    private readonly PatternLibrary _bank = new();
    private readonly MidiInputService _midi = new();
    private LiveRemoteServer? _remoteServer;
    private LiveOscServer? _oscServer;
    private string? _remoteUrl;
    private readonly DispatcherTimer _timer;
    private ColorConversionProfile? _colors;
    private string? _ildaInitialDirectory;
    private int _selectedSlot;
    private bool _learnNote;
    private MidiTargetKind _learnTarget = MidiTargetKind.Intensity;
    private bool _learnCc;
    private bool _projectionActive;

    /// <summary>Raised when the Live bank changes (import / remove / clear).</summary>
    public event Action? BankChanged;

    public int PadCount => _bank.Items.Count;

    public LiveView()
    {
        InitializeComponent();
        _engine.Attach(_bank);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) =>
        {
            var frame = RenderColoredFrame();
            if (frame is null) LivePreview.Clear();
            else LivePreview.ShowFrame(frame);
        };
        _timer.Start();
        _midi.MessageReceived += OnMidi;
        LearnCcBox.ItemsSource = new[]
        {
            MidiTargetKind.Intensity, MidiTargetKind.Hue, MidiTargetKind.Rotation,
            MidiTargetKind.Width, MidiTargetKind.Height, MidiTargetKind.OffsetX, MidiTargetKind.OffsetY,
            MidiTargetKind.Red, MidiTargetKind.Green, MidiTargetKind.Blue, MidiTargetKind.Blackout
        };
        LearnCcBox.SelectedIndex = 0;
        PreviewKeyDown += LiveView_PreviewKeyDown;
        RefreshDevices();
        Loaded += (_, _) =>
        {
            RefreshNetworkInfo();
            RefreshOscHint();
            RebuildPads();
        };
        Unloaded += (_, _) =>
        {
            _midi.Dispose();
            _remoteServer?.Dispose();
            _remoteServer = null;
            _oscServer?.Dispose();
            _oscServer = null;
        };
    }

    /// <summary>Whether Live is the active projection mode (tab Live selected).</summary>
    public void SetProjectionActive(bool active)
    {
        _projectionActive = active;
        if (!active)
        {
            Panic();
        }
    }

    public bool IsProjectionActive => _projectionActive;

    /// <summary>Optional projector color map (hardware), independent of Atelier show content.</summary>
    public void SetColors(ColorConversionProfile? colors)
    {
        _colors = colors;
        RebuildPads();
    }

    public void SetIldaInitialDirectory(string? folder) => _ildaInitialDirectory = folder;

    public void Panic()
    {
        _engine.Panic();
        LivePreview.Clear();
    }

    /// <summary>Current Live output frame (colored), or null if nothing triggered.</summary>
    public LaserFrame? RenderColoredFrame()
    {
        var raw = _engine.Render();
        return raw is null ? null : LaserColorConverter.Apply(raw, _colors);
    }

    /// <summary>Push Live frame to DAC when Live projection is armed; blank otherwise.</summary>
    public void WriteToDac(ILaserDac dac, bool laserArmed)
    {
        if (!laserArmed)
        {
            dac.Blank();
            return;
        }

        var frame = _engine.Render();
        if (frame is null)
        {
            dac.Blank();
            return;
        }

        dac.WriteFrame(DacConvert.From(frame, _colors ?? ColorConversionProfile.CreateRgb()), 30000);
    }

    private LiveRemoteConfig BuildRemoteConfig()
    {
        var items = _bank.Items;
        var pads = items.Select(p => new LiveRemotePadInfo(p.Name, p.Frames.Count)).ToList();
        return new LiveRemoteConfig(pads.Count, pads);
    }

    private string? BuildPreviewSvg(int slot)
    {
        if (slot < 0 || slot >= _bank.Items.Count) return null;
        var pattern = _bank.Items[slot];
        var frame = pattern.FrameAtProgress(pattern.Frames.Count > 1 ? 0.18 : 0)
                    ?? pattern.Frames.FirstOrDefault();
        var colored = LaserColorConverter.Apply(IldaSvgPreview.SampleForPreview(frame), _colors);
        return IldaSvgPreview.ToSvg(colored, size: 160, strokeWidth: 1.15);
    }

    private void ImportIlda_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Charger des ILDA dans la banque Live",
            Filter = "ILDA / frames|*.ild;*.ILD;*.fra;*.FRA|Tous les fichiers|*.*",
            Multiselect = true
        };
        if (!string.IsNullOrWhiteSpace(_ildaInitialDirectory) && Directory.Exists(_ildaInitialDirectory))
            dlg.InitialDirectory = _ildaInitialDirectory;

        if (dlg.ShowDialog() != true) return;

        var added = 0;
        var errors = new List<string>();
        foreach (var path in dlg.FileNames)
        {
            try
            {
                _bank.ImportIlda(path);
                added++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(path)} : {ex.Message}");
            }
        }

        if (_selectedSlot >= _bank.Items.Count)
            _selectedSlot = Math.Max(0, _bank.Items.Count - 1);

        RebuildPads();
        BankChanged?.Invoke();

        if (errors.Count > 0)
        {
            MessageBox.Show(
                Window.GetWindow(this) ?? Application.Current.MainWindow,
                $"{added} fichier(s) ajouté(s).\n\nÉchecs :\n" + string.Join("\n", errors.Take(8)),
                "Banque Live",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RemovePad_Click(object sender, RoutedEventArgs e)
    {
        if (_bank.Items.Count == 0) return;
        if (_selectedSlot < 0 || _selectedSlot >= _bank.Items.Count) return;

        _engine.Panic();
        _bank.Items.RemoveAt(_selectedSlot);
        if (_selectedSlot >= _bank.Items.Count)
            _selectedSlot = Math.Max(0, _bank.Items.Count - 1);
        RebuildPads();
        BankChanged?.Invoke();
    }

    private void ClearBank_Click(object sender, RoutedEventArgs e)
    {
        if (_bank.Items.Count == 0) return;
        var confirm = MessageBox.Show(
            Window.GetWindow(this) ?? Application.Current.MainWindow,
            "Vider toute la banque Live ? (n’affecte pas le show Atelier)",
            "Banque Live",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        _engine.Panic();
        _bank.Items.Clear();
        _selectedSlot = 0;
        RebuildPads();
        BankChanged?.Invoke();
    }

    private void StartRemote_Click(object sender, RoutedEventArgs e)
    {
        if (_remoteServer is not null) return;

        var portText = RemotePortBox.Text?.Trim();
        if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535)
        {
            RemoteStatus.Text = "Port invalide.";
            return;
        }

        try
        {
            _remoteServer = new LiveRemoteServer(
                getConfig: BuildRemoteConfig,
                getPreviewSvg: BuildPreviewSvg,
                onPadPress: (slot, velocity) =>
                    Dispatcher.BeginInvoke(() => _engine.TriggerSlot(slot, velocity)),
                onPadRelease: slot =>
                    Dispatcher.BeginInvoke(() => _engine.ReleaseSlot(slot)),
                onPanic: () => Dispatcher.BeginInvoke(Panic),
                onNoteOn: (note, velocity, channel) =>
                    Dispatcher.BeginInvoke(() => _engine.NoteOn(note, velocity, channel)),
                onCc: (cc, value, channel) =>
                    Dispatcher.BeginInvoke(() => _engine.ControlChange(cc, value, channel))
            );

            _remoteServer.Start(port);
            RefreshNetworkInfo(port);
            RemoteStatus.Text = $"Serveur web : actif. Ouvrez sur l'iPad : {_remoteUrl}";
        }
        catch (Exception ex)
        {
            _remoteServer?.Dispose();
            _remoteServer = null;
            _remoteUrl = null;
            RefreshNetworkInfo();
            RemoteStatus.Text = "Démarrage serveur impossible : " + ex.Message;
        }
    }

    private void StopRemote_Click(object sender, RoutedEventArgs e)
    {
        _remoteServer?.Dispose();
        _remoteServer = null;
        _remoteUrl = null;
        RefreshNetworkInfo();
        RemoteStatus.Text = "Serveur web : arrêté.";
    }

    private void RefreshIp_Click(object sender, RoutedEventArgs e)
    {
        RefreshNetworkInfo();
        RefreshOscHint();
    }

    private void StartOsc_Click(object sender, RoutedEventArgs e)
    {
        if (_oscServer is not null) return;

        var portText = OscPortBox.Text?.Trim();
        if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535)
        {
            OscStatus.Text = "Port OSC invalide.";
            return;
        }

        try
        {
            _oscServer = new LiveOscServer(
                onPadPress: (slot, velocity) =>
                    Dispatcher.BeginInvoke(() =>
                    {
                        _selectedSlot = slot;
                        _engine.TriggerSlot(slot, velocity);
                        RebuildPads();
                    }),
                onPadRelease: slot =>
                    Dispatcher.BeginInvoke(() => _engine.ReleaseSlot(slot)),
                onPanic: () => Dispatcher.BeginInvoke(Panic),
                onNoteOn: (note, velocity, channel) =>
                    Dispatcher.BeginInvoke(() => _engine.NoteOn(note, velocity, channel)),
                onCc: (cc, value, channel) =>
                    Dispatcher.BeginInvoke(() => _engine.ControlChange(cc, value, channel))
            );
            _oscServer.Start(port);
            var ip = LiveRemoteServer.PreferredLanIp();
            OscStatus.Text = $"OSC : actif — udp://{ip}:{port}";
            RefreshOscHint();
        }
        catch (Exception ex)
        {
            _oscServer?.Dispose();
            _oscServer = null;
            OscStatus.Text = "Démarrage OSC impossible : " + ex.Message;
            RefreshOscHint();
        }
    }

    private void StopOsc_Click(object sender, RoutedEventArgs e)
    {
        _oscServer?.Dispose();
        _oscServer = null;
        OscStatus.Text = "OSC : arrêté.";
        RefreshOscHint();
    }

    private void RefreshOscHint()
    {
        var ip = LiveRemoteServer.PreferredLanIp();
        var portText = OscPortBox.Text?.Trim();
        if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535)
            port = 9000;
        OscHint.Text = _oscServer is not null
            ? $"Cible TouchOSC : {ip}  port {port}  ·  ex. /lumina/pad/1"
            : $"Après démarrage : {ip} port {port} (UDP)";
    }

    private void CopyRemoteUrl_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_remoteUrl))
        {
            var portText = RemotePortBox.Text?.Trim();
            if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535)
            {
                RemoteStatus.Text = "Démarrez d’abord le serveur web.";
                return;
            }
            var candidate = $"http://{LiveRemoteServer.PreferredLanIp()}:{port}/";
            try
            {
                Clipboard.SetText(candidate);
                RemoteStatus.Text = "URL proposée copiée (serveur pas encore démarré) : " + candidate;
            }
            catch
            {
                RemoteStatus.Text = "Impossible de copier l’URL (presse-papiers).";
            }
            return;
        }

        try
        {
            Clipboard.SetText(_remoteUrl);
            RemoteStatus.Text = "URL iPad copiée dans le presse-papiers.";
        }
        catch
        {
            RemoteStatus.Text = "Impossible de copier l’URL (presse-papiers).";
        }
    }

    private void RefreshNetworkInfo(int? activePort = null)
    {
        var ips = LiveRemoteServer.GetLanIpv4Addresses();
        var preferred = LiveRemoteServer.PreferredLanIp();
        LocalIpText.Text = ips.Count == 0
            ? "Aucune IP LAN détectée (vérifiez Wi‑Fi / Ethernet)"
            : string.Join("  ·  ", ips);

        var portText = RemotePortBox.Text?.Trim();
        if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535)
            port = activePort ?? 8787;
        if (activePort is int p) port = p;

        var url = $"http://{preferred}:{port}/";
        if (_remoteServer is not null)
        {
            _remoteUrl = url;
            RemoteUrlHint.Text = "URL iPad : " + url;
        }
        else
        {
            RemoteUrlHint.Text = "URL iPad (après démarrage) : " + url;
        }
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

    private void LiveView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Panic();
            e.Handled = true;
            return;
        }

        var slot = KeyToSlot(e.Key);
        if (slot is null) return;

        _selectedSlot = slot.Value;
        _engine.TriggerSlot(slot.Value);
        RebuildPads();
        e.Handled = true;
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        var slot = KeyToSlot(e.Key);
        if (slot is not null)
            _engine.ReleaseSlot(slot.Value);
    }

    private static int? KeyToSlot(Key key)
    {
        if (key >= Key.D1 && key <= Key.D8) return key - Key.D1;
        if (key >= Key.NumPad1 && key <= Key.NumPad8) return key - Key.NumPad1;
        if (key >= Key.A && key <= Key.L) return 8 + (key - Key.A);
        return null;
    }

    private void TriggerPad(int index)
    {
        _selectedSlot = index;
        _engine.TriggerSlot(index);
        RebuildPads();
    }

    private void RebuildPads()
    {
        PadGrid.Children.Clear();
        var items = _bank.Items;
        BankStatus.Text = $"{items.Count} fichier(s) dans la banque Live (indépendante de l’Atelier).";
        LibraryHint.Text = items.Count == 0
            ? "Aucun pad Live. Utilisez « Charger ILDA… » — 1 fichier = 1 touche. Le show Atelier n’est pas utilisé."
            : $"{items.Count} pad(s) Live — MIDI note {_engine.Map.FirstNote}+, clavier 1–8 / A–L, iPad, ou toucher les tuiles.";

        if (items.Count == 0)
        {
            PadGrid.Children.Add(new TextBlock
            {
                Text = "Banque Live vide — chargez des .ild / .fra ici (séparé du show Atelier).",
                Foreground = TryFindResource("BoothMuted") as System.Windows.Media.Brush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4)
            });
            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            var index = i;
            var pattern = items[i];
            PadGrid.Children.Add(PatternPadTiles.Build(
                pattern, index, index == _selectedSlot, _colors, this,
                onPress: _ => TriggerPad(index),
                onRelease: _ => _engine.ReleaseSlot(index),
                onSelect: slot =>
                {
                    _selectedSlot = slot;
                    RebuildPads();
                }));
        }
    }
}
