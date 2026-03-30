using Microsoft.Win32;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace OptiBat.Services;

/// <summary>
/// Manages dark/light theme following system preference.
/// Applies Mica backdrop via WPF-UI.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    private bool _disposed;

    public event Action? ThemeChanged;

    public bool IsDarkTheme { get; private set; }

    private ThemeService()
    {
        IsDarkTheme = DetectSystemDarkMode();
    }

    public void ApplySystemTheme()
    {
        IsDarkTheme = DetectSystemDarkMode();
        var theme = IsDarkTheme
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;

        ApplicationThemeManager.Apply(theme);
        ThemeChanged?.Invoke();
    }

    public void OnUserPreferenceChanged()
    {
        var wasDark = IsDarkTheme;
        IsDarkTheme = DetectSystemDarkMode();

        if (wasDark != IsDarkTheme)
            ApplySystemTheme();
    }

    private static bool DetectSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detect if taskbar uses dark theme (for tray icon text color).
    /// </summary>
    public static bool IsTaskbarDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("SystemUsesLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _instance = null;
    }
}
