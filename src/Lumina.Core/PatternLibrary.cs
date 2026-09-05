namespace Lumina.Core;

public sealed class PatternItem
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public string? SourcePath { get; set; }
    public List<LaserFrame> Frames { get; init; } = [];

    public LaserFrame? FrameAtProgress(double t01)
    {
        if (Frames.Count == 0) return null;
        var i = (int)Math.Clamp(Math.Floor(t01 * Frames.Count), 0, Frames.Count - 1);
        return Frames[i];
    }
}

public sealed class PatternLibrary
{
    public List<PatternItem> Items { get; } = [];

    public PatternItem? Get(string id) =>
        Items.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public PatternItem Add(string name, IReadOnlyList<LaserFrame> frames, string? sourcePath, string? id = null)
    {
        var item = new PatternItem
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N")[..12] : id,
            Name = name,
            SourcePath = sourcePath,
            Frames = frames.ToList()
        };
        Items.Add(item);
        return item;
    }

    public PatternItem ImportIlda(string path)
    {
        var frames = ReadFrames(path);
        var name = Path.GetFileNameWithoutExtension(path);
        return Add(name, frames, path);
    }

    public IReadOnlyList<PatternItem> ImportEachFrame(string path)
    {
        var frames = ReadFrames(path);
        var folder = Path.GetFileNameWithoutExtension(path);
        var added = new List<PatternItem>();
        for (var i = 0; i < frames.Count; i++)
        {
            var frameName = string.IsNullOrWhiteSpace(frames[i].Name) ? $"{folder} {i + 1}" : frames[i].Name;
            added.Add(Add(frameName, [frames[i]], path));
        }

        return added;
    }

    /// <summary>Reads .ild via ILDA header or MediaLas .fra via FraReader.</summary>
    public static IReadOnlyList<LaserFrame> ReadFrames(string path)
    {
        if (path.EndsWith(".fra", StringComparison.OrdinalIgnoreCase))
            return FraReader.Read(path);
        return IldaReader.Read(path);
    }
}
