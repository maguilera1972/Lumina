using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Lumina.Core;
using Microsoft.Win32;

namespace Lumina.App;

public partial class ColorPaletteView : UserControl
{
    private ColorConversionProfile _profile = ColorConversionProfile.CreateRgb();
    private LaserFrame? _sample;
    private int _selected;
    private int _worldIndex;
    private bool _suppress = true;

    public event Action? ProfileChanged;
    public event Action<LaserFrame?>? HoldOutputChanged;

    public ColorConversionProfile Profile => _profile;

    public ColorPaletteView()
    {
        InitializeComponent();
        ProjectorBox.SelectedIndex = 0;
        DriverBox.SelectedIndex = 1;
        ModeBox.SelectedIndex = 0;
        CurveChannelBox.SelectedIndex = 0;
        RebuildGrid();
        UpdateProjectorUi();
        _suppress = false;
    }

    public void Bind(ColorConversionProfile profile, LaserFrame? sampleFrame)
    {
        _profile = profile;
        _sample = sampleFrame;
        _suppress = true;
        ProjectorBox.SelectedIndex = profile.IsRgy ? 1 : 0;
        DriverBox.SelectedIndex = profile.Driver switch
        {
            DacDriverKind.Preview => 0,
            DacDriverKind.MediaLasUsbBox => 1,
            DacDriverKind.Fb3Usb => 2,
            DacDriverKind.Fb4Network => 3,
            DacDriverKind.OpenDacBridge => 4,
            _ => 1
        };
        RoutingBox.SelectedIndex = profile.YellowRouting == IldaYellowRouting.MixOnRedGreen ? 1 : 0;
        EnabledBox.IsChecked = profile.Enabled;
        MasterGain.Value = profile.MasterBrightness;
        RedGain.Value = profile.RedGain;
        GreenGain.Value = profile.GreenGain;
        BlueGain.Value = profile.BlueGain;
        YellowGain.Value = profile.YellowGain;
        BlueFold.Value = profile.BlueFold;
        YellowFromRg.Value = profile.YellowFromRedGreen;
        ProfileNameBox.Text = profile.Name;
        ProjectionWorldBank.EnsureDefaults(profile);
        ProjectionWorldBank.SyncPrimary(profile);
        _worldIndex = 0;
        RebuildWorldList();
        PushWorldEditor();
        RefreshAlignReadouts();
        ModeBox.SelectedIndex = profile.Mode switch
        {
            ColorConversionMode.NearestPrimary => 1,
            ColorConversionMode.AvailableMix => 2,
            ColorConversionMode.PaletteTable => 3,
            _ => 0
        };
        PushCurveSliders();
        _suppress = false;
        UpdateProjectorUi();
        RebuildGrid();
        PushEntry();
        RefreshSample();
    }

    public void SetSample(LaserFrame? frame)
    {
        _sample = frame;
        RefreshSample();
    }

    private void Profile_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        PullProfile();
        UpdateProjectorUi();
        RebuildGrid();
        PushEntry();
        RefreshSample();
        ProfileChanged?.Invoke();
    }

    private void Out_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppress || _selected < 0 || _selected >= _profile.Entries.Count) return;
        var entry = _profile.Entries[_selected];
        entry.OutR = (byte)OutR.Value;
        entry.OutG = (byte)OutG.Value;
        entry.OutB = (byte)OutB.Value;
        RebuildGrid();
        PushEntry();
        RefreshSample();
        ProfileChanged?.Invoke();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _profile.ResetTableToIlda();
        RebuildGrid();
        PushEntry();
        RefreshSample();
        ProfileChanged?.Invoke();
    }

    private void PresetRgb_Click(object sender, RoutedEventArgs e)
    {
        _profile.ApplyProjector(ProjectorColorSet.Rgb);
        _profile.Mode = ColorConversionMode.Direct;
        _profile.Driver = DacDriverKind.MediaLasUsbBox;
        Bind(_profile, _sample);
        ProfileChanged?.Invoke();
    }

    private void PresetRgy_Click(object sender, RoutedEventArgs e)
    {
        _profile.ApplyProjector(ProjectorColorSet.Rgy);
        _profile.Mode = ColorConversionMode.AvailableMix;
        _profile.Driver = DacDriverKind.MediaLasUsbBox;
        _profile.YellowRouting = IldaYellowRouting.IldaBlueAsYellow;
        Bind(_profile, _sample);
        ProfileChanged?.Invoke();
    }

    private void TestRed_Click(object sender, RoutedEventArgs e) => ShowTest(ColorTestPatterns.Red());
    private void TestGreen_Click(object sender, RoutedEventArgs e) => ShowTest(ColorTestPatterns.Green());
    private void TestBlue_Click(object sender, RoutedEventArgs e) => ShowTest(ColorTestPatterns.Blue());
    private void TestYellow_Click(object sender, RoutedEventArgs e) => ShowTest(ColorTestPatterns.Yellow());
    private void TestWhite_Click(object sender, RoutedEventArgs e) => ShowTest(ColorTestPatterns.White());

    private void ShowTest(LaserFrame frame)
    {
        SetSample(frame);
        HoldOutputChanged?.Invoke(frame);
        ProfileChanged?.Invoke();
    }

    private void Mire_Click(object sender, RoutedEventArgs e) => ShowTest(ProjectionMire.Grid());

    private void ClearMire_Click(object sender, RoutedEventArgs e)
    {
        HoldOutputChanged?.Invoke(null);
        RefreshSample();
        ProfileChanged?.Invoke();
    }

    private void ResetAlign_Click(object sender, RoutedEventArgs e)
    {
        ProjectionWorldBank.EnsureDefaults(_profile);
        if (_worldIndex < 0 || _worldIndex >= _profile.Worlds.Count) _worldIndex = 0;
        _profile.Worlds[_worldIndex].Alignment.Reset();
        if (_worldIndex == 0)
            _profile.Alignment.Reset();
        Bind(_profile, _sample);
        ProfileChanged?.Invoke();
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        PullProfile();
        var dlg = new SaveFileDialog
        {
            Title = "Enregistrer le profil projecteur",
            Filter = ProjectorProfileStore.OpenDialogFilter,
            FileName = (_profile.Name.Length == 0 ? "projecteur" : _profile.Name) + ProjectorProfileStore.Extension
        };
        if (dlg.ShowDialog() != true) return;
        ProjectorProfileStore.Save(dlg.FileName, _profile);
    }

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Charger un profil projecteur",
            Filter = ProjectorProfileStore.OpenDialogFilter
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _profile = ProjectorProfileStore.Load(dlg.FileName);
            Bind(_profile, _sample);
            ProfileChanged?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Profil projecteur", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CurveChannel_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        _suppress = true;
        PushCurveSliders();
        _suppress = false;
    }

    private void Curve_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppress) return;
        var curve = SelectedCurve();
        curve.C0 = Curve0.Value;
        curve.C25 = Curve25.Value;
        curve.C50 = Curve50.Value;
        curve.C75 = Curve75.Value;
        curve.C100 = Curve100.Value;
        RebuildGrid();
        RefreshSample();
        ProfileChanged?.Invoke();
    }

    private void ResetCurve_Click(object sender, RoutedEventArgs e)
    {
        SelectedCurve().Reset();
        _suppress = true;
        PushCurveSliders();
        _suppress = false;
        RebuildGrid();
        RefreshSample();
        ProfileChanged?.Invoke();
    }

    private IntensityCurve SelectedCurve() => CurveChannelBox.SelectedIndex switch
    {
        1 => _profile.GreenCurve,
        2 => _profile.BlueCurve,
        3 => _profile.YellowCurve,
        _ => _profile.RedCurve
    };

    private void PushCurveSliders()
    {
        var curve = SelectedCurve();
        Curve0.Value = curve.C0;
        Curve25.Value = curve.C25;
        Curve50.Value = curve.C50;
        Curve75.Value = curve.C75;
        Curve100.Value = curve.C100;
    }

    private void PullProfile()
    {
        if (ProjectorBox is null || DriverBox is null || RoutingBox is null || ModeBox is null) return;
        if (AlignWidth is null || ProfileNameBox is null) return;
        var projector = ProjectorBox.SelectedIndex == 1 ? ProjectorColorSet.Rgy : ProjectorColorSet.Rgb;
        _profile.Driver = DriverBox.SelectedIndex switch
        {
            0 => DacDriverKind.Preview,
            2 => DacDriverKind.Fb3Usb,
            3 => DacDriverKind.Fb4Network,
            4 => DacDriverKind.OpenDacBridge,
            _ => DacDriverKind.MediaLasUsbBox
        };
        _profile.ApplyProjector(projector);
        if (_profile.IsRgy && _profile.Driver == DacDriverKind.MediaLasUsbBox)
        {
            _profile.YellowRouting = IldaYellowRouting.IldaBlueAsYellow;
        }
        else
        {
            _profile.YellowRouting = RoutingBox.SelectedIndex == 1
                ? IldaYellowRouting.MixOnRedGreen
                : IldaYellowRouting.IldaBlueAsYellow;
        }

        _profile.Enabled = EnabledBox.IsChecked == true;
        _profile.MasterBrightness = MasterGain.Value;
        _profile.RedGain = RedGain.Value;
        _profile.GreenGain = GreenGain.Value;
        _profile.BlueGain = BlueGain.Value;
        _profile.YellowGain = YellowGain.Value;
        _profile.BlueFold = BlueFold.Value;
        _profile.YellowFromRedGreen = YellowFromRg.Value;
        _profile.Mode = ModeBox.SelectedIndex switch
        {
            1 => ColorConversionMode.NearestPrimary,
            2 => ColorConversionMode.AvailableMix,
            3 => ColorConversionMode.PaletteTable,
            _ => ColorConversionMode.Direct
        };
        var name = ProfileNameBox.Text.Trim();
        if (name.Length > 0) _profile.Name = name;
        ProjectionWorldBank.EnsureDefaults(_profile);
        if (_worldIndex < 0 || _worldIndex >= _profile.Worlds.Count)
            _worldIndex = 0;
        var world = _profile.Worlds[_worldIndex];
        world.Enabled = WorldEnabledBox.IsChecked == true;
        if (WorldProjectorBox.SelectedIndex >= 0)
            world.ProjectorIndex = WorldProjectorBox.SelectedIndex;
        world.Alignment ??= new ProjectionAlignment();
        world.Alignment.Width = AlignWidth.Value;
        world.Alignment.Height = AlignHeight.Value;
        world.Alignment.OffsetX = AlignX.Value;
        world.Alignment.OffsetY = AlignY.Value;
        world.Alignment.Rotation = AlignRot.Value;
        world.Alignment.FlipX = FlipXBox.IsChecked == true;
        world.Alignment.FlipY = FlipYBox.IsChecked == true;
        if (_worldIndex == 0)
            _profile.Alignment.CopyFrom(world.Alignment);
        else
            ProjectionWorldBank.SyncPrimary(_profile);
        RefreshAlignReadouts();
    }

    private void RebuildWorldList()
    {
        if (WorldList is null) return;
        ProjectionWorldBank.EnsureDefaults(_profile);
        var prev = _worldIndex;
        WorldList.Items.Clear();
        for (var i = 0; i < _profile.Worlds.Count; i++)
        {
            var w = _profile.Worlds[i];
            var mark = w.Enabled ? "" : " (off)";
            WorldList.Items.Add($"{i + 1}. {w.Name} · P{w.ProjectorIndex}{mark}");
        }

        _worldIndex = Math.Clamp(prev, 0, Math.Max(0, _profile.Worlds.Count - 1));
        if (WorldList.Items.Count > 0)
            WorldList.SelectedIndex = _worldIndex;
    }

    private void PushWorldEditor()
    {
        ProjectionWorldBank.EnsureDefaults(_profile);
        if (_worldIndex < 0 || _worldIndex >= _profile.Worlds.Count) _worldIndex = 0;
        var world = _profile.Worlds[_worldIndex];
        WorldNameBox.Text = world.Name;
        WorldEnabledBox.IsChecked = world.Enabled;
        WorldProjectorBox.SelectedIndex = Math.Clamp(world.ProjectorIndex, 0, 7);
        var a = world.Alignment ?? new ProjectionAlignment();
        AlignWidth.Value = a.Width;
        AlignHeight.Value = a.Height;
        AlignX.Value = a.OffsetX;
        AlignY.Value = a.OffsetY;
        AlignRot.Value = a.Rotation;
        FlipXBox.IsChecked = a.FlipX;
        FlipYBox.IsChecked = a.FlipY;
        RefreshAlignReadouts();
    }

    private void WorldList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || WorldList.SelectedIndex < 0) return;
        PullProfile();
        _worldIndex = WorldList.SelectedIndex;
        _suppress = true;
        PushWorldEditor();
        _suppress = false;
        RefreshSample();
    }

    private void AddWorld_Click(object sender, RoutedEventArgs e)
    {
        PullProfile();
        ProjectionWorldBank.Add(_profile);
        _worldIndex = _profile.Worlds.Count - 1;
        _suppress = true;
        RebuildWorldList();
        PushWorldEditor();
        _suppress = false;
        RefreshSample();
        ProfileChanged?.Invoke();
    }

    private void RemoveWorld_Click(object sender, RoutedEventArgs e)
    {
        ProjectionWorldBank.EnsureDefaults(_profile);
        if (_profile.Worlds.Count <= 1) return;
        var id = _profile.Worlds[_worldIndex].Id;
        if (!ProjectionWorldBank.Remove(_profile, id)) return;
        _worldIndex = Math.Min(_worldIndex, _profile.Worlds.Count - 1);
        _suppress = true;
        RebuildWorldList();
        PushWorldEditor();
        _suppress = false;
        RefreshSample();
        ProfileChanged?.Invoke();
    }

    private void WorldName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        ProjectionWorldBank.EnsureDefaults(_profile);
        if (_worldIndex < 0 || _worldIndex >= _profile.Worlds.Count) return;
        var name = WorldNameBox.Text.Trim();
        if (name.Length == 0) return;
        _profile.Worlds[_worldIndex].Name = name;
        _suppress = true;
        RebuildWorldList();
        _suppress = false;
        ProfileChanged?.Invoke();
    }

    private void RefreshAlignReadouts()
    {
        if (AlignWidthLabel is null) return;
        AlignWidthLabel.Text = $"{AlignWidth.Value * 100:0} %";
        AlignHeightLabel.Text = $"{AlignHeight.Value * 100:0} %";
        AlignXLabel.Text = $"{AlignX.Value:+0.00;-0.00;0}";
        AlignYLabel.Text = $"{AlignY.Value:+0.00;-0.00;0}";
        AlignRotLabel.Text = $"{AlignRot.Value:0.0}°";
    }

    private void UpdateProjectorUi()
    {
        var rgy = _profile.IsRgy;
        BlueGainLabel.Visibility = BlueGain.Visibility = rgy ? Visibility.Collapsed : Visibility.Visible;
        YellowGainLabel.Visibility = YellowGain.Visibility = rgy ? Visibility.Visible : Visibility.Collapsed;
        RgyExtras.Visibility = rgy ? Visibility.Visible : Visibility.Collapsed;
        RoutingLabel.Visibility = RoutingBox.Visibility = rgy ? Visibility.Visible : Visibility.Collapsed;
        RoutingBox.IsEnabled = !(_profile.Driver == DacDriverKind.MediaLasUsbBox && rgy);
        TestBlueBtn.Visibility = rgy ? Visibility.Collapsed : Visibility.Visible;
        TestYellowBtn.Visibility = rgy ? Visibility.Visible : Visibility.Collapsed;
        AnalogHint.Text = _profile.AnalogMapSummary();
        if (CurveChannelBox.Items.Count > 3)
        {
            var blueItem = (ComboBoxItem)CurveChannelBox.Items[2];
            var yellowItem = (ComboBoxItem)CurveChannelBox.Items[3];
            blueItem.Visibility = rgy ? Visibility.Collapsed : Visibility.Visible;
            yellowItem.Visibility = rgy ? Visibility.Visible : Visibility.Collapsed;
            if (rgy && CurveChannelBox.SelectedIndex == 2) CurveChannelBox.SelectedIndex = 3;
            if (!rgy && CurveChannelBox.SelectedIndex == 3) CurveChannelBox.SelectedIndex = 2;
        }
    }

    private void RebuildGrid()
    {
        SwatchGrid.Children.Clear();
        for (var i = 0; i < _profile.Entries.Count; i++)
        {
            var index = i;
            var entry = _profile.Entries[i];
            var converted = LaserColorConverter.Apply(
                new LaserPoint(0, 0, 0, entry.InR, entry.InG, entry.InB, 255, false),
                _profile);
            var stack = new StackPanel { Margin = new Thickness(2) };
            stack.Children.Add(new Rectangle
            {
                Height = 18,
                Fill = new SolidColorBrush(Color.FromRgb(entry.InR, entry.InG, entry.InB))
            });
            stack.Children.Add(new Rectangle
            {
                Height = 18,
                Fill = new SolidColorBrush(Color.FromRgb(converted.R, converted.G, converted.B))
            });
            var btn = new Button
            {
                Content = stack,
                Padding = new Thickness(1),
                BorderBrush = index == _selected ? Brushes.White : Brushes.Transparent,
                BorderThickness = new Thickness(2),
                Background = Brushes.Transparent,
                Tag = index
            };
            btn.Click += (_, _) =>
            {
                _selected = index;
                RebuildGrid();
                PushEntry();
            };
            SwatchGrid.Children.Add(btn);
        }
    }

    private void PushEntry()
    {
        if (_selected < 0 || _selected >= _profile.Entries.Count) return;
        _suppress = true;
        var e = _profile.Entries[_selected];
        EntryLabel.Text = $"Index {_selected}  source #{e.InR:X2}{e.InG:X2}{e.InB:X2}";
        OutR.Value = e.OutR;
        OutG.Value = e.OutG;
        OutB.Value = e.OutB;
        var diodes = LaserColorConverter.ConvertDiodes(
            new LaserPoint(0, 0, 0, e.InR, e.InG, e.InB, 255, false),
            _profile);
        var preview = diodes.ToPreviewRgb(_profile.Projector);
        var analog = diodes.ToIldaAnalog(_profile);
        OutPreview.Fill = new SolidColorBrush(Color.FromRgb(preview.r, preview.g, preview.b));
        var pin7 = _profile.IsRgy ? $"jaune {analog.b}" : $"bleu {analog.b}";
        PinText.Text = $"Pin 5 R  {analog.r}\nPin 6 G  {analog.g}\nPin 7 B  {pin7}";
        _suppress = false;
    }

    private void RefreshSample()
    {
        SamplePreview.ShowFrame(LaserColorConverter.Apply(_sample, _profile));
    }
}
