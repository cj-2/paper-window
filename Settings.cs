using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace PaperWindow;

/// <summary>A named look: color effect, blue light, brightness, paper texture and mouse effects.</summary>
internal sealed class Profile
{
    public const string DefaultName = "Padrão";
    public const int MinBrightness = 20;   // below this the screen becomes hard to read
    public const int MinTrailLength = 10;

    public string Name { get; set; } = DefaultName;
    public string Effect { get; set; } = ColorEffects.All[0].Id;
    public int FilterStrength { get; set; } = 100;   // 0..100
    public int BlueLightFilter { get; set; } = 0;    // 0..100
    public int Brightness { get; set; } = 100;       // MinBrightness..100
    public string Texture { get; set; } = TextureStyles.All[0].Id;
    public int TextureOpacity { get; set; } = 55;    // 0..100
    public bool CursorTheme { get; set; } = false;
    public string CursorStyle { get; set; } = CursorStyles.All[0].Id;
    public bool InkTrail { get; set; } = false;
    public int InkTrailLength { get; set; } = 40;    // MinTrailLength..100 (x15 ms)

    public Profile Clone(string name)
    {
        var p = (Profile)MemberwiseClone();
        p.Name = name;
        return p;
    }

    /// <summary>Copies everything but the name.</summary>
    public void CopyLookFrom(Profile other)
    {
        Effect = other.Effect;
        FilterStrength = other.FilterStrength;
        BlueLightFilter = other.BlueLightFilter;
        Brightness = other.Brightness;
        Texture = other.Texture;
        TextureOpacity = other.TextureOpacity;
        CursorTheme = other.CursorTheme;
        CursorStyle = other.CursorStyle;
        InkTrail = other.InkTrail;
        InkTrailLength = other.InkTrailLength;
    }

    public bool SameLook(Profile o) =>
        Effect == o.Effect && FilterStrength == o.FilterStrength && BlueLightFilter == o.BlueLightFilter
        && Brightness == o.Brightness && Texture == o.Texture && TextureOpacity == o.TextureOpacity
        && CursorTheme == o.CursorTheme && CursorStyle == o.CursorStyle
        && InkTrail == o.InkTrail && InkTrailLength == o.InkTrailLength;

    /// <summary>Unknown ids (e.g. from a newer version) and out-of-range values fall back to something valid.</summary>
    public void Normalize()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? DefaultName : Name.Trim();
        Effect = ColorEffects.Find(Effect ?? "").Id;
        Texture = TextureStyles.Find(Texture ?? "").Id;
        FilterStrength = Math.Clamp(FilterStrength, 0, 100);
        BlueLightFilter = Math.Clamp(BlueLightFilter, 0, 100);
        Brightness = Math.Clamp(Brightness, MinBrightness, 100);
        TextureOpacity = Math.Clamp(TextureOpacity, 0, 100);
        CursorStyle = CursorStyles.Find(CursorStyle ?? "").Id;
        InkTrailLength = Math.Clamp(InkTrailLength, MinTrailLength, 100);
    }
}

/// <summary>User preferences, persisted as JSON in %APPDATA%\PaperWindow\settings.json.</summary>
internal sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public bool HideFromCapture { get; set; } = true;
    public string ToggleHotkey { get; set; } = Hotkey.Default.ToString();   // "" = no hotkey
    public string ActiveProfile { get; set; } = Profile.DefaultName;
    public List<Profile> Profiles { get; set; } = [new Profile()];

    /// <summary>Day/night schedule: switches to <see cref="DayProfile"/> at <see cref="DayStart"/> and to <see cref="NightProfile"/> at <see cref="NightStart"/>.</summary>
    public bool ScheduleEnabled { get; set; } = false;
    public string DayProfile { get; set; } = Profile.DefaultName;
    public string NightProfile { get; set; } = Profile.DefaultName;
    public TimeSpan DayStart { get; set; } = new(7, 0, 0);
    public TimeSpan NightStart { get; set; } = new(19, 0, 0);

    /// <summary>Whether <paramref name="time"/> falls in the night period; the night may cross midnight. Equal times mean always day.</summary>
    public bool IsNight(TimeSpan time) =>
        NightStart > DayStart
            ? time >= NightStart || time < DayStart
            : time >= NightStart && time < DayStart;

    /// <summary>The profile the schedule wants at <paramref name="time"/>; null when the schedule is off.</summary>
    public string? ScheduledProfile(TimeSpan time) =>
        !ScheduleEnabled ? null : IsNight(time) ? NightProfile : DayProfile;

    /// <summary>The profile being shown and edited. Always exists after <see cref="Normalize"/>.</summary>
    [JsonIgnore]
    public Profile Current => Profiles.FirstOrDefault(p => p.Name == ActiveProfile) ?? Profiles[0];

    /// <summary>Unsaved edits of the active profile, previewed on screen; null when the saved profile is shown.</summary>
    [JsonIgnore]
    public Profile? Draft { get; set; }

    /// <summary>The look on screen: the draft if there is one, otherwise the saved active profile.</summary>
    [JsonIgnore]
    public Profile Shown => Draft ?? Current;

    [JsonIgnore]
    public bool HasUnsavedChanges => Draft != null && !Draft.SameLook(Current);

    /// <summary>Changes the on-screen look without touching the saved profile.</summary>
    public void EditDraft(Action<Profile> change)
    {
        Draft ??= Current.Clone(Current.Name);
        change(Draft);
    }

    public void SaveDraft()
    {
        if (Draft != null) Current.CopyLookFrom(Draft);
        Draft = null;
    }

    public void DiscardDraft() => Draft = null;

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
        string json = File.ReadAllText(path);
        var s = JsonSerializer.Deserialize<Settings>(json)
                ?? throw new JsonException("Arquivo de configuração vazio.");
        // Files from before profiles existed keep the look at the root: turn it into a single profile.
        if (JsonNode.Parse(json)?["Profiles"] is null)
        {
            var legacy = JsonSerializer.Deserialize<Profile>(json)!;
            legacy.Name = Profile.DefaultName;
            s.Profiles = [legacy];
            s.ActiveProfile = legacy.Name;
        }
        s.Normalize();
        return s;
    }

    public void Save() => Write(FilePath);

    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        // Relaxed escaping keeps the file readable ("Ctrl+Alt+P", "Padrão") for people who open it by hand.
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
    }

    void Normalize()
    {
        var profiles = (Profiles ?? []).Where(p => p != null).ToList();
        Profiles = [];
        foreach (var p in profiles)
        {
            p.Normalize();
            p.Name = UniqueName(p.Name);
            Profiles.Add(p);
        }
        if (Profiles.Count == 0) Profiles.Add(new Profile());
        if (!Profiles.Any(p => p.Name == ActiveProfile)) ActiveProfile = Profiles[0].Name;
        if (!Hotkey.TryParse(ToggleHotkey, out _)) ToggleHotkey = Hotkey.Default.ToString();
        DayStart = TimeOfDay(DayStart);
        NightStart = TimeOfDay(NightStart);
        FixScheduleProfiles();
    }

    /// <summary>Keeps hours and minutes inside one day (hand-edited files may hold "25:00" or seconds).</summary>
    static TimeSpan TimeOfDay(TimeSpan t) =>
        new(((t.Hours % 24) + 24) % 24, ((t.Minutes % 60) + 60) % 60, 0);

    /// <summary>Points schedule slots whose profile no longer exists at the first profile.</summary>
    void FixScheduleProfiles()
    {
        if (!Profiles.Any(p => p.Name == DayProfile)) DayProfile = Profiles[0].Name;
        if (!Profiles.Any(p => p.Name == NightProfile)) NightProfile = Profiles[0].Name;
    }

    /// <summary><paramref name="wanted"/>, or "wanted (2)", "wanted (3)"... — whichever no other profile uses.</summary>
    public string UniqueName(string wanted, Profile? except = null)
    {
        string baseName = string.IsNullOrWhiteSpace(wanted) ? "Perfil" : wanted.Trim();
        string name = baseName;
        for (int n = 2; Profiles.Any(p => p != except && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)); n++)
            name = $"{baseName} ({n})";
        return name;
    }

    /// <summary>Adds a copy of <paramref name="from"/> under a free name and makes it active.</summary>
    public Profile AddProfile(string name, Profile from)
    {
        var p = from.Clone(UniqueName(name));
        Profiles.Add(p);
        ActiveProfile = p.Name;
        return p;
    }

    /// <summary>Renames a profile; returns false if the name is empty or taken by another profile.</summary>
    public bool RenameProfile(Profile p, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0 || UniqueName(newName, except: p) != newName) return false;
        if (ActiveProfile == p.Name) ActiveProfile = newName;
        if (DayProfile == p.Name) DayProfile = newName;
        if (NightProfile == p.Name) NightProfile = newName;
        p.Name = newName;
        return true;
    }

    /// <summary>Deletes a profile, keeping at least one.</summary>
    public bool DeleteProfile(Profile p)
    {
        if (Profiles.Count <= 1 || !Profiles.Remove(p)) return false;
        if (ActiveProfile == p.Name) ActiveProfile = Profiles[0].Name;
        FixScheduleProfiles();
        return true;
    }

    /// <summary>
    /// Adds the profiles of an imported file without touching existing ones: identical profiles are skipped,
    /// name clashes get a suffix. Activates the file's active profile. Returns how many were added.
    /// </summary>
    public int ImportProfiles(Settings other)
    {
        int added = 0;
        string? activate = null;
        foreach (var p in other.Profiles)
        {
            var same = Profiles.FirstOrDefault(e =>
                string.Equals(e.Name, p.Name, StringComparison.OrdinalIgnoreCase) && e.SameLook(p));
            string name;
            if (same != null)
            {
                name = same.Name;
            }
            else
            {
                var copy = p.Clone(UniqueName(p.Name));
                Profiles.Add(copy);
                name = copy.Name;
                added++;
            }
            if (p == other.Current) activate = name;
        }
        if (activate != null) ActiveProfile = activate;
        return added;
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
