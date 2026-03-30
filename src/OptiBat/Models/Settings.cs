using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OptiBat.Models;

public enum PowerSourceAction { Activate, Deactivate, DoNothing }

/// <summary>
/// All user-configurable settings. Persisted as JSON.
/// Follows optiRAM pattern: post-deserialization validation with Math.Clamp.
/// </summary>
public sealed class Settings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "optiBAT");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");
    private static readonly string SnapshotFile = Path.Combine(SettingsDir, "snapshots.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private CancellationTokenSource? _debounceCts;

    // ── Auto-optimization ────────────────────────────────────────────
    public bool AutoOptimizeOnBattery { get; set; } = true;
    public int DebouncePowerChangeSeconds { get; set; } = 2;

    // ── Per-domain toggles ───────────────────────────────────────────
    public bool EcoQosEnabled { get; set; } = true;
    public bool TimerResolutionEnabled { get; set; } = true;
    public bool BackgroundServicesEnabled { get; set; } = true;
    public bool UsbSuspendEnabled { get; set; } = true;
    public bool NetworkPowerEnabled { get; set; } = true;
    public bool GpuPowerEnabled { get; set; } = true;
    public bool CpuParkingEnabled { get; set; } = true;
    public bool DiskCoalescingEnabled { get; set; } = true;

    // ── Domain-specific settings ─────────────────────────────────────
    public List<string> EcoQosExcludedProcesses { get; set; } =
    [
        "System", "Idle", "smss", "csrss", "wininit", "services",
        "lsass", "svchost", "dwm", "winlogon", "fontdrvhost", "conhost",
        "Memory Compression", "Registry"
    ];

    public List<string> ServicesToThrottle { get; set; } =
    [
        "WSearch",     // Windows Search indexing
        "SysMain",     // Superfetch
        "DiagTrack",   // Connected User Experiences and Telemetry
        "BITS",        // Background Intelligent Transfer
        "wuauserv",    // Windows Update
        "DoSvc",       // Delivery Optimization
        "DPS",         // Diagnostic Policy Service
        "WdiServiceHost", // Diagnostic Service Host
    ];

    public int CpuParkingMinProcessorDC { get; set; } = 5;
    public int DiskIdleTimeoutSeconds { get; set; } = 30;

    // ── UI state ─────────────────────────────────────────────────────
    public double WindowWidth { get; set; } = 960;
    public double WindowHeight { get; set; } = 660;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public string ThemeMode { get; set; } = "System";

    // ── Paths ────────────────────────────────────────────────────────
    public static string GetSettingsDir() => SettingsDir;
    public static string GetSnapshotPath() => SnapshotFile;

    // ── Load / Save ──────────────────────────────────────────────────

    public static Settings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new Settings();

            var json = File.ReadAllText(SettingsFile);
            var settings = JsonSerializer.Deserialize<Settings>(json, JsonOptions) ?? new Settings();
            settings.Validate();
            return settings;
        }
        catch
        {
            return new Settings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Silent — settings loss is non-critical
        }
    }

    public async void SaveDebounced()
    {
        var old = Interlocked.Exchange(ref _debounceCts, new CancellationTokenSource());
        old?.Cancel();
        old?.Dispose();
        var token = _debounceCts!.Token;

        // Snapshot JSON on calling thread to avoid torn reads
        var json = JsonSerializer.Serialize(this, JsonOptions);

        try
        {
            await Task.Delay(500, token);
            Directory.CreateDirectory(SettingsDir);
            await File.WriteAllTextAsync(SettingsFile, json, token);
        }
        catch (OperationCanceledException)
        {
            // Expected from debounce
        }
        catch
        {
            // Silent
        }
    }

    private void Validate()
    {
        DebouncePowerChangeSeconds = Math.Clamp(DebouncePowerChangeSeconds, 1, 10);
        CpuParkingMinProcessorDC = Math.Clamp(CpuParkingMinProcessorDC, 0, 100);
        DiskIdleTimeoutSeconds = Math.Clamp(DiskIdleTimeoutSeconds, 10, 300);

        WindowWidth = Math.Clamp(WindowWidth, 400, 4000);
        WindowHeight = Math.Clamp(WindowHeight, 300, 3000);
        if (!double.IsFinite(WindowLeft)) WindowLeft = double.NaN;
        if (!double.IsFinite(WindowTop)) WindowTop = double.NaN;

        ThemeMode ??= "System";
        EcoQosExcludedProcesses ??= [];
        ServicesToThrottle ??= [];
    }
}
