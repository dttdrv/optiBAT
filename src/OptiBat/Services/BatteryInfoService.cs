using OptiBat.Models;
using OptiBat.Native;

namespace OptiBat.Services;

/// <summary>
/// Reads battery state from Windows APIs.
/// Uses both GetSystemPowerStatus (simple) and CallNtPowerInformation (detailed).
/// </summary>
public static class BatteryInfoService
{
    public static BatteryInfo GetBatteryInfo()
    {
        // Primary: simple power status
        if (!NativeMethods.GetSystemPowerStatus(out var powerStatus))
        {
            return new BatteryInfo { HasBattery = false, IsOnAC = true };
        }

        var info = new BatteryInfo
        {
            HasBattery = powerStatus.BatteryFlag != 128, // 128 = no battery
            IsOnAC = powerStatus.ACLineStatus == 1,
            ChargePercent = powerStatus.BatteryLifePercent <= 100
                ? powerStatus.BatteryLifePercent
                : 0,
            EstimatedTimeRemaining = powerStatus.BatteryLifeTime != 0xFFFFFFFF
                ? TimeSpan.FromSeconds(powerStatus.BatteryLifeTime)
                : null,
        };

        // Secondary: detailed battery state for drain rate
        var batteryState = NativeMethods.GetBatteryState();
        if (batteryState.HasValue)
        {
            var bs = batteryState.Value;
            return info with
            {
                IsCharging = bs.Charging,
                IsDischarging = bs.Discharging,
                DrainRateMilliwatts = bs.Rate,
                ChargePercent = bs.MaxCapacity > 0
                    ? (int)(bs.RemainingCapacity * 100.0 / bs.MaxCapacity)
                    : info.ChargePercent,
            };
        }

        return info with
        {
            IsCharging = (powerStatus.BatteryFlag & 8) != 0,
            IsDischarging = powerStatus.ACLineStatus == 0 && powerStatus.BatteryFlag != 128,
        };
    }

    /// <summary>
    /// Quick check: is the system on AC power?
    /// </summary>
    public static bool IsOnACPower()
    {
        return NativeMethods.GetSystemPowerStatus(out var s) && s.ACLineStatus == 1;
    }
}
