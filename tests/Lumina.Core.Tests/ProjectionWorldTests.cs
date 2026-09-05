using Lumina.Core;

namespace Lumina.Core.Tests;

public class ProjectionWorldTests
{
    private static LaserFrame Square()
    {
        return new LaserFrame
        {
            Name = "sq",
            Points =
            [
                new LaserPoint(-10000, -10000, 0, 255, 0, 0, 255, false),
                new LaserPoint(10000, -10000, 0, 255, 0, 0, 255, false),
                new LaserPoint(10000, 10000, 0, 255, 0, 0, 255, false),
                new LaserPoint(-10000, 10000, 0, 255, 0, 0, 255, false),
            ]
        };
    }

    [Fact]
    public void EnsureDefaults_CreatesPrimaryWorldFromAlignment()
    {
        var profile = ColorConversionProfile.CreateRgb();
        profile.Alignment.OffsetX = 0.2;
        ProjectionWorldBank.EnsureDefaults(profile);
        Assert.Single(profile.Worlds);
        Assert.Equal(0.2, profile.Worlds[0].Alignment.OffsetX, 3);
    }

    [Fact]
    public void Compose_TwoWorlds_DuplicatesGeometryWithBlankJump()
    {
        var profile = ColorConversionProfile.CreateRgb();
        ProjectionWorldBank.EnsureDefaults(profile);
        ProjectionWorldBank.Add(profile, "Monde 2");
        profile.Worlds[0].Alignment.OffsetX = -0.3;
        profile.Worlds[1].Alignment.OffsetX = 0.3;
        ProjectionWorldBank.SyncPrimary(profile);

        var composed = ProjectionComposer.Compose(Square(), profile);
        Assert.NotNull(composed);
        // 4 + blank + 4
        Assert.True(composed!.Points.Count >= 9);
        Assert.Contains(composed.Points, p => p.Blanked);
    }

    [Fact]
    public void ForProjector_FiltersByIndex()
    {
        var profile = ColorConversionProfile.CreateRgb();
        ProjectionWorldBank.EnsureDefaults(profile);
        var w2 = ProjectionWorldBank.Add(profile, "P2");
        w2.ProjectorIndex = 1;
        profile.Worlds[0].ProjectorIndex = 0;

        var only1 = ProjectionComposer.ForProjector(Square(), profile, 1);
        Assert.NotNull(only1);
        Assert.Equal(4, only1!.Points.Count);

        var missing = ProjectionComposer.ForProjector(Square(), profile, 5);
        Assert.Null(missing);
    }

    [Fact]
    public void Remove_KeepsAtLeastOneWorld()
    {
        var profile = ColorConversionProfile.CreateRgb();
        ProjectionWorldBank.EnsureDefaults(profile);
        Assert.False(ProjectionWorldBank.Remove(profile, profile.Worlds[0].Id));
        ProjectionWorldBank.Add(profile);
        Assert.True(ProjectionWorldBank.Remove(profile, profile.Worlds[^1].Id));
        Assert.Single(profile.Worlds);
    }
}
