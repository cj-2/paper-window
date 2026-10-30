using System.Text.Json;
using Microsoft.Win32;

namespace PaperWindow;

/// <summary>User preferences, persisted as JSON in %APPDATA%\PaperWindow\settings.json.</summary>
internal sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public string Effect { get; set; } = ColorEffects.All[0].Id;
    public int FilterStrength { get; set; } = 100;   // 0..100
    public string Texture { get; set; } = TextureStyles.All[0].Id;
    public int TextureOpacity { get; set; } = 55;    // 0..100
    public bool HideFromCapture { get; set; } = true;

    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PaperWindow", "settings.json");

    public static bool Exists => File.Exists(FilePath);

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable file: fall back to defaults rather than refusing to start.
        }
        return new Settings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void CopyFrom(Settings other)
    {
        Enabled = other.Enabled;
        Effect = other.Effect;
        FilterStrength = other.FilterStrength;
        Texture = other.Texture;
        TextureOpacity = other.TextureOpacity;
        HideFromCapture = other.HideFromCapture;
    }
}

/// <summary>"Start with Windows" via the per-user Run key.</summary>
internal static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "PaperWindow";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
                key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(RunValue, false);
        }
    }
}
