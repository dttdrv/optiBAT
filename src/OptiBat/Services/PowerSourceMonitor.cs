using System.Windows.Threading;
using Microsoft.Win32;
using OptiBat.Models;

namespace OptiBat.Services;

/// <summary>
/// Monitors AC/battery power transitions and fires events.
/// Debounces flickers from loose USB-C connections.
/// </summary>
public sealed class PowerSourceMonitor : IDisposable
{
    private readonly DispatcherTimer _pollTimer;
    private readonly int _debounceSeconds;
    private bool _lastIsOnAC;
    private DateTime? _pendingTransitionTime;
    private bool? _pendingState;
    private bool _disposed;

    /// <summary>
    /// Fires when power source definitively changes (after debounce).
    /// True = now on AC, False = now on battery.
    /// </summary>
    public event Action<bool>? PowerSourceChanged;

    /// <summary>
    /// Fires every poll tick with updated battery info.
    /// </summary>
    public event Action<BatteryInfo>? BatteryInfoUpdated;

    public bool IsOnAC => _lastIsOnAC;

    public PowerSourceMonitor(int debounceSeconds = 2)
    {
        _debounceSeconds = debounceSeconds;
        _lastIsOnAC = BatteryInfoService.IsOnACPower();

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _pollTimer.Tick += OnPollTick;

        // Also listen to system events for immediate detection
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Start()
    {
        _pollTimer.Start();
    }

    public void Stop()
    {
        _pollTimer.Stop();
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        if (_disposed) return;

        var info = BatteryInfoService.GetBatteryInfo();
        BatteryInfoUpdated?.Invoke(info);

        var currentIsOnAC = info.IsOnAC;
        CheckTransition(currentIsOnAC);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.Mode != PowerModes.StatusChange) return;

        var currentIsOnAC = BatteryInfoService.IsOnACPower();
        CheckTransition(currentIsOnAC);
    }

    private void CheckTransition(bool currentIsOnAC)
    {
        if (currentIsOnAC == _lastIsOnAC)
        {
            // Stable — cancel any pending transition
            _pendingTransitionTime = null;
            _pendingState = null;
            return;
        }

        if (_pendingState == null || _pendingState != currentIsOnAC)
        {
            // New transition detected — start debounce
            _pendingTransitionTime = DateTime.UtcNow;
            _pendingState = currentIsOnAC;
            return;
        }

        // Same pending state — check if debounce period elapsed
        if (_pendingTransitionTime.HasValue &&
            (DateTime.UtcNow - _pendingTransitionTime.Value).TotalSeconds >= _debounceSeconds)
        {
            _lastIsOnAC = currentIsOnAC;
            _pendingTransitionTime = null;
            _pendingState = null;
            PowerSourceChanged?.Invoke(currentIsOnAC);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollTimer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
