using Lumina.Core;
using Xunit;

namespace Lumina.Core.Tests;

public class ColorConversionTests
{
    private static LaserPoint P(byte r, byte g, byte b) =>
        new(0, 0, 0, r, g, b, 255, false);

    [Fact]
    public void Rgb_keeps_blue_on_ilda_pin_7()
    {
        var profile = ColorConversionProfile.CreateRgb();
        var diodes = LaserColorConverter.ConvertDiodes(P(0, 0, 255), profile);
        var analog = diodes.ToIldaAnalog(profile);
        Assert.Equal(0, analog.r);
        Assert.Equal(0, analog.g);
        Assert.Equal(255, analog.b);
        Assert.Equal(0, diodes.Y);
    }

    [Fact]
    public void Rgy_maps_source_blue_onto_yellow_ilda_pin()
    {
        var profile = ColorConversionProfile.CreateRgy();
        var diodes = LaserColorConverter.ConvertDiodes(P(0, 0, 255), profile);
        var analog = diodes.ToIldaAnalog(profile);
        Assert.Equal(0, analog.r);
        Assert.Equal(0, analog.g);
        Assert.True(analog.b > 200, "yellow must ride USB-Box / ILDA blue analog");
        Assert.Equal(0, diodes.B);
        Assert.True(diodes.Y > 200);
    }

    [Fact]
    public void Rgy_preview_shows_yellow_not_blue()
    {
        var profile = ColorConversionProfile.CreateRgy();
        var preview = LaserColorConverter.Apply(P(0, 0, 255), profile);
        Assert.Equal(0, preview.B);
        Assert.True(preview.R > 200);
        Assert.True(preview.G > 200);
    }

    [Fact]
    public void Rgy_pure_red_does_not_drive_yellow_pin()
    {
        var profile = ColorConversionProfile.CreateRgy();
        profile.YellowFromRedGreen = 0;
        var analog = LaserColorConverter.ConvertDiodes(P(255, 0, 0), profile).ToIldaAnalog(profile);
        Assert.Equal(255, analog.r);
        Assert.Equal(0, analog.g);
        Assert.Equal(0, analog.b);
    }

    [Fact]
    public void Usb_box_rgy_warns_if_yellow_is_not_on_blue_pin()
    {
        var profile = ColorConversionProfile.CreateRgy();
        profile.Driver = DacDriverKind.MediaLasUsbBox;
        profile.YellowRouting = IldaYellowRouting.MixOnRedGreen;
        var warnings = DacColorCompatibility.Warnings(profile);
        Assert.Contains(warnings, w => w.Contains("trois sorties", StringComparison.OrdinalIgnoreCase) ||
                                       w.Contains("broche ILDA bleu", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Intensity_curve_identity_leaves_mid_grey()
    {
        var curve = new IntensityCurve();
        Assert.Equal(0.5, curve.Map(0.5), 3);
        Assert.Equal(0, curve.Map(0), 3);
        Assert.Equal(1, curve.Map(1), 3);
    }

    [Fact]
    public void Test_patterns_have_five_brightness_steps()
    {
        var red = ColorTestPatterns.Red();
        Assert.True(red.Points.Count >= 5 * 40);
        Assert.Contains(red.Points, p => p.R == 255);
        Assert.Contains(red.Points, p => p.R == 0 && p.Blanked);
    }

    [Fact]
    public void Projection_alignment_scales_and_shifts()
    {
        var align = new ProjectionAlignment { Width = 0.5, Height = 1 };
        var mapped = ProjectionMapper.Map(new LaserPoint(32767, 0, 0, 255, 0, 0, 255, false), align);
        Assert.InRange(mapped.X, 16380, 16400);
        var shifted = ProjectionMapper.Map(
            new LaserPoint(0, 0, 0, 255, 0, 0, 255, false),
            new ProjectionAlignment { OffsetX = 0.25 });
        Assert.InRange(shifted.X, 8190, 8200);
    }

    [Fact]
    public void Alignment_mire_has_a_closed_frame()
    {
        var mire = ProjectionMire.Grid();
        Assert.True(mire.Points.Count > 80);
        Assert.Contains(mire.Points, p => p.IsVisible);
    }

    [Fact]
    public void Projector_profile_roundtrips_alignment()
    {
        var path = Path.Combine(Path.GetTempPath(), "lumina-profile-test.lumina-profile.json");
        try
        {
            var profile = ColorConversionProfile.CreateRgb();
            profile.Name = "Salle";
            profile.Alignment.Width = 0.8;
            profile.Alignment.Height = 0.6;
            profile.Alignment.OffsetX = 0.1;
            ProjectorProfileStore.Save(path, profile);
            var loaded = ProjectorProfileStore.Load(path);
            Assert.Equal("Salle", loaded.Name);
            Assert.Equal(0.8, loaded.Alignment.Width, 3);
            Assert.Equal(0.6, loaded.Alignment.Height, 3);
            Assert.Equal(0.1, loaded.Alignment.OffsetX, 3);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
