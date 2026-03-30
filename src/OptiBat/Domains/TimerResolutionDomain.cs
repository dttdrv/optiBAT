using System.Diagnostics;
using OptiBat.Models;
using OptiBat.Native;

namespace OptiBat.Domains;

/// <summary>
/// Clamps inflated timer resolutions back to default.
/// Many apps (Chrome, Spotify, games) set the system timer to 1ms or 0.5ms,
/// which forces 1000-2000 CPU wakeups/sec even when idle.
/// The default 15.625ms (64 Hz) is much more power-efficient.
///
/// On Windows 11 22H2+, uses PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION
/// to tell the OS to ignore per-process timer requests for background apps.
/// On older Windows, falls back to NtSetTimerResolution to reset the global timer.
/// </summary>
public sealed class TimerResolutionDomain : IOptimizationDomain
{
    private readonly Settings _settings;
    private bool _isActive;
    private uint _originalResolution;
    private readonly HashSet<uint> _processesIgnored = [];

    // Default Windows timer resolution: 15.625ms = 156250 in 100ns units
    private const uint DEFAULT_RESOLUTION = 156250;

    public string Id => "timer-resolution";
    public string DisplayName => "Timer Resolution";
    public bool IsSupported => true; // Available on all Windows versions
    public bool IsActive => _isActive;

    public TimerResolutionDomain(Settings settings)
    {
        _settings = settings;
    }

    public DomainSnapshot CaptureBaseline()
    {
        var snapshot = new DomainSnapshot { DomainId = Id };

        // Query current global timer resolution
        NativeMethods.NtQueryTimerResolution(out var min, out var max, out var current);
        snapshot.Set("currentResolution", current);
        snapshot.Set("minResolution", min);
        snapshot.Set("maxResolution", max);

        return snapshot;
    }

    public ApplyResult Apply(DomainSnapshot baseline)
    {
        var sw = Stopwatch.StartNew();
        _originalResolution = baseline.Get<uint>("currentResolution");
        int ignored = 0, failed = 0, skipped = 0;
        var exclusions = new HashSet<string>(_settings.EcoQosExcludedProcesses,
            StringComparer.OrdinalIgnoreCase);

        _processesIgnored.Clear();

        // Strategy 1: Per-process timer resolution ignore (Windows 11 22H2+)
        // This tells Windows to ignore the timer resolution requested by background processes
        var isWin11 = Environment.OSVersion.Version >= new Version(10, 0, 22621);

        if (isWin11)
        {
            var foregroundPid = NativeMethods.GetForegroundProcessId();

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var pid = (uint)proc.Id;
                    if (pid <= 4 || pid == Environment.ProcessId || pid == foregroundPid)
                    {
                        skipped++;
                        continue;
                    }

                    if (exclusions.Contains(proc.ProcessName))
                    {
                        skipped++;
                        continue;
                    }

                    var handle = NativeMethods.OpenProcess(
                        NativeMethods.PROCESS_SET_INFORMATION, false, pid);
                    if (handle == IntPtr.Zero) { skipped++; continue; }

                    try
                    {
                        if (NativeMethods.SetProcessTimerResolutionIgnore(handle, true))
                        {
                            _processesIgnored.Add(pid);
                            ignored++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(handle);
                    }
                }
                catch { skipped++; }
                finally { proc.Dispose(); }
            }
        }

        // Strategy 2: Reset global timer resolution to default (all Windows)
        // This is a fallback — less surgical but still effective
        if (_originalResolution < DEFAULT_RESOLUTION)
        {
            NativeMethods.NtSetTimerResolution(DEFAULT_RESOLUTION, true, out _);
        }

        _isActive = true;
        sw.Stop();

        var msg = isWin11
            ? $"Ignored timer on {ignored} processes, reset global to 15.6ms"
            : "Reset global timer resolution to 15.6ms";

        return ApplyResult.Ok(Id, msg, ignored, failed, skipped, sw.Elapsed);
    }

    public void Revert(DomainSnapshot baseline)
    {
        // Restore per-process timer resolution allowances
        foreach (var pid in _processesIgnored)
        {
            var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_SET_INFORMATION, false, pid);
            if (handle == IntPtr.Zero) continue;
            try { NativeMethods.SetProcessTimerResolutionIgnore(handle, false); }
            finally { NativeMethods.CloseHandle(handle); }
        }
        _processesIgnored.Clear();

        // Restore original global timer resolution
        var original = baseline.Get<uint>("currentResolution");
        if (original > 0 && original != DEFAULT_RESOLUTION)
        {
            NativeMethods.NtSetTimerResolution(original, true, out _);
        }
        else
        {
            // Disable our override
            NativeMethods.NtSetTimerResolution(DEFAULT_RESOLUTION, false, out _);
        }

        _isActive = false;
    }

    public DomainStatus GetStatus()
    {
        NativeMethods.NtQueryTimerResolution(out _, out _, out var current);
        var currentMs = current / 10000.0;

        return new DomainStatus
        {
            DomainId = Id,
            DisplayName = DisplayName,
            IsSupported = IsSupported,
            IsActive = _isActive,
            Summary = _isActive
                ? $"Timer at {currentMs:F1}ms, {_processesIgnored.Count} processes clamped"
                : $"Timer at {currentMs:F1}ms",
        };
    }

    public void Dispose() { }
}
