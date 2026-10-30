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
            if (File.Exists(FilePath)) return Read(FilePath);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable file: fall back to defaults rather than refusing to start.
        }
        return new Settings();
    }

    /// <summary>Reads a settings file (the app's own or an exported one). Throws on unreadable or invalid JSON.</summary>
    public static Settings Read(string path)
    {
        var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path))
                ?? throw new JsonException("Arquivo de configuração vazio.");
        // Unknown ids (e.g. from a newer version) and out-of-range values fall back to something valid.
        s.Effect = ColorEffects.Find(s.Effect ?? "").Id;
        s.Texture = TextureStyles.Find(s.Texture ?? "").Id;
        s.FilterStrength = Math.Clamp(s.FilterStrength, 0, 100);
        s.TextureOpacity = Math.Clamp(s.TextureOpacity, 0, 100);
        return s;
    }

    public void Save() => Write(FilePath);

    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
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
