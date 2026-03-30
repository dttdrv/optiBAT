using System.Diagnostics;
using System.Runtime.InteropServices;
using OptiBat.Models;
using OptiBat.Native;

namespace OptiBat.Domains;

/// <summary>
/// Stops non-essential Windows services while on battery.
/// Targets: indexing, telemetry, prefetch, update services.
/// Each service's original state is captured and restored exactly.
/// </summary>
public sealed class BackgroundServiceDomain : IOptimizationDomain
{
    private readonly Settings _settings;
    private bool _isActive;
    private int _servicesStopped;

    public string Id => "background-services";
    public string DisplayName => "Background Services";
    public bool IsSupported => true;
    public bool IsActive => _isActive;

    public BackgroundServiceDomain(Settings settings)
    {
        _settings = settings;
    }

    public DomainSnapshot CaptureBaseline()
    {
        var snapshot = new DomainSnapshot { DomainId = Id };
        var serviceStates = new Dictionary<string, ServiceState>();

        var scManager = NativeMethods.OpenSCManagerW(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scManager == IntPtr.Zero)
        {
            snapshot.Set("services", serviceStates);
            return snapshot;
        }

        try
        {
            foreach (var serviceName in _settings.ServicesToThrottle)
            {
                var hService = NativeMethods.OpenServiceW(scManager, serviceName,
                    NativeMethods.SERVICE_QUERY_CONFIG | NativeMethods.SERVICE_QUERY_STATUS);

                if (hService == IntPtr.Zero) continue;

                try
                {
                    // Get current status
                    if (!NativeMethods.QueryServiceStatus(hService, out var status))
                        continue;

                    // Get start type — skip this service if we can't determine it
                    var startType = GetServiceStartType(hService);
                    if (startType == uint.MaxValue) continue;

                    serviceStates[serviceName] = new ServiceState
                    {
                        StartType = startType,
                        WasRunning = status.dwCurrentState == NativeMethods.SERVICE_RUNNING
                    };
                }
                finally
                {
                    NativeMethods.CloseServiceHandle(hService);
                }
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scManager);
        }

        snapshot.Set("services", serviceStates);
        return snapshot;
    }

    public ApplyResult Apply(DomainSnapshot baseline)
    {
        var sw = Stopwatch.StartNew();
        int stopped = 0, failed = 0, skipped = 0;

        var scManager = NativeMethods.OpenSCManagerW(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scManager == IntPtr.Zero)
            return ApplyResult.Fail(Id, "Cannot open Service Control Manager (need admin)");

        try
        {
            foreach (var serviceName in _settings.ServicesToThrottle)
            {
                var hService = NativeMethods.OpenServiceW(scManager, serviceName,
                    NativeMethods.SERVICE_ALL_ACCESS);

                if (hService == IntPtr.Zero) { skipped++; continue; }

                try
                {
                    // Check current state
                    if (!NativeMethods.QueryServiceStatus(hService, out var status))
                    { failed++; continue; }

                    // Stop if running
                    if (status.dwCurrentState == NativeMethods.SERVICE_RUNNING)
                    {
                        if (NativeMethods.ControlService(hService,
                            NativeMethods.SERVICE_CONTROL_STOP, out _))
                        {
                            stopped++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    else
                    {
                        skipped++;
                    }

                    // Set to demand-start (manual) so it doesn't auto-restart
                    NativeMethods.ChangeServiceConfigW(hService,
                        NativeMethods.SERVICE_NO_CHANGE,
                        NativeMethods.SERVICE_DEMAND_START,
                        NativeMethods.SERVICE_NO_CHANGE,
                        null, null, IntPtr.Zero, null, null, null, null);
                }
                finally
                {
                    NativeMethods.CloseServiceHandle(hService);
                }
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scManager);
        }

        _servicesStopped = stopped;
        _isActive = stopped > 0; // Only active if we actually stopped something
        sw.Stop();

        return ApplyResult.Ok(Id,
            $"Stopped {stopped} services (skipped {skipped}, failed {failed})",
            stopped, failed, skipped, sw.Elapsed);
    }

    public void Revert(DomainSnapshot baseline)
    {
        var services = baseline.Get<Dictionary<string, ServiceState>>("services");
        if (services == null || services.Count == 0)
        {
            _isActive = false;
            return;
        }

        var scManager = NativeMethods.OpenSCManagerW(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scManager == IntPtr.Zero) return;

        try
        {
            foreach (var (serviceName, state) in services)
            {
                var hService = NativeMethods.OpenServiceW(scManager, serviceName,
                    NativeMethods.SERVICE_ALL_ACCESS);
                if (hService == IntPtr.Zero) continue;

                try
                {
                    // Restore original start type
                    NativeMethods.ChangeServiceConfigW(hService,
                        NativeMethods.SERVICE_NO_CHANGE,
                        state.StartType,
                        NativeMethods.SERVICE_NO_CHANGE,
                        null, null, IntPtr.Zero, null, null, null, null);

                    // Restart if it was running before
                    if (state.WasRunning)
                    {
                        NativeMethods.StartServiceW(hService, 0, IntPtr.Zero);
                    }
                }
                finally
                {
                    NativeMethods.CloseServiceHandle(hService);
                }
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scManager);
        }

        _isActive = false;
        _servicesStopped = 0;
    }

    public DomainStatus GetStatus() => new()
    {
        DomainId = Id,
        DisplayName = DisplayName,
        IsSupported = IsSupported,
        IsActive = _isActive,
        Summary = _isActive
            ? $"{_servicesStopped} services stopped"
            : "Inactive",
        Details = _settings.ServicesToThrottle.ToArray()
    };

    private static uint GetServiceStartType(IntPtr hService)
    {
        // Query required buffer size
        NativeMethods.QueryServiceConfigW(hService, IntPtr.Zero, 0, out var bytesNeeded);
        if (bytesNeeded == 0) return uint.MaxValue; // Sentinel: query failed

        var buffer = Marshal.AllocHGlobal((int)bytesNeeded);
        try
        {
            if (!NativeMethods.QueryServiceConfigW(hService, buffer, bytesNeeded, out _))
                return uint.MaxValue; // Sentinel: query failed

            // dwStartType is at offset 4 (after dwServiceType)
            return (uint)Marshal.ReadInt32(buffer, 4);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose() { }
}

/// <summary>
/// Serializable record of a service's pre-optimization state.
/// </summary>
public sealed class ServiceState
{
    public uint StartType { get; set; }
    public bool WasRunning { get; set; }
}
