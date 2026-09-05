namespace Lumina.Core;

/// <summary>
/// Independent projection zone ("monde") — geometry + logical projector index.
/// Multiple enabled worlds duplicate content into separate zones (preview composite today;
/// multi-DAC routing later).
/// </summary>
public sealed class ProjectionWorld
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Name { get; set; } = "Monde 1";
    public bool Enabled { get; set; } = true;

    /// <summary>0 = primary DAC / preview; higher indices reserved for multi-projector output.</summary>
    public int ProjectorIndex { get; set; }

    public ProjectionAlignment Alignment { get; set; } = new();

    public ProjectionWorld Clone() => new()
    {
        Id = Id,
        Name = Name,
        Enabled = Enabled,
        ProjectorIndex = ProjectorIndex,
        Alignment = (Alignment ?? new ProjectionAlignment()).Clone()
    };
}

public static class ProjectionWorldBank
{
    public static void EnsureDefaults(ColorConversionProfile profile)
    {
        profile.Alignment ??= new ProjectionAlignment();
        profile.Worlds ??= [];
        if (profile.Worlds.Count == 0)
        {
            profile.Worlds.Add(new ProjectionWorld
            {
                Name = "Monde 1",
                Enabled = true,
                ProjectorIndex = 0,
                Alignment = profile.Alignment.Clone()
            });
        }
    }

    /// <summary>Keep legacy Alignment in sync with world 0 (primary).</summary>
    public static void SyncPrimary(ColorConversionProfile profile)
    {
        EnsureDefaults(profile);
        profile.Worlds[0].Alignment.CopyFrom(profile.Alignment);
    }

    public static void PullPrimaryIntoAlignment(ColorConversionProfile profile)
    {
        EnsureDefaults(profile);
        profile.Alignment.CopyFrom(profile.Worlds[0].Alignment ?? new ProjectionAlignment());
    }

    public static IReadOnlyList<ProjectionWorld> Active(ColorConversionProfile profile)
    {
        EnsureDefaults(profile);
        return profile.Worlds.Where(w => w.Enabled).ToList();
    }

    public static ProjectionWorld Add(ColorConversionProfile profile, string? name = null)
    {
        EnsureDefaults(profile);
        var n = profile.Worlds.Count + 1;
        var world = new ProjectionWorld
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Monde {n}" : name!,
            Enabled = true,
            ProjectorIndex = Math.Min(n - 1, 7),
            Alignment = new ProjectionAlignment
            {
                Width = 0.45,
                Height = 0.45,
                OffsetX = n == 2 ? -0.35 : n == 3 ? 0.35 : 0,
                OffsetY = n >= 4 ? -0.25 : 0
            }
        };
        profile.Worlds.Add(world);
        return world;
    }

    public static bool Remove(ColorConversionProfile profile, string id)
    {
        EnsureDefaults(profile);
        if (profile.Worlds.Count <= 1) return false;
        var idx = profile.Worlds.FindIndex(w => w.Id == id);
        if (idx < 0) return false;
        profile.Worlds.RemoveAt(idx);
        PullPrimaryIntoAlignment(profile);
        return true;
    }
}

/// <summary>Composites a frame into one or more projection worlds.</summary>
public static class ProjectionComposer
{
    public static LaserFrame? Compose(LaserFrame? frame, ColorConversionProfile? profile)
    {
        if (frame is null) return null;
        if (profile is null) return frame;

        ProjectionWorldBank.EnsureDefaults(profile);
        var active = ProjectionWorldBank.Active(profile);
        if (active.Count == 0) return frame;
        if (active.Count == 1)
            return ProjectionMapper.Apply(frame, active[0].Alignment);

        var points = new List<LaserPoint>(frame.Points.Count * active.Count + active.Count);
        foreach (var world in active)
        {
            var mapped = ProjectionMapper.Apply(frame, world.Alignment);
            if (mapped is null || mapped.Points.Count == 0) continue;

            if (points.Count > 0)
            {
                var first = mapped.Points[0];
                points.Add(first with
                {
                    Blanked = true,
                    R = 0,
                    G = 0,
                    B = 0,
                    Intensity = 0
                });
            }

            points.AddRange(mapped.Points);
        }

        if (points.Count == 0) return frame;

        return new LaserFrame
        {
            Name = frame.Name,
            Company = frame.Company,
            FrameNumber = frame.FrameNumber,
            TotalFrames = frame.TotalFrames,
            Projector = frame.Projector,
            Format = frame.Format,
            Points = points
        };
    }

    /// <summary>Frames destined for a single logical projector index (multi-DAC later).</summary>
    public static LaserFrame? ForProjector(LaserFrame? frame, ColorConversionProfile? profile, int projectorIndex)
    {
        if (frame is null || profile is null) return frame;
        ProjectionWorldBank.EnsureDefaults(profile);
        var worlds = profile.Worlds.Where(w => w.Enabled && w.ProjectorIndex == projectorIndex).ToList();
        if (worlds.Count == 0) return null;
        var temp = profile.Clone();
        temp.Worlds = worlds.Select(w => w.Clone()).ToList();
        return Compose(frame, temp);
    }
}
