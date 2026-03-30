using OptiBat.Models;

namespace OptiBat.Domains;

/// <summary>
/// Contract for a battery optimization strategy.
/// Each domain is independently testable, snapshotable, and revertible.
/// </summary>
public interface IOptimizationDomain : IDisposable
{
    /// <summary>Unique identifier (e.g., "ecoqos", "timer-resolution").</summary>
    string Id { get; }

    /// <summary>Human-readable name for UI display.</summary>
    string DisplayName { get; }

    /// <summary>Whether this optimization is supported on the current hardware/OS.</summary>
    bool IsSupported { get; }

    /// <summary>Whether this optimization is currently applied.</summary>
    bool IsActive { get; }

    /// <summary>Capture the current system state before optimization.</summary>
    DomainSnapshot CaptureBaseline();

    /// <summary>Apply the optimization using the baseline for reference.</summary>
    ApplyResult Apply(DomainSnapshot baseline);

    /// <summary>Revert to the exact state captured in the snapshot.</summary>
    void Revert(DomainSnapshot baseline);

    /// <summary>Get live status for UI binding.</summary>
    DomainStatus GetStatus();
}
