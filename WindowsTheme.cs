using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PaperWindow;

/// <summary>
/// Switches the Windows light/dark mode (apps and system, as in Settings &gt; Personalization &gt; Colors) for the current user.
/// </summary>
internal static class WindowsTheme
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    const string AppsValue = "AppsUseLightTheme", SystemValue = "SystemUsesLightTheme";

    /// <summary>Whether Windows apps currently use dark mode.</summary>
    public static bool IsDark
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key);
            return key?.GetValue(AppsValue) is int apps && apps == 0;
        }
    }

    /// <summary>Sets dark or light mode; does nothing when Windows already uses it.</summary>
    public static void Set(bool dark)
    {
        int light = dark ? 0 : 1;
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (key.GetValue(AppsValue) is int apps && apps == light && key.GetValue(SystemValue) is int system && system == light)
            return;
        key.SetValue(AppsValue, light, RegistryValueKind.DWord);
        key.SetValue(SystemValue, light, RegistryValueKind.DWord);
        // Same notification the Settings app sends, so Explorer, the taskbar and open apps repaint right away.
        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 1000, out _);
    }

    static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    const uint WM_SETTINGCHANGE = 0x001A, SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);
}
