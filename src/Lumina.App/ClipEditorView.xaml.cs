using System.Windows;
using System.Windows.Controls;
using Lumina.Core;

namespace Lumina.App;

public partial class ClipEditorView : UserControl
{
    private TimelineClip? _clip;
    private bool _suppress;

    public event Action? EffectsChanged;
    public event Action<TimelineClip>? DeleteRequested;

    public ClipEditorView()
    {
        InitializeComponent();
        IsEnabled = false;
    }

    public void Bind(TimelineClip? clip)
    {
        _clip = clip;
        IsEnabled = clip is not null;
        _suppress = true;
        if (clip is null)
        {
            ClipLabel.Text = "Sélectionnez un clip sur la timeline, ou placez un motif.";
        }
        else
        {
            ClipLabel.Text = $"{clip.Name}  ·  {clip.Duration.TotalSeconds:0.##} s";
            var fx = clip.Effects;
            Intensity.Value = fx.Intensity;
            Red.Value = fx.Red;
            Green.Value = fx.Green;
            Blue.Value = fx.Blue;
            Hue.Value = fx.Hue;
            Rotation.Value = fx.Rotation;
            WidthFx.Value = fx.Width;
            HeightFx.Value = fx.Height;
            OffsetX.Value = fx.OffsetX;
            OffsetY.Value = fx.OffsetY;
        }

        _suppress = false;
    }

    private void Fx_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppress || _clip is null) return;
        _clip.Effects.Intensity = Intensity.Value;
        _clip.Effects.Red = Red.Value;
        _clip.Effects.Green = Green.Value;
        _clip.Effects.Blue = Blue.Value;
        _clip.Effects.Hue = Hue.Value;
        _clip.Effects.Rotation = Rotation.Value;
        _clip.Effects.Width = WidthFx.Value;
        _clip.Effects.Height = HeightFx.Value;
        _clip.Effects.OffsetX = OffsetX.Value;
        _clip.Effects.OffsetY = OffsetY.Value;
        EffectsChanged?.Invoke();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_clip is null) return;
        _clip.Effects = new ClipEffects();
        Bind(_clip);
        EffectsChanged?.Invoke();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_clip is not null) DeleteRequested?.Invoke(_clip);
    }
}
