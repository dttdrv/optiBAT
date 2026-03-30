using System.Diagnostics;
using OptiBat.Models;
using OptiBat.Native;

namespace OptiBat.Domains;

/// <summary>
/// Reduces disk wake-ups by lowering disk idle timeout and enabling
/// aggressive AHCI link power management (HIPM/DIPM).
/// This lets the disk spin down / enter low-power state sooner.
/// </summary>
public sealed class DiskIoCoalescingDomain : IOptimizationDomain
{
    private readonly Settings _settings;
    private bool _isActive;

    public string Id => "disk-coalescing";
    public string DisplayName => "Disk I/O Power Saving";
    public bool IsSupported => true;
    public bool IsActive => _isActive;

    public DiskIoCoalescingDomain(Settings settings)
    {
        _settings = settings;
    }

    public DomainSnapshot CaptureBaseline()
    {
        var snapshot = new DomainSnapshot { DomainId = Id };
        var scheme = NativeMethods.GetActiveScheme();

        if (scheme == Guid.Empty)
        {
            snapshot.Set("schemeValid", false);
            return snapshot;
        }

        snapshot.Set("schemeValid", true);
        snapshot.Set("schemeGuid", scheme.ToString());

        // Disk idle timeout (seconds)
        var idleTimeout = NativeMethods.ReadDCValue(scheme,
            NativeMethods.GUID_DISK_SUBGROUP,
            NativeMethods.GUID_DISK_IDLE_TIMEOUT);

        // AHCI link power management
        // 0=Active, 1=HIPM, 2=HIPM+DIPM (most aggressive), 3=Lowest
        var ahciLinkPower = NativeMethods.ReadDCValue(scheme,
            NativeMethods.GUID_DISK_SUBGROUP,
            NativeMethods.GUID_DISK_AHCI_LINK_POWER);

        snapshot.Set("diskIdleTimeout", idleTimeout ?? 1200u); // Default: 20 min
        snapshot.Set("ahciLinkPower", ahciLinkPower ?? 0u);

        return snapshot;
    }

    public ApplyResult Apply(DomainSnapshot baseline)
    {
        var sw = Stopwatch.StartNew();

        if (!baseline.Get<bool>("schemeValid"))
            return ApplyResult.Fail(Id, "Could not read active power scheme");

        var schemeStr = baseline.Get<string>("schemeGuid");
        if (string.IsNullOrEmpty(schemeStr) || !Guid.TryParse(schemeStr, out var scheme))
            return ApplyResult.Fail(Id, "Invalid power scheme GUID");

        int applied = 0, failed = 0;

        // Set disk idle timeout (shorter = spins down sooner)
        var targetTimeout = (uint)_settings.DiskIdleTimeoutSeconds;
        if (NativeMethods.WriteDCValue(scheme,
            NativeMethods.GUID_DISK_SUBGROUP,
            NativeMethods.GUID_DISK_IDLE_TIMEOUT,
            targetTimeout))
            applied++;
        else
            failed++;

        // Enable aggressive AHCI link power management (HIPM + DIPM)
        if (NativeMethods.WriteDCValue(scheme,
            NativeMethods.GUID_DISK_SUBGROUP,
            NativeMethods.GUID_DISK_AHCI_LINK_POWER,
            2u)) // 2 = HIPM + DIPM
            applied++;
        else
            failed++;

        // Apply changes
        NativeMethods.PowerSetActiveScheme(IntPtr.Zero, scheme);

        _isActive = applied > 0;
        sw.Stop();

        return ApplyResult.Ok(Id,
            $"Disk idle: {targetTimeout}s, AHCI: HIPM+DIPM",
            applied, failed, duration: sw.Elapsed);
    }

    public void Revert(DomainSnapshot baseline)
    {
        var schemeStr = baseline.Get<string>("schemeGuid");
        if (string.IsNullOrEmpty(schemeStr) || !Guid.TryParse(schemeStr, out var scheme))
        {
            _isActive = false;
            return;
        }

        var origTimeout = baseline.Get<uint>("diskIdleTimeout");
        var origAhci = baseline.Get<uint>("ahciLinkPower");

        NativeMethods.WriteDCValue(scheme,
            NativeMethods.GUID_DISK_SUBGROUP,
            NativeMethods.GUID_DISK_IDLE_TIMEOUT, origTimeout);

        NativeMethods.WriteDCValue(scheme,
            NativeMethods.GUID_DISK_SUBGROUP,
            NativeMethods.GUID_DISK_AHCI_LINK_POWER, origAhci);

        NativeMethods.PowerSetActiveScheme(IntPtr.Zero, scheme);
        _isActive = false;
    }

    public DomainStatus GetStatus() => new()
    {
        DomainId = Id,
        DisplayName = DisplayName,
        IsSupported = IsSupported,
        IsActive = _isActive,
        Summary = _isActive
            ? $"Idle: {_settings.DiskIdleTimeoutSeconds}s, AHCI: aggressive"
            : "Inactive",
    };

    public void Dispose() { }
}
