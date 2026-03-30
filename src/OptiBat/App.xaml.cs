using System.Diagnostics;
using System.Runtime;
using System.Security.Principal;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using OptiBat.Services;

namespace OptiBat;

public partial class App : Application
{
    internal const string SingleInstanceMutexName = "optiBAT_SingleInstance_C8D4E3";
    internal const string ActivationSignalName = "optiBAT_Activate_C8D4E3";

    private SingleInstanceService? _singleInstance;
    private bool _pendingActivationRestore;

    public bool IsReadOnlyMode { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Unexpected error: {args.Exception.Message}", "optiBAT",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                MessageBox.Show($"Fatal error: {ex.Message}", "optiBAT",
                    MessageBoxButton.OK, MessageBoxImage.Error);
        };

        // Handle command-line arguments
        var args2 = Environment.GetCommandLineArgs();
        for (int i = 1; i < args2.Length; i++)
        {
            switch (args2[i].ToLowerInvariant())
            {
                case "--uninstall":
                    TaskSchedulerHelper.DeleteTask();
                    Shutdown();
                    return;
                case "--register-task":
                    var startAtLogon = args2.Any(a => a.Equals("--start-at-logon", StringComparison.OrdinalIgnoreCase));
                    TaskSchedulerHelper.CreateTask(startAtLogon);
                    Shutdown();
                    return;
            }
        }

        // Single instance enforcement
        _singleInstance = new SingleInstanceService(SingleInstanceMutexName, ActivationSignalName);
        if (!_singleInstance.TryAcquire())
        {
            Shutdown();
            return;
        }

        _singleInstance.StartListening(() =>
        {
            Dispatcher.BeginInvoke(OnActivationSignal);
        });

        // Admin check and elevation
        if (!IsRunningAsAdmin())
        {
            // Try silent elevation via scheduled task
            if (TaskSchedulerHelper.TaskExists() && TaskSchedulerHelper.RunTask())
            {
                Shutdown();
                return;
            }

            // Offer UAC elevation
            var result = MessageBox.Show(
                "optiBAT needs administrator privileges to optimize battery settings.\n\n" +
                "Yes = Restart as admin\nNo = Run in read-only mode\nCancel = Exit",
                "optiBAT — Elevation Required",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    try
                    {
                        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                        if (exePath != null)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = exePath,
                                UseShellExecute = true,
                                Verb = "runas"
                            });
                        }
                    }
                    catch { }
                    Shutdown();
                    return;

                case MessageBoxResult.Cancel:
                    Shutdown();
                    return;

                default:
                    IsReadOnlyMode = true;
                    break;
            }
        }

        // Apply system theme
        ThemeService.Instance.ApplySystemTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        // Battery efficiency for self
        Timeline.DesiredFrameRateProperty.OverrideMetadata(
            typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = 30 });

        // Low-latency GC
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
    }

    private void OnActivationSignal()
    {
        if (Dispatcher.HasShutdownStarted) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (Current.MainWindow is MainWindow mainWindow)
            {
                _pendingActivationRestore = false;
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
                return;
            }
            _pendingActivationRestore = true;
        });
    }

    internal void ApplyPendingActivation()
    {
        if (_pendingActivationRestore && Current.MainWindow is MainWindow mainWindow)
        {
            _pendingActivationRestore = false;
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
            Dispatcher.BeginInvoke(ThemeService.Instance.OnUserPreferenceChanged);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        ThemeService.Instance.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
