using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ChattyBuddy.Wpf.Models;
using ChattyBuddy.Wpf.Services;
using ChattyBuddy.Wpf.ViewModels;
using System.Diagnostics;
using System.IO;

namespace ChattyBuddy.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private DeviceSelectionViewModel viewModel;
    private readonly bool suppressAutoConnect;
    private HwndSource hwndSource;

    public MainWindow() : this(false)
    {
    }

    public MainWindow(bool suppressAutoConnect)
    {
        InitializeComponent();
        this.suppressAutoConnect = suppressAutoConnect;
        var tailscaleService = new TailscaleService();
        viewModel = new DeviceSelectionViewModel(tailscaleService, new SelectedDeviceStore());
        viewModel.Connected += ViewModelOnConnected;
        DataContext = viewModel;
        SourceInitialized += MainWindow_OnSourceInitialized;
        Closed += MainWindow_OnClosed;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        SystemTheme.Apply(this);
        EnsureAutoLaunch();
        await viewModel.InitializeAsync(!suppressAutoConnect);
    }

    private void ViewModelOnConnected(TailscaleDevice device)
    {
        var chatWindow = new ChatWindow(device);
        chatWindow.Show();
        Close();
    }

    private void MainWindow_OnSourceInitialized(object sender, System.EventArgs e)
    {
        hwndSource = (HwndSource)PresentationSource.FromVisual(this);
        if (hwndSource != null)
            hwndSource.AddHook(WndProc);
    }

    private void MainWindow_OnClosed(object sender, System.EventArgs e)
    {
        if (hwndSource != null)
            hwndSource.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SETTINGCHANGE = 0x001A;
        const int WM_THEMECHANGED = 0x031A;
        if (msg == WM_SETTINGCHANGE || msg == WM_THEMECHANGED)
            SystemTheme.Apply(this);
        return IntPtr.Zero;
    }

    private void EnsureAutoLaunch()
    {
        try
        {
            var runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(runKey);
            string exePath = null;
            var procPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(procPath) && Path.GetExtension(procPath).Equals(".exe", System.StringComparison.OrdinalIgnoreCase))
                exePath = procPath;
            else
            {
                var asm = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                var dir = string.IsNullOrEmpty(asm) ? AppContext.BaseDirectory : Path.GetDirectoryName(asm);
                var candidate = Path.Combine(dir ?? string.Empty, "ChattyBuddy.exe");
                if (File.Exists(candidate))
                    exePath = candidate;
                else if (!string.IsNullOrWhiteSpace(asm))
                    exePath = asm;
            }
            if (!string.IsNullOrWhiteSpace(exePath))
                key.SetValue("ChattyBuddy", $"\"{exePath}\"");
        }
        catch
        {
        }
    }
}

static class SystemTheme
{
    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void Apply(Window window)
    {
        var isLight = true;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
                isLight = i != 0;
        }
        catch
        {
        }

        SolidColorBrush backgroundBrush;
        SolidColorBrush controlBackgroundBrush;
        SolidColorBrush textForegroundBrush;
        SolidColorBrush subtleTextForegroundBrush;
        SolidColorBrush borderBrush;
        SolidColorBrush buttonBackgroundBrush;
        SolidColorBrush buttonForegroundBrush;
        SolidColorBrush scrollBackgroundBrush;
        SolidColorBrush ownBubbleBrush;

        if (isLight)
        {
            backgroundBrush = SystemColors.WindowBrush;
            controlBackgroundBrush = Brushes.White;
            textForegroundBrush = Brushes.Black;
            subtleTextForegroundBrush = Brushes.Gray;
            borderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            buttonBackgroundBrush = SystemColors.ControlBrush;
            buttonForegroundBrush = Brushes.Black;
            scrollBackgroundBrush = Brushes.White;
            ownBubbleBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0xF8, 0xC6));
            EnableDarkTitleBar(window, false);
        }
        else
        {
            backgroundBrush = new SolidColorBrush(Color.FromRgb(18, 18, 18));
            controlBackgroundBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            textForegroundBrush = Brushes.White;
            subtleTextForegroundBrush = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            borderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            buttonBackgroundBrush = new SolidColorBrush(Color.FromRgb(45, 45, 45));
            buttonForegroundBrush = Brushes.White;
            scrollBackgroundBrush = new SolidColorBrush(Color.FromRgb(24, 24, 24));
            ownBubbleBrush = new SolidColorBrush(Color.FromRgb(0x14, 0x4D, 0x37));
            EnableDarkTitleBar(window, true);
        }

        window.Background = backgroundBrush;

        window.Resources["ChatBackgroundBrush"] = backgroundBrush;
        window.Resources["ChatControlBackgroundBrush"] = controlBackgroundBrush;
        window.Resources["ChatTextForegroundBrush"] = textForegroundBrush;
        window.Resources["ChatSubtleTextForegroundBrush"] = subtleTextForegroundBrush;
        window.Resources["ChatBorderBrush"] = borderBrush;
        window.Resources["ChatButtonBackgroundBrush"] = buttonBackgroundBrush;
        window.Resources["ChatButtonForegroundBrush"] = buttonForegroundBrush;
        window.Resources["ChatScrollBackgroundBrush"] = backgroundBrush;
        window.Resources["ChatOwnBubbleBrush"] = ownBubbleBrush;
        window.Resources["ChatReceivedBubbleBrush"] = isLight
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1))
            : new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E));
    }

    static void EnableDarkTitleBar(Window window, bool enabled)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var useDark = enabled ? 1 : 0;
        try
        {
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
        catch
        {
        }
    }
}