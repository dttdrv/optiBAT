using System.Diagnostics;
using OptiBat.Models;
using OptiBat.Native;

namespace OptiBat.Domains;

/// <summary>
/// Marks background processes with EcoQoS (efficiency mode).
/// On modern CPUs with E-cores, this schedules background work on efficiency cores
/// at lower frequency. Foreground app is never touched.
/// </summary>
public sealed class EcoQosDomain : IOptimizationDomain
{
    private readonly Settings _settings;
    private bool _isActive;

    // PIDs we throttled, so we can un-throttle exactly those
    private readonly HashSet<uint> _throttledPids = [];

    public string Id => "ecoqos";
    public string DisplayName => "Process Power Throttling";
    public bool IsSupported => Environment.OSVersion.Version >= new Version(10, 0, 16299); // Win10 1709+
    public bool IsActive => _isActive;

    public EcoQosDomain(Settings settings)
    {
        _settings = settings;
    }

    public DomainSnapshot CaptureBaseline()
    {
        var snapshot = new DomainSnapshot { DomainId = Id };
        // We don't need to capture per-process EcoQoS state because
        // reverting simply disables EcoQoS on the PIDs we throttled.
        // But we do store the foreground PID to exclude it.
        snapshot.Set("captured", true);
        return snapshot;
    }

    public ApplyResult Apply(DomainSnapshot baseline)
    {
        var sw = Stopwatch.StartNew();
        int throttled = 0, failed = 0, skipped = 0;
        var foregroundPid = NativeMethods.GetForegroundProcessId();
        var selfPid = Environment.ProcessId;
        var exclusions = new HashSet<string>(_settings.EcoQosExcludedProcesses,
            StringComparer.OrdinalIgnoreCase);

        _throttledPids.Clear();

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var pid = (uint)proc.Id;

                // Never throttle: self, foreground, system, excluded
                if (pid == selfPid || pid == foregroundPid || pid <= 4)
                {
                    skipped++;
                    continue;
                }

                var name = proc.ProcessName;
                if (exclusions.Contains(name))
                {
                    skipped++;
                    continue;
                }

                var handle = NativeMethods.OpenProcess(
                    NativeMethods.PROCESS_SET_INFORMATION | NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION,
                    false, pid);

                if (handle == IntPtr.Zero)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    if (NativeMethods.SetProcessEcoQoS(handle, true))
                    {
                        _throttledPids.Add(pid);
                        throttled++;
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
            catch
            {
                skipped++;
            }
            finally
            {
                proc.Dispose();
            }
        }

        _isActive = throttled > 0;
        sw.Stop();

        // Persist throttled PIDs into the snapshot for crash recovery
        baseline.Set("throttledPids", _throttledPids.ToList());

        return ApplyResult.Ok(Id,
            $"Throttled {throttled} processes (skipped {skipped}, failed {failed})",
            throttled, failed, skipped, sw.Elapsed);
    }

    public void Revert(DomainSnapshot baseline)
    {
        // On crash recovery, _throttledPids is empty — restore from snapshot
        var pidsToRevert = _throttledPids.Count > 0
            ? _throttledPids
            : new HashSet<uint>(baseline.Get<List<uint>>("throttledPids") ?? []);

        foreach (var pid in pidsToRevert)
        {
            var handle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_SET_INFORMATION, false, pid);
            if (handle == IntPtr.Zero) continue;

            try
            {
                NativeMethods.SetProcessEcoQoS(handle, false);
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }

        _throttledPids.Clear();
        _isActive = false;
    }

    public DomainStatus GetStatus() => new()
    {
        DomainId = Id,
        DisplayName = DisplayName,
        IsSupported = IsSupported,
        IsActive = _isActive,
        Summary = _isActive ? $"{_throttledPids.Count} processes throttled" : "Inactive",
        Details = _isActive
            ? [$"Excluded: {string.Join(", ", _settings.EcoQosExcludedProcesses.Take(5))}..."]
            : []
    };

    public void Dispose() { }
}
