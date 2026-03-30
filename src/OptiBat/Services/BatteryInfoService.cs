using OptiBat.Models;
using OptiBat.Native;

namespace OptiBat.Services;

/// <summary>
/// Reads battery state from Windows APIs.
/// Primary: GetSystemPowerStatus (reliable, basic)
/// Secondary: CallNtPowerInformation/SystemBatteryState (detailed, drain rate)
/// </summary>
public static class BatteryInfoService
{
    public static BatteryInfo GetBatteryInfo()
    {
        // Primary: simple power status — always available
        if (!NativeMethods.GetSystemPowerStatus(out var ps))
            return new BatteryInfo { HasBattery = false, IsOnAC = true };

        var hasBattery = ps.BatteryFlag != 128; // 128 = no system battery
        var isOnAC = ps.ACLineStatus == 1;
        var chargePercent = ps.BatteryLifePercent is >= 0 and <= 100
            ? (int)ps.BatteryLifePercent
            : 0;

        // Estimated time from basic API (often unavailable)
        TimeSpan? estimatedTime = ps.BatteryLifeTime is not 0xFFFFFFFF and not 0
            ? TimeSpan.FromSeconds(ps.BatteryLifeTime)
            : null;

        // Secondary: detailed battery state (drain rate, better capacity)
        var bs = NativeMethods.GetBatteryState();
        if (bs.HasValue && bs.Value.BatteryPresent)
        {
            var b = bs.Value;

            // Charge percent from mWh capacity (more precise than basic API)
            if (b.MaxCapacity > 0 && b.RemainingCapacity <= b.MaxCapacity)
                chargePercent = (int)(b.RemainingCapacity * 100.0 / b.MaxCapacity);

            // Drain rate: negative = discharging, positive = charging, 0 = idle/unknown
            var rateMilliwatts = b.Rate;

            // Estimated time from detailed API (prefer over basic)
            if (b.EstimatedTime is not 0xFFFFFFFF and not 0)
                estimatedTime = TimeSpan.FromSeconds(b.EstimatedTime);

            // If no OS estimate but we have rate + capacity, calculate ourselves
            if (estimatedTime == null && rateMilliwatts < 0 && b.RemainingCapacity > 0)
            {
                var hoursLeft = b.RemainingCapacity / (double)Math.Abs(rateMilliwatts);
                if (hoursLeft is > 0 and < 48) // Sanity: under 48 hours
                    estimatedTime = TimeSpan.FromHours(hoursLeft);
            }

            return new BatteryInfo
            {
                HasBattery = true,
                IsOnAC = isOnAC,
                IsCharging = b.Charging,
                IsDischarging = b.Discharging,
                ChargePercent = chargePercent,
                DrainRateMilliwatts = rateMilliwatts,
                EstimatedTimeRemaining = estimatedTime,
            };
        }

        // Fallback: basic API only
        return new BatteryInfo
        {
            HasBattery = hasBattery,
            IsOnAC = isOnAC,
            IsCharging = (ps.BatteryFlag & 8) != 0,
            IsDischarging = !isOnAC && hasBattery,
            ChargePercent = chargePercent,
            DrainRateMilliwatts = 0,
            EstimatedTimeRemaining = estimatedTime,
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
