using System.ComponentModel;
using System.Windows;
using OptiBat.Models;
using OptiBat.Services;
using OptiBat.ViewModels;

namespace OptiBat;

/// <summary>
/// Main application window. Hosts the ViewModel and manages tray icon.
/// </summary>
public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly TrayIconService _trayService;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Apply read-only mode from App
        if (Application.Current is App app)
        {
            _viewModel.IsReadOnlyMode = app.IsReadOnlyMode;
        }

        // Tray icon
        _trayService = new TrayIconService();
        _trayService.ShowWindowRequested += () => Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
        _trayService.ExitRequested += () => Dispatcher.Invoke(() =>
        {
            _trayService.Dispose();
            Application.Current.Shutdown();
        });
        _trayService.ToggleOptimizationRequested += () => Dispatcher.Invoke(() =>
        {
            if (_viewModel.OptimizeNowCommand.CanExecute(null))
                _viewModel.OptimizeNowCommand.Execute(null);
        });

        // Update tray icon with battery info
        _viewModel.PowerMonitor.BatteryInfoUpdated += info =>
        {
            Dispatcher.Invoke(() =>
            {
                _trayService.UpdateIcon(info.Watts, info.IsOnAC);
                _trayService.UpdateTooltip(
                    $"optiBAT — {info.Watts:F1}W | {info.ChargePercent}% {info.StatusText}\n" +
                    (_viewModel.IsActive ? "Optimizations active" : "Monitoring"));
            });
        };

        // Restore window position from settings
        var settings = _viewModel.Settings;
        if (double.IsFinite(settings.WindowLeft) && double.IsFinite(settings.WindowTop))
        {
            Left = settings.WindowLeft;
            Top = settings.WindowTop;
        }
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _trayService.Initialize();
        _viewModel.Initialize();

        // Handle pending activation from second instance
        if (Application.Current is App app)
            app.ApplyPendingActivation();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Save window position
        var settings = _viewModel.Settings;
        if (WindowState == WindowState.Normal)
        {
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
        }

        if (settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        // Actually closing
        _viewModel.Shutdown();
        _trayService.Dispose();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _viewModel.Settings.MinimizeToTray)
        {
            Hide();
        }
    }
}
