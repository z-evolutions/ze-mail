using System;
using System.IO;
using System.Text.Json;

namespace ZeMail.UI.Services;

public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ZE-Mail", "settings.json");

    // ── Allgemein ────────────────────────────────────────────────────────────
    public string Theme { get; set; } = "Dark";

    // ── Verfassen ────────────────────────────────────────────────────────────
    public string ComposeFormat    { get; set; } = "HTML";
    public string QuoteStyle       { get; set; } = "Above";

    // ── Kalender ─────────────────────────────────────────────────────────────
    public int    DefaultEventDurationMinutes { get; set; } = 60;
    public int    WorkDayStartHour            { get; set; } = 8;
    public int    WorkDayEndHour              { get; set; } = 18;
    public int    FirstDayOfWeek              { get; set; } = 1; // 1=Montag, 0=Sonntag
    public bool   ShowWeekends                { get; set; } = true;

    // ── Benachrichtigungen ───────────────────────────────────────────────────
    public bool ToastNotificationsEnabled { get; set; } = true;

    // ── Laden / Speichern ────────────────────────────────────────────────────
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions)
                   ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}