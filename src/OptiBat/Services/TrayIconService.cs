using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using DrawColor = System.Drawing.Color;
using DrawFont = System.Drawing.Font;

namespace OptiBat.Services;

/// <summary>
/// System tray icon with battery percentage overlay.
/// Renders dynamic icon showing current battery %.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private bool _disposed;

    public event Action? ShowWindowRequested;
    public event Action? ExitRequested;
    public event Action? ToggleOptimizationRequested;

    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "optiBAT — Battery Optimizer",
            DoubleClickCommand = new RelayTrayCommand(() => ShowWindowRequested?.Invoke()),
        };

        _trayIcon.ContextMenu = BuildContextMenu();
        UpdateIcon(100, true); // Default
    }

    public void UpdateIcon(int batteryPercent, bool isOnAC)
    {
        if (_trayIcon == null) return;

        var icon = RenderBatteryIcon(batteryPercent, isOnAC);
        _trayIcon.Icon = icon;
    }

    public void UpdateTooltip(string text)
    {
        if (_trayIcon != null)
            _trayIcon.ToolTipText = text;
    }

    public void ShowBalloon(string title, string message)
    {
        _trayIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var optimizeItem = new MenuItem { Header = "Optimize Now" };
        optimizeItem.Click += (_, _) => ToggleOptimizationRequested?.Invoke();
        menu.Items.Add(optimizeItem);

        menu.Items.Add(new Separator());

        var showItem = new MenuItem { Header = "Show Window" };
        showItem.Click += (_, _) => ShowWindowRequested?.Invoke();
        menu.Items.Add(showItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        return menu;
    }

    private static System.Drawing.Icon RenderBatteryIcon(int percent, bool isOnAC)
    {
        var bmp = new System.Drawing.Bitmap(32, 32);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(DrawColor.Transparent);

        var text = isOnAC ? "AC" : $"{percent}";
        var isDarkTaskbar = ThemeService.IsTaskbarDark();
        var textColor = isDarkTaskbar ? DrawColor.White : DrawColor.Black;

        var fontSize = text.Length <= 2 ? 18f : 14f;
        using var font = new DrawFont("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
        using var brush = new System.Drawing.SolidBrush(textColor);

        var size = g.MeasureString(text, font);
        var x = (32 - size.Width) / 2;
        var y = (32 - size.Height) / 2;
        g.DrawString(text, font, brush, x, y);

        var handle = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _trayIcon?.Dispose();
    }
}

/// <summary>
/// Simple ICommand for tray double-click.
/// </summary>
internal sealed class RelayTrayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    public RelayTrayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
