using System.Text.Json;
using System.IO;
using System;

namespace Lumina.App;

/// <summary>Persist les préférences utilisateur de l'application.</summary>
public sealed class LuminaUserSettings
{
    public string? IldaContentFolder { get; set; }

    private static string SettingsFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumina");

    private static string SettingsPath => Path.Combine(SettingsFolder, "settings.json");

    public static LuminaUserSettings Load()
    {
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path)) return new LuminaUserSettings();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LuminaUserSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new LuminaUserSettings();
        }
        catch
        {
            // En cas d'erreur de lecture/deserialization, on repart sur des valeurs par défaut.
            return new LuminaUserSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsFolder);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}

