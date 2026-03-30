using OptiBat.Models;

namespace OptiBat.Tests;

public sealed class BatteryInfoTests
{
    [Fact]
    public void Watts_Returns_Absolute_Value()
    {
        var info = new BatteryInfo { DrainRateMilliwatts = -15000 };
        Assert.Equal(15.0, info.Watts);
    }

    [Fact]
    public void StatusText_On_AC_Charging()
    {
        var info = new BatteryInfo { IsOnAC = true, IsCharging = true };
        Assert.Equal("Charging", info.StatusText);
    }

    [Fact]
    public void StatusText_On_AC_Not_Charging()
    {
        var info = new BatteryInfo { IsOnAC = true, IsCharging = false };
        Assert.Equal("Plugged in", info.StatusText);
    }

    [Fact]
    public void StatusText_On_Battery()
    {
        var info = new BatteryInfo { IsOnAC = false };
        Assert.Equal("On battery", info.StatusText);
    }
}
