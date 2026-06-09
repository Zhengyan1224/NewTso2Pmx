using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NewTso2Pmx.App.Configuration;

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    public static string SettingsPath
    {
        get
        {
            var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory = AppContext.BaseDirectory;
            }

            return Path.Combine(baseDirectory, "NewTso2Pmx", "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                           ?? new AppSettings();
            Normalize(settings);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Normalize(settings);
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static void Normalize(AppSettings settings)
    {
        settings.Comment ??= string.Empty;
        settings.OutputMode = settings.OutputMode is AppOutputModes.SourceFolder or AppOutputModes.CustomFolder
            ? settings.OutputMode
            : AppOutputModes.CreateSubfolder;
        settings.CustomOutputFolder ??= string.Empty;
        settings.BoneTableSelection ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        settings.ExtraPhysicsSelection ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        settings.RecentDirectories ??= new Dictionary<string, string>(StringComparer.Ordinal);
        settings.EditSession ??= new EditSessionSettings();
        settings.EditSession.SourcePath ??= string.Empty;
        settings.EditSession.TsoCategories ??= [];
        settings.EditSession.SelectedSubMeshes ??= [];
    }
}
