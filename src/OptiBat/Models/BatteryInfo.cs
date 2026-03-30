namespace OptiBat.Models;

/// <summary>
/// Snapshot of current battery state.
/// </summary>
public sealed record BatteryInfo
{
    public bool HasBattery { get; init; }
    public bool IsOnAC { get; init; }
    public bool IsCharging { get; init; }
    public bool IsDischarging { get; init; }
    public int ChargePercent { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Current drain rate in milliwatts. Negative = discharging.
    /// </summary>
    public int DrainRateMilliwatts { get; init; }

    /// <summary>
    /// Estimated wattage (absolute value of drain rate).
    /// </summary>
    public double Watts => Math.Abs(DrainRateMilliwatts) / 1000.0;

    public string StatusText => IsOnAC
        ? IsCharging ? "Charging" : "Plugged in"
        : "On battery";
}
