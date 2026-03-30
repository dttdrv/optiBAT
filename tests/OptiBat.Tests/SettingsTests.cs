using OptiBat.Models;

namespace OptiBat.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void Default_Settings_Have_Sane_Values()
    {
        var settings = new Settings();

        Assert.True(settings.AutoOptimizeOnBattery);
        Assert.True(settings.EcoQosEnabled);
        Assert.True(settings.TimerResolutionEnabled);
        Assert.True(settings.BackgroundServicesEnabled);
        Assert.True(settings.UsbSuspendEnabled);
        Assert.True(settings.NetworkPowerEnabled);
        Assert.True(settings.GpuPowerEnabled);
        Assert.True(settings.CpuParkingEnabled);
        Assert.True(settings.DiskCoalescingEnabled);
        Assert.Equal(2, settings.DebouncePowerChangeSeconds);
        Assert.Equal(960, settings.WindowWidth);
        Assert.Equal(660, settings.WindowHeight);
        Assert.Equal("System", settings.ThemeMode);
    }

    [Fact]
    public void Default_ExcludedProcesses_Contains_System_Processes()
    {
        var settings = new Settings();

        Assert.Contains("System", settings.EcoQosExcludedProcesses);
        Assert.Contains("csrss", settings.EcoQosExcludedProcesses);
        Assert.Contains("dwm", settings.EcoQosExcludedProcesses);
        Assert.Contains("svchost", settings.EcoQosExcludedProcesses);
    }

    [Fact]
    public void Default_ServicesToThrottle_Contains_Expected_Services()
    {
        var settings = new Settings();

        Assert.Contains("WSearch", settings.ServicesToThrottle);
        Assert.Contains("SysMain", settings.ServicesToThrottle);
        Assert.Contains("DiagTrack", settings.ServicesToThrottle);
        Assert.Contains("wuauserv", settings.ServicesToThrottle);
    }

    [Fact]
    public void Load_Returns_Default_When_No_File_Exists()
    {
        // Settings.Load() reads from AppData, which may or may not have a file
        // but should never throw
        var settings = Settings.Load();
        Assert.NotNull(settings);
    }
}
