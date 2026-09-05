using System.Text.Json;

namespace Lumina.Core;

/// <summary>Save/load a projector profile (colours + projection zone) independently of a show.</summary>
public static class ProjectorProfileStore
{
    public const string Extension = ".lumina-profile.json";
    public const string OpenDialogFilter =
        "Profil projecteur Lumina (*.lumina-profile.json)|*.lumina-profile.json|JSON (*.json)|*.json|Tous|*.*";

    public static void Save(string path, ColorConversionProfile profile)
    {
        var json = JsonSerializer.Serialize(profile.Clone(), LuminaJson.Default.ColorConversionProfile);
        File.WriteAllText(path, json);
    }

    public static ColorConversionProfile Load(string path)
    {
        var json = File.ReadAllText(path);
        var loaded = JsonSerializer.Deserialize(json, LuminaJson.Default.ColorConversionProfile)
                     ?? throw new InvalidDataException("Profil projecteur illisible.");
        if (loaded.Entries.Count == 0) loaded.ResetTableToIlda();
        loaded.Alignment ??= new ProjectionAlignment();
        loaded.ApplyProjector(loaded.Projector);
        ProjectionWorldBank.EnsureDefaults(loaded);
        ProjectionWorldBank.SyncPrimary(loaded);
        return loaded;
    }
}
