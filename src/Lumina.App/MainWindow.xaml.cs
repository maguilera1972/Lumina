using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Lumina.Core;
using Lumina.Dac;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace Lumina.App;

public partial class MainWindow : Window
{
    private readonly PreviewDac _dac = new();
    private readonly ShowClock _clock = new();
    private readonly ShowAudioPlayer _audio;
    private readonly DispatcherTimer _timer;
    private LaserShow? _show;
    private BeatGrid? _beatGrid;
    private bool _emergency;
    private bool _laserArmed;
    private LaserFrame? _holdOutput;

    public MainWindow()
    {
        InitializeComponent();
        _dac.Connect();
        _audio = new ShowAudioPlayer(Dispatcher);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Tick();
        _audio.Opened += () =>
        {
            RefreshClockBinding();
            _ = LoadWaveformAsync();
        };
        _audio.Ended += () => StopPlayback();
        _audio.Failed += msg =>
        {
            StatusText.Text = "Lecture audio impossible : " + msg;
        };
        Timeline.SeekRequested += SeekTo;
        Timeline.TimelineChanged += OnTimelineChanged;
        Timeline.ClipSelected += clip => ClipEditor.Bind(clip);
        Waveform.SeekRequested += SeekTo;
        PaletteView.ProfileChanged += OnPaletteChanged;
        PaletteView.HoldOutputChanged += OnHoldOutput;
        PaletteView.Bind(ColorConversionProfile.CreateRgb(), null);
        Library.PlaceRequested += PlacePattern;
        Library.ImportClicked += ImportIldaWithPicker;
        ClipEditor.EffectsChanged += OnClipEffectsChanged;
        ClipEditor.DeleteRequested += DeleteClip;
        ShowEmptyState(true);
        Closed += (_, _) =>
        {
            _timer.Stop();
            _audio.Dispose();
            _dac.Dispose();
        };
        UpdateLaserArmUi();
    }

    private void CloseShow_Click(object sender, RoutedEventArgs e)
    {
        ResetSession();
        StatusText.Text = "Show fermé. Audio, timeline et motifs vidés. Ouvrez un nouveau fichier pour repartir à zéro.";
    }

    private void ResetSession()
    {
        StopPlayback();
        _emergency = false;
        _laserArmed = false;
        _beatGrid = null;
        _audio.Stop();
        _audio.Close();
        _holdOutput = null;
        _show = null;
        IldaView.Clear();
        PaletteView.Bind(ColorConversionProfile.CreateRgb(), null);
        Library.Bind(null);
        ClipEditor.Bind(null);
        Timeline.Bind(null, TimeSpan.FromSeconds(10));
        Waveform.Clear("Chargez un MP3 ou un WAV pour voir la forme d’onde, le rythme et caler le laser.");
        ShowPreview.Clear();
        ShowEmptyState(true);
        AudioText.Text = "Pas d’audio";
        SyncText.Text = "Prévisualisation écran — projecteur éteint.";
        TimeText.Text = "00:00.0 / 00:00.0";
        FrameText.Text = "Frame 0 / 0";
        UpdateLaserArmUi();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        ResetSession();
        _show = ShowLoader.CreateUntitled();
        PaletteView.Bind(_show.Colors, null);
        Library.Bind(_show, _show.Colors);
        ClipEditor.Bind(null);
        BindLoadedShow("Nouveau show vide — aucune trace de l’ancien projet.");
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Ouvrir un show laser",
            Filter = "Shows laser|*.ild;*.fra;*.xrs;*.xmb;*.mrs;*.shw;*.lumina.json|Projet Lumina (*.lumina.json)|*.lumina.json|ILDA (*.ild)|*.ild|Mamba|*.xrs;*.xmb;*.mrs;*.shw|Tous|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        LoadShow(dlg.FileName);
    }

    private void InspectIlda_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Inspecter un fichier ILDA",
            Filter = "ILDA|*.ild|Tous|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var frames = IldaReader.Read(dlg.FileName);
            IldaView.Load(IOPath.GetFileName(dlg.FileName), frames);
            StatusText.Text = $"ILDA : {frames.Count} frame(s) dans {IOPath.GetFileName(dlg.FileName)}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ILDA", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadShow(string path)
    {
        ResetSession();
        _show = ShowLoader.Open(path);
        IldaView.Load(_show.Title, _show.Frames);
        PaletteView.Bind(_show.Colors, _show.Frames.Count > 0 ? _show.Frames[0] : null);
        Library.Bind(_show, _show.Colors);
        ClipEditor.Bind(_show.Timeline.Clips.FirstOrDefault());
        if (_show.Timeline.Clips.Count > 0) Timeline.SelectClip(_show.Timeline.Clips[0]);
        var warn = _show.Warnings.Count == 0 ? "" : " " + string.Join(" ", _show.Warnings);
        BindLoadedShow($"Chargé : {_show.Frames.Count} frame(s), {_show.Library.Items.Count} motif(s), {_show.Timeline.Clips.Count} clip(s).{warn}");
    }

    private void BindLoadedShow(string status)
    {
        if (_show is null) return;
        TitleText.Text = _show.Title;
        KindText.Text = _show.Kind switch
        {
            ShowKind.IldaAnimation => "Animation ILDA — prévisualisation écran",
            ShowKind.MambaShow => "Show Mamba importé — prévisualisation écran",
            _ => "Show Lumina — prévisualisation écran"
        };
        ShowEmptyState(false);
        RefreshAudioUi();
        RefreshClockBinding();
        BindTimeline();
        RenderAt(_clock.Position);
        UpdateLaserArmUi();
        StatusText.Text = status;
    }

    private void ShowEmptyState(bool empty)
    {
        EmptyOverlay.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        ShowPreview.SetHintVisible(!empty);
        if (empty)
        {
            TitleText.Text = "Aucun show";
            KindText.Text = "USB-Box facultative";
        }
    }

    private void ImportPattern_Click(object sender, RoutedEventArgs e) => ImportIldaWithPicker();

    private void ImportIldaWithPicker()
    {
        if (_show is null) New_Click(this, new RoutedEventArgs());
        if (_show is null) return;

        var dlg = new OpenFileDialog
        {
            Title = "Importer un contenu ILDA",
            Filter = "Motifs|*.ild;*.fra|ILDA (*.ild)|*.ild|FRA|*.fra|Tous|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != true) return;

        foreach (var path in dlg.FileNames)
        {
            ImportIldaFile(path);
        }
    }

    private void ImportIldaFile(string path)
    {
        if (_show is null) return;
        try
        {
            IReadOnlyList<LaserFrame> frames = path.EndsWith(".fra", StringComparison.OrdinalIgnoreCase)
                ? FraReader.Read(path)
                : IldaReader.Read(path);
            if (frames.Count == 0)
            {
                MessageBox.Show(this, "Aucune frame dans " + IOPath.GetFileName(path), "ILDA", MessageBoxButton.OK);
                return;
            }

            var picker = new IldaPickerWindow(path, frames) { Owner = this };
            if (picker.ShowDialog() != true || picker.Result is null) return;
            ApplyIldaPick(path, frames, picker.Result);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Import ILDA", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyIldaPick(string path, IReadOnlyList<LaserFrame> frames, IldaPickResult pick)
    {
        if (_show is null) return;
        var baseName = IOPath.GetFileNameWithoutExtension(path);
        var added = new List<PatternItem>();
        if (pick.AsAnimation)
        {
            added.Add(_show.Library.Add(baseName, frames, path));
        }
        else
        {
            foreach (var index in pick.Indices)
            {
                if (index < 0 || index >= frames.Count) continue;
                var frame = frames[index];
                var name = string.IsNullOrWhiteSpace(frame.Name) ? $"{baseName} {index + 1}" : frame.Name;
                added.Add(_show.Library.Add(name, [frame], path));
            }
        }

        if (added.Count == 0) return;
        if (_show.Frames.Count == 0)
        {
            _show.Frames = pick.AsAnimation
                ? frames.ToList()
                : pick.Indices.Where(i => i >= 0 && i < frames.Count).Select(i => frames[i]).ToList();
        }

        PlacePatterns(added);
        Library.Bind(_show, _show.Colors);
        Library.Select(added[^1]);
        IldaView.Load(_show.Title, _show.Frames);
        AutoSave();
        StatusText.Text = pick.AsAnimation
            ? $"Animation « {baseName} » ({frames.Count} frames) ajoutée à la timeline."
            : $"{added.Count} forme(s) de « {baseName} » ajoutée(s) à la timeline.";
    }

    private void PlacePatterns(IReadOnlyList<PatternItem> patterns)
    {
        if (_show is null || patterns.Count == 0) return;
        var t = _beatGrid?.Snap(_clock.Position) ?? _clock.Position;
        TimelineClip? last = null;
        foreach (var pattern in patterns)
        {
            last = _show.Timeline.PlaceClip(pattern, t);
            t = last.End;
        }

        if (last is not null)
        {
            Timeline.SelectClip(last);
            ClipEditor.Bind(last);
        }

        BindTimeline();
        RenderAt(_clock.Position);
    }

    private void LinkAudio_Click(object sender, RoutedEventArgs e)
    {
        if (_show is null)
        {
            MessageBox.Show(this, "Ouvrez ou créez d’abord un show, puis associez un MP3 ou un WAV.", "Lumina", MessageBoxButton.OK);
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title = "Audio du show (MP3 / WAV)",
            Filter = AudioCue.OpenDialogFilter
        };
        if (dlg.ShowDialog() != true) return;

        if (_show.SourcePath is not null)
        {
            var cue = AudioLocator.Link(_show.SourcePath, dlg.FileName, TimeSpan.Zero);
            _show.Audio.LinkedPath = cue.LinkedPath;
            _show.Audio.ResolvedPath = cue.ResolvedPath;
            _show.Audio.Status = cue.Status;
            _show.Audio.Volume = cue.Volume;
        }
        else
        {
            _show.Audio.LinkedPath = dlg.FileName;
            _show.Audio.ResolvedPath = dlg.FileName;
            _show.Audio.Status = AudioStatus.Ready;
        }

        LoadAudioFile(_show.Audio.ResolvedPath);
        RefreshAudioUi();
        RefreshClockBinding();
        BindTimeline();
        var kind = AudioCue.FormatLabel(_show.Audio.ResolvedPath);
        StatusText.Text = $"{kind} associé. Lecture = prévisualisation synchro (forme d’onde + laser). Projecteur éteint tant que LASER OFF.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_show is null)
        {
            MessageBox.Show(this, "Aucun show à enregistrer.", "Lumina", MessageBoxButton.OK);
            return;
        }

        if (_show.SourcePath is null)
        {
            SaveAs_Click(sender, e);
            return;
        }

        LuminaSidecar.SaveShow(_show);
        StatusText.Text = "Show enregistré dans " + IOPath.GetFileName(LuminaSidecar.PathFor(_show.SourcePath));
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_show is null)
        {
            MessageBox.Show(this, "Aucun show à enregistrer.", "Lumina", MessageBoxButton.OK);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Enregistrer le projet Lumina",
            Filter = "Projet Lumina (*.lumina.json)|*.lumina.json",
            FileName = string.IsNullOrWhiteSpace(_show.Title) ? "show.lumina.json" : _show.Title + ".lumina.json"
        };
        if (dlg.ShowDialog() != true) return;
        _show.SourcePath = dlg.FileName;
        _show.Title = IOPath.GetFileName(dlg.FileName);
        if (_show.Title.EndsWith(LuminaSidecar.Extension, StringComparison.OrdinalIgnoreCase))
        {
            _show.Title = _show.Title[..^LuminaSidecar.Extension.Length];
        }

        TitleText.Text = _show.Title;
        LuminaSidecar.SaveShow(_show);
        StatusText.Text = "Projet enregistré : " + IOPath.GetFileName(dlg.FileName);
    }

    private void ConvertIlda_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Convertir un show Mamba / MRS en ILDA",
            Filter = "Shows|*.mrs;*.xrs;*.xmb;*.shw;*.fra;*.ild|Tous|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        var save = new SaveFileDialog
        {
            Title = "Enregistrer l’ILDA",
            Filter = "ILDA (*.ild)|*.ild",
            FileName = IOPath.GetFileNameWithoutExtension(dlg.FileName) + ".ild"
        };
        if (save.ShowDialog() != true) return;

        try
        {
            var result = ShowIldaExporter.Export(dlg.FileName, save.FileName);
            var extra = result.Warnings.Count == 0 ? "" : " " + string.Join(" ", result.Warnings);
            StatusText.Text = $"Export ILDA : {result.FrameCount} frame(s) → {IOPath.GetFileName(result.DestinationPath)}.{extra}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export ILDA", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnHoldOutput(LaserFrame? frame)
    {
        _holdOutput = frame;
        if (_laserArmed && frame is not null) _timer.Start();
        RenderAt(_clock.Position);
        StatusText.Text = frame is null
            ? "Mire arrêtée — retour à la timeline."
            : frame.Name == "MIRE"
                ? "Mire d’alignement affichée. Allumez le projecteur (LASER ON) pour caler largeur, hauteur et position sur le mur."
                : $"Motif test « {frame.Name} » — visible à l’écran, et sur le projecteur s’il est armé.";
    }

    private void OnPaletteChanged()
    {
        if (_show is not null)
        {
            _show.Colors = PaletteView.Profile;
            AutoSave();
        }

        RenderAt(_clock.Position);
        Library.SetColors(_show?.Colors);
        UpdateLaserArmUi();
    }

    private void PlacePattern(PatternItem pattern)
    {
        if (_show is null) return;
        var start = _beatGrid?.Snap(_clock.Position) ?? _clock.Position;
        var clip = _show.Timeline.PlaceClip(pattern, start);
        Timeline.SelectClip(clip);
        ClipEditor.Bind(clip);
        BindTimeline();
        AutoSave();
        RenderAt(start);
        StatusText.Text = $"Clip « {pattern.Name} » placé à {FormatTime(start)}.";
    }

    private void OnClipEffectsChanged()
    {
        AutoSave();
        RenderAt(_clock.Position);
    }

    private void DeleteClip(TimelineClip clip)
    {
        _show?.Timeline.Remove(clip);
        ClipEditor.Bind(_show?.Timeline.Clips.LastOrDefault());
        BindTimeline();
        AutoSave();
        RenderAt(_clock.Position);
    }

    private void AutoSave()
    {
        if (_show?.SourcePath is null) return;
        try { LuminaSidecar.SaveShow(_show); }
        catch { /* sidecar optional while untitled */ }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        var path = files[0];
        var ext = IOPath.GetExtension(path);
        if (LuminaSidecar.IsProjectFile(path) ||
            ext.Equals(".xrs", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".xmb", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".mrs", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".shw", StringComparison.OrdinalIgnoreCase) ||
            (_show is null && (ext.Equals(".ild", StringComparison.OrdinalIgnoreCase) || ext.Equals(".fra", StringComparison.OrdinalIgnoreCase))))
        {
            LoadShow(path);
            return;
        }

        if (_show is null) New_Click(sender, e);
        if (_show is null) return;
        if (ext.Equals(".ild", StringComparison.OrdinalIgnoreCase) || ext.Equals(".fra", StringComparison.OrdinalIgnoreCase))
        {
            ImportIldaFile(path);
        }
        else if (AudioCue.IsSupportedFile(path))
        {
            _show.Audio.LinkedPath = path;
            _show.Audio.ResolvedPath = path;
            _show.Audio.Status = AudioStatus.Ready;
            LoadAudioFile(path);
            RefreshAudioUi();
            RefreshClockBinding();
        }
    }

    private void RefreshAudioUi()
    {
        if (_show is null)
        {
            AudioText.Text = "Pas de show.";
            return;
        }

        AudioText.Text = _show.Audio.Status switch
        {
            AudioStatus.Ready => AudioCue.FormatLabel(_show.Audio.ResolvedPath) + " : " + IOPath.GetFileName(_show.Audio.ResolvedPath),
            AudioStatus.MissingFile => "Audio lié mais absent : " + _show.Audio.LinkedPath,
            _ => "Pas d’audio (MP3 / WAV) — la prévisualisation laser tourne sur horloge interne."
        };

        if (_show.Audio.IsPlayable)
        {
            LoadAudioFile(_show.Audio.ResolvedPath);
        }
        else
        {
            _audio.Close();
            Waveform.Clear("Chargez un MP3 ou un WAV pour voir la forme d’onde et caler le laser.");
        }

        UpdateSyncText();
    }

    private void LoadAudioFile(string? path)
    {
        _audio.Stop();
        _audio.Close();
        if (path is null || !File.Exists(path)) return;
        _audio.Open(IOPath.GetFullPath(path));
        _audio.Volume = Math.Clamp(_show?.Audio.Volume ?? 1.0, 0, 1);
    }

    private async Task LoadWaveformAsync()
    {
        var path = _show?.Audio.ResolvedPath;
        if (path is null || !File.Exists(path) || !_audio.HasDuration) return;
        var duration = _audio.Duration;
        float[] peaks;
        BeatGrid? grid = null;
        try
        {
            peaks = await Task.Run(() => WaveformSampler.Sample(path, 1200));
        }
        catch
        {
            peaks = [];
        }

        try
        {
            grid = await Task.Run(() => BeatAnalyzer.Analyze(path));
        }
        catch
        {
            grid = null;
        }

        if (_show is null || _show.Audio.ResolvedPath != path) return;

        _beatGrid = grid;
        Waveform.SetAudio(peaks, duration, grid?.Beats());
        if (_show is not null && grid is not null)
        {
            var msg = RhythmSync.Align(_show, grid, duration);
            BindTimeline();
            ClipEditor.Bind(Timeline.SelectedClip);
            AutoSave();
            StatusText.Text = AudioCue.FormatLabel(path) + " lié — " + msg;
        }

        BindTimeline();
        UpdateSyncText();
    }

    private void RefreshClockBinding()
    {
        if (_show is null) return;
        TimeSpan? audioDuration = null;
        Func<TimeSpan>? pos = null;
        if (_show.Audio.IsPlayable && _audio.HasDuration)
        {
            audioDuration = _audio.Duration;
            pos = () => _audio.Position;
        }

        _clock.Bind(_show, audioDuration, pos);
        BindTimeline();
        TimeText.Text = FormatTime(TimeSpan.Zero) + " / " + FormatTime(_clock.TimelineDuration);
        UpdateSyncText();
    }

    private void BindTimeline()
    {
        Timeline.Bind(_show, _clock.TimelineDuration);
    }

    private void UpdateSyncText()
    {
        if (_show is null)
        {
            SyncText.Text = "Ouvrez un show pour prévisualiser.";
            return;
        }

        if (_show.Audio.IsPlayable && _audio.HasDuration)
        {
            var audio = _audio.Duration;
            var laser = _show.TimelineDuration;
            var delta = Math.Abs((audio - laser).TotalSeconds);
            var bpm = _beatGrid is null ? "" : $" Rythme {_beatGrid.Bpm:0.0} BPM.";
            SyncText.Text = delta > 0.5
                ? $"Audio {FormatTime(audio)} / laser {FormatTime(laser)}.{bpm} Recalez ou cliquez « Caler sur le rythme »."
                : "Synchro : l’audio pilote le laser." + bpm;
        }
        else
        {
            SyncText.Text = "Sans audio, la prévisualisation utilise l’horloge interne. Associez un MP3 ou un WAV pour le synchro réel.";
        }
    }

    private void AlignRhythm_Click(object sender, RoutedEventArgs e)
    {
        if (_show is null)
        {
            MessageBox.Show(this, "Ouvrez d’abord un show.", "Lumina", MessageBoxButton.OK);
            return;
        }

        if (_show.Audio.ResolvedPath is null || !File.Exists(_show.Audio.ResolvedPath))
        {
            MessageBox.Show(this, "Associez un MP3 ou un WAV, puis Lumina détectera le rythme et calera les clips.", "Lumina", MessageBoxButton.OK);
            return;
        }

        try
        {
            _beatGrid = BeatAnalyzer.Analyze(_show.Audio.ResolvedPath);
            var duration = _audio.HasDuration ? _audio.Duration : _beatGrid.Duration;
            var msg = RhythmSync.Align(_show, _beatGrid, duration);
            Waveform.SetAudio(WaveformSampler.Sample(_show.Audio.ResolvedPath, 1200), duration, _beatGrid.Beats());
            BindTimeline();
            ClipEditor.Bind(Timeline.SelectedClip);
            AutoSave();
            UpdateSyncText();
            StatusText.Text = msg;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Analyse du rythme impossible : " + ex.Message, "Lumina", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnTimelineChanged()
    {
        if (_show?.SourcePath is not null)
        {
            try { LuminaSidecar.SaveShow(_show); }
            catch { /* sidecar optional while untitled */ }
        }

        RefreshClockBinding();
        RenderAt(_clock.Position);
    }

    private void Play_Click(object sender, RoutedEventArgs e) => StartPlayback();
    private void Pause_Click(object sender, RoutedEventArgs e) => PausePlayback();
    private void Stop_Click(object sender, RoutedEventArgs e) => StopPlayback();
    private void Emergency_Click(object sender, RoutedEventArgs e) => EmergencyStop();

    private void StartPlayback()
    {
        if (_show is null) return;
        _emergency = false;
        RefreshClockBinding();
        if (_show.Audio.IsPlayable) _audio.Play();
        _clock.Start();
        _timer.Start();
        StatusText.Text = _laserArmed
            ? "Lecture vers le projecteur."
            : "Prévisualisation en cours — projecteur éteint. Vérifiez le calage audio / laser.";
    }

    private void PausePlayback()
    {
        _clock.Pause();
        _timer.Stop();
        if (_show?.Audio.IsPlayable == true) _audio.Pause();
    }

    private void StopPlayback()
    {
        _clock.Stop();
        _timer.Stop();
        _audio.Stop();
        if (_audio.IsOpen) _audio.Position = TimeSpan.Zero;
        _dac.Blank();
        RenderAt(TimeSpan.Zero);
        if (_laserArmed && _holdOutput is not null) _timer.Start();
    }

    private void EmergencyStop()
    {
        _emergency = true;
        _laserArmed = false;
        UpdateLaserArmUi();
        StopPlayback();
        StatusText.Text = "Arrêt d’urgence : laser éteint, audio coupé.";
    }

    private void LaserArm_Click(object sender, RoutedEventArgs e)
    {
        if (!_laserArmed)
        {
            var confirm = MessageBox.Show(
                this,
                "Activer la sortie projecteur ? La prévisualisation à l’écran reste disponible. Un arrêt d’urgence (Échap) recoupe le laser.",
                "Lumina",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
        }

        _laserArmed = !_laserArmed;
        if (!_laserArmed)
        {
            _dac.Blank();
        }
        else
        {
            if (_holdOutput is not null || _show is not null)
            {
                _timer.Start();
            }

            RenderAt(_clock.Position);
        }

        UpdateLaserArmUi();
    }

    private void UpdateLaserArmUi()
    {
        LaserArmButton.Content = _laserArmed ? "LASER ON" : "LASER OFF — prévisualisation";
        var map = (_show?.Colors ?? PaletteView.Profile).AnalogMapSummary();
        DacText.Text = _laserArmed
            ? "Sortie projecteur : armée (USB-Box) · " + map
            : "Sortie projecteur : coupée · " + map;
    }

    private void SeekTo(TimeSpan time)
    {
        if (_show is null) return;
        if (_show.Audio.IsPlayable)
        {
            _audio.Position = time;
        }

        _clock.Seek(time);
        RenderAt(time);
    }

    private void Tick()
    {
        if (_emergency) return;
        if (_show is null)
        {
            if (_holdOutput is not null) RenderAt(_clock.Position);
            return;
        }

        if (_clock.Finished)
        {
            StopPlayback();
            return;
        }

        RenderAt(_clock.Position);
    }

    private void RenderAt(TimeSpan time)
    {
        var colors = _show?.Colors ?? PaletteView.Profile;
        var frame = _holdOutput ?? _show?.FrameAt(time);
        var overlayEmpty = _show is null && _holdOutput is null;
        EmptyOverlay.Visibility = overlayEmpty ? Visibility.Visible : Visibility.Collapsed;
        ShowPreview.SetHintVisible(!overlayEmpty);

        if (frame is null)
        {
            ShowPreview.Clear();
            if (_laserArmed) _dac.Blank();
            return;
        }

        var visual = LaserColorConverter.Apply(frame, colors);
        ShowPreview.ShowFrame(visual);
        Waveform.SetPosition(time);
        Timeline.SetPosition(time);
        TimeText.Text = FormatTime(time) + " / " + FormatTime(_clock.TimelineDuration);
        if (_show is not null)
        {
            var idx = _show.IndexOf(frame);
            FrameText.Text = _holdOutput is null
                ? $"Frame {(idx < 0 ? 0 : idx + 1)} / {_show.Frames.Count}"
                : _holdOutput.Name;
        }
        else
        {
            FrameText.Text = frame.Name;
        }

        if (_laserArmed)
        {
            _dac.WriteFrame(DacConvert.From(frame, colors), 30000);
        }
        else
        {
            _dac.Blank();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            EmergencyStop();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            New_Click(sender, e);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            Open_Click(sender, e);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W)
        {
            CloseShow_Click(sender, e);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            Save_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Space && e.OriginalSource is not System.Windows.Controls.TextBox)
        {
            if (_clock.IsRunning) PausePlayback();
            else StartPlayback();
            e.Handled = true;
        }
    }

    private void Quit_Click(object sender, RoutedEventArgs e) => Close();

    private static string FormatTime(TimeSpan t) =>
        $"{(int)t.TotalMinutes:00}:{t.Seconds:00}.{t.Milliseconds / 100:0}";
}
