namespace PaperWindow;

/// <summary>A global key combination, stored in settings as text like "Ctrl+Alt+Win+P".</summary>
internal readonly record struct Hotkey(Keys Key, bool Ctrl, bool Alt, bool Shift, bool Win)
{
    public static readonly Hotkey Default = new(Keys.P, Ctrl: true, Alt: true, Shift: false, Win: true);

    public uint Modifiers =>
        (Alt ? Native.MOD_ALT : 0) | (Ctrl ? Native.MOD_CONTROL : 0) | (Shift ? Native.MOD_SHIFT : 0) | (Win ? Native.MOD_WIN : 0);

    /// <summary>
    /// A usable global hotkey needs Ctrl, Alt or Win (Shift alone would steal normal typing), except F1–F24,
    /// and the key itself must not be a modifier.
    /// </summary>
    public bool IsValid =>
        Key is not (Keys.None or Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        && (Key & Keys.Modifiers) == 0
        && (Ctrl || Alt || Win || Key is >= Keys.F1 and <= Keys.F24);

    public override string ToString()
    {
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Win) parts.Add("Win");
        parts.Add(Key is >= Keys.D0 and <= Keys.D9 ? ((char)('0' + (Key - Keys.D0))).ToString() : Key.ToString());
        return string.Join("+", parts);
    }

    /// <summary>Parses "Ctrl+Alt+Win+P"; empty text means "no hotkey" (success with null).</summary>
    public static bool TryParse(string? text, out Hotkey? hotkey)
    {
        hotkey = null;
        if (string.IsNullOrWhiteSpace(text)) return true;

        bool ctrl = false, alt = false, shift = false, win = false;
        Keys key = Keys.None;
        foreach (var raw in text.Split('+'))
        {
            string part = raw.Trim();
            switch (part.ToLowerInvariant())
            {
                case "ctrl": ctrl = true; break;
                case "alt": alt = true; break;
                case "shift": shift = true; break;
                case "win": win = true; break;
                default:
                    if (key != Keys.None) return false;
                    if (part.Length == 1 && char.IsDigit(part[0])) key = Keys.D0 + (part[0] - '0');
                    else if (!Enum.TryParse(part, ignoreCase: true, out key) || int.TryParse(part, out _)) return false;
                    break;
            }
        }
        var parsed = new Hotkey(key, ctrl, alt, shift, win);
        if (!parsed.IsValid) return false;
        hotkey = parsed;
        return true;
    }

    /// <summary>Whether a Windows key is held right now (WinForms key events don't report it).</summary>
    public static bool WinKeyDown =>
        (Native.GetKeyState(0x5B) & 0x8000) != 0 || (Native.GetKeyState(0x5C) & 0x8000) != 0;
}

/// <summary>Owns the system-wide registration of one hotkey through a hidden message-only window.</summary>
internal sealed class HotkeyManager : NativeWindow, IDisposable
{
    const int Id = 1;

    Hotkey? _desired;
    bool _registered;
    bool _suspended;

    public event Action? Pressed;

    /// <summary>True when the desired hotkey could not be registered (usually another program already owns it).</summary>
    public bool Failed { get; private set; }

    public HotkeyManager() => CreateHandle(new CreateParams { Parent = Native.HWND_MESSAGE });

    public void Set(Hotkey? hotkey)
    {
        if (_desired == hotkey && !Failed) return;
        _desired = hotkey;
        Refresh();
    }

    /// <summary>Temporarily releases the hotkey, e.g. while the user records a new one in the settings window.</summary>
    public void Suspend(bool suspended)
    {
        _suspended = suspended;
        Refresh();
    }

    void Refresh()
    {
        if (_registered) Native.UnregisterHotKey(Handle, Id);
        _registered = false;
        Failed = false;
        if (_suspended || _desired is not { } hk) return;
        _registered = Native.RegisterHotKey(Handle, Id, hk.Modifiers | Native.MOD_NOREPEAT, (uint)hk.Key);
        Failed = !_registered;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && (int)m.WParam == Id)
            Pressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registered) Native.UnregisterHotKey(Handle, Id);
        _registered = false;
        DestroyHandle();
    }
}
