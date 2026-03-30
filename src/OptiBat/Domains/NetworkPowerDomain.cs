using System.Diagnostics;
using Microsoft.Win32;
using OptiBat.Models;

namespace OptiBat.Domains;

/// <summary>
/// Enables power management on network adapters.
/// Disables wake-on-LAN and enables power saving on Wi-Fi/Ethernet when on battery.
/// Uses registry-based approach for reliability across adapter types.
/// </summary>
public sealed class NetworkPowerDomain : IOptimizationDomain
{
    private bool _isActive;
    private int _adaptersModified;

    // Network adapter class GUID
    private const string NET_CLASS_KEY = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    public string Id => "network-power";
    public string DisplayName => "Network Power Saving";
    public bool IsSupported => true;
    public bool IsActive => _isActive;

    public DomainSnapshot CaptureBaseline()
    {
        var snapshot = new DomainSnapshot { DomainId = Id };
        var adapterStates = new Dictionary<string, AdapterPowerState>();

        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(NET_CLASS_KEY);
            if (classKey == null) { snapshot.Set("adapters", adapterStates); return snapshot; }

            foreach (var subKeyName in classKey.GetSubKeyNames())
            {
                if (!int.TryParse(subKeyName, out _)) continue; // Skip non-numeric keys

                try
                {
                    using var adapterKey = classKey.OpenSubKey(subKeyName);
                    if (adapterKey == null) continue;

                    // Only real adapters have DriverDesc
                    var driverDesc = adapterKey.GetValue("DriverDesc") as string;
                    if (string.IsNullOrEmpty(driverDesc)) continue;

                    // Skip virtual adapters
                    var componentId = adapterKey.GetValue("ComponentId") as string ?? "";
                    if (componentId.Contains("virtual", StringComparison.OrdinalIgnoreCase) ||
                        componentId.Contains("vmware", StringComparison.OrdinalIgnoreCase) ||
                        componentId.Contains("vbox", StringComparison.OrdinalIgnoreCase))
                        continue;

                    adapterStates[subKeyName] = new AdapterPowerState
                    {
                        DriverDesc = driverDesc,
                        PnPCapabilities = adapterKey.GetValue("PnPCapabilities") as int? ?? 0,
                        WakeOnMagicPacket = adapterKey.GetValue("*WakeOnMagicPacket") as string ?? "1",
                        WakeOnPattern = adapterKey.GetValue("*WakeOnPattern") as string ?? "1",
                        EEE = adapterKey.GetValue("*EEE") as string,
                    };
                }
                catch { }
            }
        }
        catch { }

        snapshot.Set("adapters", adapterStates);
        return snapshot;
    }

    public ApplyResult Apply(DomainSnapshot baseline)
    {
        var sw = Stopwatch.StartNew();
        int modified = 0, failed = 0, skipped = 0;

        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(NET_CLASS_KEY);
            if (classKey == null)
                return ApplyResult.Fail(Id, "Cannot access network adapter registry");

            var adapters = baseline.Get<Dictionary<string, AdapterPowerState>>("adapters");
            if (adapters == null)
                return ApplyResult.Fail(Id, "No adapter baseline captured");

            foreach (var (subKeyName, _) in adapters)
            {
                try
                {
                    using var adapterKey = Registry.LocalMachine.OpenSubKey(
                        $@"{NET_CLASS_KEY}\{subKeyName}", writable: true);
                    if (adapterKey == null) { skipped++; continue; }

                    // Enable power management: PnPCapabilities bit 4 (0x10) = allow power off
                    // Setting to 0 enables all power management features
                    adapterKey.SetValue("PnPCapabilities", 0, RegistryValueKind.DWord);

                    // Disable wake-on-LAN (saves power, prevents random wake-ups)
                    adapterKey.SetValue("*WakeOnMagicPacket", "0", RegistryValueKind.String);
                    adapterKey.SetValue("*WakeOnPattern", "0", RegistryValueKind.String);

                    // Enable Energy Efficient Ethernet if supported
                    if (adapterKey.GetValue("*EEE") != null)
                    {
                        adapterKey.SetValue("*EEE", "1", RegistryValueKind.String);
                    }

                    modified++;
                }
                catch
                {
                    failed++;
                }
            }
        }
        catch (Exception ex)
        {
            return ApplyResult.Fail(Id, $"Registry error: {ex.Message}");
        }

        _adaptersModified = modified;
        _isActive = modified > 0;
        sw.Stop();

        return ApplyResult.Ok(Id,
            $"Power saving enabled on {modified} adapters",
            modified, failed, skipped, sw.Elapsed);
    }

    public void Revert(DomainSnapshot baseline)
    {
        var adapters = baseline.Get<Dictionary<string, AdapterPowerState>>("adapters");
        if (adapters == null) { _isActive = false; return; }

        foreach (var (subKeyName, state) in adapters)
        {
            try
            {
                using var adapterKey = Registry.LocalMachine.OpenSubKey(
                    $@"{NET_CLASS_KEY}\{subKeyName}", writable: true);
                if (adapterKey == null) continue;

                adapterKey.SetValue("PnPCapabilities", state.PnPCapabilities, RegistryValueKind.DWord);
                adapterKey.SetValue("*WakeOnMagicPacket", state.WakeOnMagicPacket, RegistryValueKind.String);
                adapterKey.SetValue("*WakeOnPattern", state.WakeOnPattern, RegistryValueKind.String);

                if (state.EEE != null)
                    adapterKey.SetValue("*EEE", state.EEE, RegistryValueKind.String);
            }
            catch { }
        }

        _isActive = false;
        _adaptersModified = 0;
    }

    public DomainStatus GetStatus() => new()
    {
        DomainId = Id,
        DisplayName = DisplayName,
        IsSupported = IsSupported,
        IsActive = _isActive,
        Summary = _isActive ? $"{_adaptersModified} adapters optimized" : "Inactive",
    };

    public void Dispose() { }
}

public sealed class AdapterPowerState
{
    public string DriverDesc { get; set; } = "";
    public int PnPCapabilities { get; set; }
    public string WakeOnMagicPacket { get; set; } = "1";
    public string WakeOnPattern { get; set; } = "1";
    public string? EEE { get; set; }
}
