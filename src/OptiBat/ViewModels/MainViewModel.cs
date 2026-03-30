using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using OptiBat.Models;
using OptiBat.Services;

namespace OptiBat.ViewModels;

/// <summary>
/// Main ViewModel: binds battery info, domain statuses, and engine state to the UI.
/// Follows optiRAM's SetProperty + debounced save pattern.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly Settings _settings;
    private readonly SnapshotStore _snapshotStore;
    private readonly OptimizationEngine _engine;
    private readonly PowerSourceMonitor _powerMonitor;
    private bool _initialized;

    // ── Battery state ────────────────────────────────────────────────
    private int _chargePercent;
    private string _batteryStatus = "Unknown";
    private double _watts;
    private string _estimatedTime = "--:--";
    private bool _isOnAC = true;
    private bool _isOptimizing;
    private bool _isActive;
    private string _statusText = "Starting...";
    private bool _isReadOnlyMode;
    private bool _showResult;
    private string _resultText = "";
    private double _baselineDrainWatts; // Drain rate when optimization first activated
    private double _savedWatts;
    private string _savingsText = "";

    // ── Domain toggles ───────────────────────────────────────────────
    private bool _autoOptimize;
    private bool _ecoQosEnabled;
    private bool _timerResolutionEnabled;
    private bool _backgroundServicesEnabled;
    private bool _usbSuspendEnabled;
    private bool _networkPowerEnabled;
    private bool _gpuPowerEnabled;
    private bool _cpuParkingEnabled;
    private bool _diskCoalescingEnabled;

    // ── Collections ──────────────────────────────────────────────────
    public ObservableCollection<DomainStatus> DomainStatuses { get; } = [];
    public ObservableCollection<string> ActivityLog { get; } = [];

    // ── Commands ─────────────────────────────────────────────────────
    public ICommand OptimizeNowCommand { get; }
    public ICommand RevertNowCommand { get; }
    public ICommand RestartAsAdminCommand { get; }

    // ── Properties ───────────────────────────────────────────────────
    public int ChargePercent { get => _chargePercent; set => SetProperty(ref _chargePercent, value); }
    public string BatteryStatus { get => _batteryStatus; set => SetProperty(ref _batteryStatus, value); }
    public double Watts { get => _watts; set => SetProperty(ref _watts, value); }
    public string EstimatedTime { get => _estimatedTime; set => SetProperty(ref _estimatedTime, value); }
    public bool IsOnAC { get => _isOnAC; set => SetProperty(ref _isOnAC, value); }
    public bool IsOptimizing { get => _isOptimizing; set => SetProperty(ref _isOptimizing, value); }
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public bool IsReadOnlyMode { get => _isReadOnlyMode; set => SetProperty(ref _isReadOnlyMode, value); }
    public bool ShowResult { get => _showResult; set => SetProperty(ref _showResult, value); }
    public double SavedWatts { get => _savedWatts; set => SetProperty(ref _savedWatts, value); }
    public string SavingsText { get => _savingsText; set => SetProperty(ref _savingsText, value); }
    public string ResultText { get => _resultText; set => SetProperty(ref _resultText, value); }

    public bool AutoOptimize
    {
        get => _autoOptimize;
        set
        {
            if (SetProperty(ref _autoOptimize, value))
            {
                _settings.AutoOptimizeOnBattery = value;
                _settings.SaveDebounced();
                StatusText = value ? "Auto-optimize on battery: enabled" : "Auto-optimize: disabled";
            }
        }
    }

    // Per-domain toggle properties
    public bool EcoQosEnabled
    {
        get => _ecoQosEnabled;
        set { if (SetProperty(ref _ecoQosEnabled, value)) { _settings.EcoQosEnabled = value; _settings.SaveDebounced(); } }
    }
    public bool TimerResolutionEnabled
    {
        get => _timerResolutionEnabled;
        set { if (SetProperty(ref _timerResolutionEnabled, value)) { _settings.TimerResolutionEnabled = value; _settings.SaveDebounced(); } }
    }
    public bool BackgroundServicesEnabled
    {
        get => _backgroundServicesEnabled;
        set { if (SetProperty(ref _backgroundServicesEnabled, value)) { _settings.BackgroundServicesEnabled = value; _settings.SaveDebounced(); } }
    }
    public bool UsbSuspendEnabled
    {
        get => _usbSuspendEnabled;
        set { if (SetProperty(ref _usbSuspendEnabled, value)) { _settings.UsbSuspendEnabled = value; _settings.SaveDebounced(); } }
    }
    public bool NetworkPowerEnabled
    {
        get => _networkPowerEnabled;
        set { if (SetProperty(ref _networkPowerEnabled, value)) { _settings.NetworkPowerEnabled = value; _settings.SaveDebounced(); } }
    }
    public bool GpuPowerEnabled
    {
        get => _gpuPowerEnabled;
        set { if (SetProperty(ref _gpuPowerEnabled, value)) { _settings.GpuPowerEnabled = value; _settings.SaveDebounced(); } }
    }
    public bool CpuParkingEnabled
    {
        get => _cpuParkingEnabled;
        set { if (SetProperty(ref _cpuParkingEnabled, value)) { _settings.CpuParkingEnabled = value; _settings.SaveDebounced(); } }
    }
    public bool DiskCoalescingEnabled
    {
        get => _diskCoalescingEnabled;
        set { if (SetProperty(ref _diskCoalescingEnabled, value)) { _settings.DiskCoalescingEnabled = value; _settings.SaveDebounced(); } }
    }

    public Settings Settings => _settings;
    public OptimizationEngine Engine => _engine;
    public PowerSourceMonitor PowerMonitor => _powerMonitor;

    public MainViewModel()
    {
        _settings = Models.Settings.Load();
        _snapshotStore = new SnapshotStore(Models.Settings.GetSnapshotPath());
        _engine = new OptimizationEngine(_settings, _snapshotStore);
        _powerMonitor = new PowerSourceMonitor(_settings.DebouncePowerChangeSeconds);

        // Sync toggle state from settings
        _autoOptimize = _settings.AutoOptimizeOnBattery;
        _ecoQosEnabled = _settings.EcoQosEnabled;
        _timerResolutionEnabled = _settings.TimerResolutionEnabled;
        _backgroundServicesEnabled = _settings.BackgroundServicesEnabled;
        _usbSuspendEnabled = _settings.UsbSuspendEnabled;
        _networkPowerEnabled = _settings.NetworkPowerEnabled;
        _gpuPowerEnabled = _settings.GpuPowerEnabled;
        _cpuParkingEnabled = _settings.CpuParkingEnabled;
        _diskCoalescingEnabled = _settings.DiskCoalescingEnabled;

        // Commands
        OptimizeNowCommand = new RelayCommand(OptimizeNow, () => !IsOptimizing && !IsReadOnlyMode);
        RevertNowCommand = new RelayCommand(RevertNow, () => IsActive && !IsOptimizing && !IsReadOnlyMode);
        RestartAsAdminCommand = new RelayCommand(RestartAsAdmin);
    }

    public void Initialize()
    {
        if (_initialized) return;

        // Wire events
        _powerMonitor.BatteryInfoUpdated += OnBatteryInfoUpdated;
        _powerMonitor.PowerSourceChanged += OnPowerSourceChanged;
        _engine.EventOccurred += OnEngineEvent;

        // Enable privileges on background thread
        Task.Run(PrivilegeManager.EnableAllRequired);

        // Crash recovery
        _engine.TryCrashRecovery();

        // Start monitoring
        _powerMonitor.Start();

        // Check initial state
        var info = BatteryInfoService.GetBatteryInfo();
        OnBatteryInfoUpdated(info);

        // If already on battery and auto is enabled, activate
        if (!info.IsOnAC && _settings.AutoOptimizeOnBattery && !_isReadOnlyMode)
        {
            Task.Run(() =>
            {
                var result = _engine.ActivateAll();
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsActive = _engine.IsActive;
                    RefreshDomainStatuses();
                    ShowResultBanner(result.Message);
                });
            });
        }

        _initialized = true;
        StatusText = _settings.AutoOptimizeOnBattery
            ? "Monitoring battery — auto-optimize enabled"
            : "Monitoring battery";

        RefreshDomainStatuses();
    }

    private void OnBatteryInfoUpdated(BatteryInfo info)
    {
        void Apply()
        {
            ChargePercent = info.ChargePercent;
            BatteryStatus = info.StatusText;
            Watts = info.Watts;
            IsOnAC = info.IsOnAC;
            EstimatedTime = info.EstimatedTimeRemaining.HasValue
                ? $"{info.EstimatedTimeRemaining.Value.Hours}h {info.EstimatedTimeRemaining.Value.Minutes}m"
                : "--:--";

            // Savings tracking: compare current drain to baseline recorded at optimization start
            if (_isActive && !info.IsOnAC && _baselineDrainWatts > 0.1 && info.Watts > 0.01)
            {
                var saved = _baselineDrainWatts - info.Watts;
                SavedWatts = Math.Max(0, saved);
                if (saved > 0.05)
                    SavingsText = $"Saving ~{saved:F1} W ({saved / _baselineDrainWatts * 100:F0}% less drain)";
                else
                    SavingsText = "Measuring savings...";
            }
            else if (!_isActive || info.IsOnAC)
            {
                SavedWatts = 0;
                SavingsText = "";
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            Apply();
        else
            dispatcher.Invoke(Apply);
    }

    private void OnPowerSourceChanged(bool isOnAC)
    {
        if (_isReadOnlyMode || !_settings.AutoOptimizeOnBattery) return;

        // Record baseline drain before optimization (for savings calculation)
        if (!isOnAC)
            _baselineDrainWatts = Watts;

        Task.Run(() =>
        {
            EngineResult result;

            if (!isOnAC)
            {
                // Switched to battery — activate all
                result = _engine.ActivateAll();
            }
            else
            {
                // Switched to AC — revert all
                result = _engine.RevertAll();
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsActive = _engine.IsActive;
                RefreshDomainStatuses();
                ShowResultBanner(result.Message);

                if (isOnAC)
                {
                    _baselineDrainWatts = 0;
                    SavedWatts = 0;
                    SavingsText = "";
                    StatusText = "On AC power — optimizations reverted";
                }
                else
                {
                    SavingsText = "Measuring savings...";
                    StatusText = $"On battery — {result.Message}";
                }
            });
        });
    }

    private void OnEngineEvent(EngineEvent evt)
    {
        var msg = $"[{evt.Timestamp:HH:mm:ss}] {evt.Message}";
        var dispatcher = Application.Current?.Dispatcher;

        void AddLog()
        {
            ActivityLog.Insert(0, msg);
            while (ActivityLog.Count > 100)
                ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }

        if (dispatcher == null || dispatcher.CheckAccess())
            AddLog();
        else
            dispatcher.Invoke(AddLog);
    }

    private void OptimizeNow()
    {
        if (IsOptimizing) return;
        IsOptimizing = true;
        StatusText = "Optimizing...";
        _baselineDrainWatts = Watts; // Record drain before optimization

        try
        {
            Task.Run(() => _engine.ActivateAll()).ContinueWith(task =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsOptimizing = false;
                    IsActive = _engine.IsActive;
                    RefreshDomainStatuses();

                    if (task.IsFaulted)
                    {
                        StatusText = $"Failed: {task.Exception?.InnerException?.Message}";
                        return;
                    }

                    var result = task.Result;
                    ShowResultBanner(result.Message);
                    StatusText = result.Success
                        ? $"Optimized in {result.Duration.TotalMilliseconds:F0}ms"
                        : $"Failed: {result.Message}";
                });
            }, TaskScheduler.Default);
        }
        catch
        {
            IsOptimizing = false;
        }
    }

    private void RevertNow()
    {
        if (IsOptimizing) return;
        IsOptimizing = true;
        StatusText = "Reverting...";

        try
        {
            Task.Run(() => _engine.RevertAll()).ContinueWith(task =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsOptimizing = false;
                    IsActive = _engine.IsActive;
                    RefreshDomainStatuses();

                    if (task.IsFaulted)
                    {
                        StatusText = $"Revert failed: {task.Exception?.InnerException?.Message}";
                        return;
                    }

                    var result = task.Result;
                    ShowResultBanner(result.Message);
                    StatusText = "All optimizations reverted";
                });
            }, TaskScheduler.Default);
        }
        catch
        {
            IsOptimizing = false;
        }
    }

    private void RestartAsAdmin()
    {
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath == null) return;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            });

            Application.Current?.Shutdown();
        }
        catch { StatusText = "Elevation cancelled"; }
    }

    private void RefreshDomainStatuses()
    {
        var statuses = _engine.GetAllStatuses();
        DomainStatuses.Clear();
        foreach (var s in statuses)
            DomainStatuses.Add(s);
    }

    private void ShowResultBanner(string message)
    {
        ResultText = message;
        ShowResult = true;

        // Auto-dismiss after 5 seconds
        Task.Delay(5000).ContinueWith(_ =>
        {
            Application.Current?.Dispatcher.Invoke(() => ShowResult = false);
        });
    }

    public void Shutdown()
    {
        _powerMonitor.BatteryInfoUpdated -= OnBatteryInfoUpdated;
        _powerMonitor.PowerSourceChanged -= OnPowerSourceChanged;
        _engine.EventOccurred -= OnEngineEvent;
        _powerMonitor.Dispose();
        _engine.Dispose();
        _settings.Save();
        _initialized = false;
    }
}
