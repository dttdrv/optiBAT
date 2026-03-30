using OptiBat.Domains;
using OptiBat.Models;

namespace OptiBat.Services;

/// <summary>
/// Central orchestrator: manages the lifecycle of all optimization domains.
/// Iterates enabled domains in order, snapshots → applies → stores.
/// Reverts in reverse order for safety.
/// </summary>
public sealed class OptimizationEngine : IDisposable
{
    private readonly List<IOptimizationDomain> _domains;
    private readonly SnapshotStore _snapshotStore;
    private readonly Settings _settings;
    private int _optimizing; // Interlocked guard
    private bool _disposed;

    public event Action<EngineEvent>? EventOccurred;

    public bool IsOptimizing => Interlocked.CompareExchange(ref _optimizing, 0, 0) != 0;
    public bool IsActive => _domains.Any(d => d.IsActive);
    public IReadOnlyList<IOptimizationDomain> Domains => _domains.AsReadOnly();

    public OptimizationEngine(Settings settings, SnapshotStore snapshotStore)
    {
        _settings = settings;
        _snapshotStore = snapshotStore;

        // Register all domains in priority order
        _domains =
        [
            new EcoQosDomain(settings),
            new TimerResolutionDomain(settings),
            new BackgroundServiceDomain(settings),
            new UsbSuspendDomain(),
            new NetworkPowerDomain(),
            new GpuPowerDomain(),
            new CpuParkingDomain(settings),
            new DiskIoCoalescingDomain(settings),
        ];
    }

    /// <summary>
    /// Activate all enabled and supported domains. Called when switching to battery.
    /// </summary>
    public EngineResult ActivateAll()
    {
        if (Interlocked.Exchange(ref _optimizing, 1) != 0)
            return new EngineResult { Message = "Optimization already in progress" };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = new List<ApplyResult>();

        try
        {
            foreach (var domain in _domains)
            {
                if (!IsDomainEnabled(domain.Id) || !domain.IsSupported || domain.IsActive)
                    continue;

                try
                {
                    Emit($"Capturing baseline for {domain.DisplayName}...");
                    var snapshot = domain.CaptureBaseline();
                    _snapshotStore.Store(snapshot);

                    Emit($"Applying {domain.DisplayName}...");
                    var result = domain.Apply(snapshot);
                    results.Add(result);

                    if (result.Success)
                        Emit($"{domain.DisplayName}: {result.Message}");
                    else
                    {
                        Emit($"{domain.DisplayName} failed: {result.Message}");
                        _snapshotStore.Remove(domain.Id); // Don't keep useless snapshot
                    }
                }
                catch (Exception ex)
                {
                    results.Add(ApplyResult.Fail(domain.Id, ex.Message));
                    Emit($"{domain.DisplayName} error: {ex.Message}");
                    _snapshotStore.Remove(domain.Id);
                }
            }

            sw.Stop();
            return new EngineResult
            {
                Success = results.Any(r => r.Success),
                Results = results,
                Duration = sw.Elapsed,
                Message = $"Activated {results.Count(r => r.Success)}/{results.Count} domains"
            };
        }
        finally
        {
            Interlocked.Exchange(ref _optimizing, 0);
        }
    }

    /// <summary>
    /// Revert all active domains to their pre-optimization state.
    /// Called when switching to AC power.
    /// </summary>
    public EngineResult RevertAll()
    {
        if (Interlocked.Exchange(ref _optimizing, 1) != 0)
            return new EngineResult { Message = "Operation already in progress" };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var reverted = 0;
        var failed = 0;

        try
        {
            // Revert in reverse order (last applied = first reverted)
            foreach (var domain in _domains.AsEnumerable().Reverse())
            {
                if (!domain.IsActive) continue;

                var snapshot = _snapshotStore.Get(domain.Id);
                if (snapshot == null)
                {
                    Emit($"No snapshot for {domain.DisplayName}, skipping revert");
                    continue;
                }

                try
                {
                    Emit($"Reverting {domain.DisplayName}...");
                    domain.Revert(snapshot);
                    _snapshotStore.Remove(domain.Id);
                    reverted++;
                    Emit($"{domain.DisplayName} reverted successfully");
                }
                catch (Exception ex)
                {
                    failed++;
                    Emit($"{domain.DisplayName} revert failed: {ex.Message}");
                }
            }

            sw.Stop();
            return new EngineResult
            {
                Success = failed == 0,
                Duration = sw.Elapsed,
                Message = $"Reverted {reverted} domains ({failed} failed)"
            };
        }
        finally
        {
            Interlocked.Exchange(ref _optimizing, 0);
        }
    }

    /// <summary>
    /// Attempt crash recovery: revert any domains that have stored snapshots.
    /// Called on startup if snapshots exist from a previous session.
    /// </summary>
    public void TryCrashRecovery()
    {
        if (!_snapshotStore.HasSnapshots) return;

        Emit("Detected snapshots from previous session — attempting recovery...");
        var toRemove = new List<string>();

        foreach (var snapshot in _snapshotStore.GetAll())
        {
            var domain = _domains.FirstOrDefault(d => d.Id == snapshot.DomainId);
            if (domain == null)
            {
                toRemove.Add(snapshot.DomainId);
                continue;
            }

            try
            {
                domain.Revert(snapshot);
                Emit($"Recovered {domain.DisplayName}");
            }
            catch (Exception ex)
            {
                Emit($"Recovery failed for {domain.DisplayName}: {ex.Message}");
            }

            toRemove.Add(snapshot.DomainId);
        }

        // Batch removal: single lock + single file write
        if (toRemove.Count > 0)
            _snapshotStore.RemoveRange(toRemove);
    }

    /// <summary>
    /// Get live status for all domains.
    /// </summary>
    public List<DomainStatus> GetAllStatuses()
    {
        return _domains.Select(d =>
        {
            try { return d.GetStatus(); }
            catch { return new DomainStatus { DomainId = d.Id, DisplayName = d.DisplayName, Summary = "Error" }; }
        }).ToList();
    }

    private bool IsDomainEnabled(string domainId) => domainId switch
    {
        "ecoqos" => _settings.EcoQosEnabled,
        "timer-resolution" => _settings.TimerResolutionEnabled,
        "background-services" => _settings.BackgroundServicesEnabled,
        "usb-suspend" => _settings.UsbSuspendEnabled,
        "network-power" => _settings.NetworkPowerEnabled,
        "gpu-power" => _settings.GpuPowerEnabled,
        "cpu-parking" => _settings.CpuParkingEnabled,
        "disk-coalescing" => _settings.DiskCoalescingEnabled,
        _ => false
    };

    private void Emit(string message)
    {
        EventOccurred?.Invoke(new EngineEvent
        {
            Timestamp = DateTime.Now,
            Message = message
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var domain in _domains)
        {
            try { domain.Dispose(); } catch { }
        }
    }
}

public sealed class EngineResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public List<ApplyResult> Results { get; init; } = [];
}

public sealed class EngineEvent
{
    public DateTime Timestamp { get; init; }
    public string Message { get; init; } = string.Empty;
}
