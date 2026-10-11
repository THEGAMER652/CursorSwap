using System.Text.Json;
using Microsoft.Win32;

namespace CursorSwap;

public enum ClickAnimation { None, Ripple, Pulse, Burst, Ring, Sparkle, Shockwave, Square, Confetti, Splash, Glow }

/// <summary>The cursor "states" Windows switches between while you hover different things.</summary>
public enum CursorRole { Normal, Link, Text, Busy, Working, Precision, Unavailable, Move, Resize, Help }

public enum Hotspot { TopLeft, Center, TopCenter }

public class RoleSetting
{
    public string Path { get; set; } = "";
    public Hotspot Hotspot { get; set; } = Hotspot.TopLeft;
}

public class Settings
{
    public const string DefaultTarget = "Discord";
    public string TargetProcess { get; set; } = DefaultTarget;
    public bool Enabled { get; set; } = true;

    // Cursors
    public Dictionary<string, RoleSetting> Cursors { get; set; } = new();
    public int CursorSize { get; set; } = 32;
    public bool UnsetUseMain { get; set; } = true;

    // Click effects
    public bool ClickEffects { get; set; } = true;
    public bool LeftEnabled { get; set; } = true;
    public bool RightEnabled { get; set; } = true;
    public ClickAnimation LeftAnimation { get; set; } = ClickAnimation.Ripple;
    public int LeftColor { get; set; } = Theme.Accent.ToArgb();
    public ClickAnimation RightAnimation { get; set; } = ClickAnimation.Sparkle;
    public int RightColor { get; set; } = Theme.Accent2.ToArgb();
    public float EffectScale { get; set; } = 1f;
    public float EffectSpeed { get; set; } = 1f;

    public bool TrayHintShown { get; set; }

    // Legacy (v1) fields, migrated on load
    public string CursorPath { get; set; } = "";
    public bool HotspotCenter { get; set; }

    public RoleSetting Role(CursorRole r)
    {
        if (!Cursors.TryGetValue(r.ToString(), out var rs) || rs == null)
            Cursors[r.ToString()] = rs = new RoleSetting { Hotspot = DefaultHotspot(r) };
        return rs;
    }

    public static Hotspot DefaultHotspot(CursorRole r) => r switch
    {
        CursorRole.Text or CursorRole.Busy or CursorRole.Precision or CursorRole.Unavailable
            or CursorRole.Move or CursorRole.Resize => Hotspot.Center,
        _ => Hotspot.TopLeft
    };

    static string Dir => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CursorSwap");
    static string FilePath => System.IO.Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        Settings s;
        try
        {
            s = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings()
                : new Settings();
        }
        catch { s = new Settings(); }

        s.Cursors ??= new();
        if (string.IsNullOrWhiteSpace(s.TargetProcess)) s.TargetProcess = DefaultTarget;
        // v1 -> v2 migration
        if (!string.IsNullOrEmpty(s.CursorPath) && string.IsNullOrEmpty(s.Role(CursorRole.Normal).Path))
        {
            s.Role(CursorRole.Normal).Path = s.CursorPath;
            s.Role(CursorRole.Normal).Hotspot = s.HotspotCenter ? Hotspot.Center : Hotspot.TopLeft;
        }
        s.CursorPath = "";
        if (s.LeftAnimation == ClickAnimation.None) { s.LeftEnabled = false; s.LeftAnimation = ClickAnimation.Ripple; }
        if (s.RightAnimation == ClickAnimation.None) { s.RightEnabled = false; s.RightAnimation = ClickAnimation.Sparkle; }
        s.CursorSize = Math.Clamp(s.CursorSize, 16, 128);
        s.EffectScale = Math.Clamp(s.EffectScale, 0.5f, 2f);
        s.EffectSpeed = Math.Clamp(s.EffectSpeed, 0.5f, 2f);
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

public static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "CursorSwap";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enable)
            key.SetValue(ValueName, $"\"{Application.ExecutablePath}\" --tray");
        else if (key.GetValue(ValueName) != null)
            key.DeleteValue(ValueName);
    }
}
