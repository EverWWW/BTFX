using System.IO;
using System.Text.Json;
using BTFX.Models.Camera;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

public sealed class CameraCaptureSettingsService : ICameraCaptureSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string SettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "Data", "Config", "camera-capture-settings.json");

    public CameraCaptureSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var defaults = new CameraCaptureSettings();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<CameraCaptureSettings>(json, JsonOptions) ?? new CameraCaptureSettings();
        }
        catch
        {
            return new CameraCaptureSettings();
        }
    }

    public void Save(CameraCaptureSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
