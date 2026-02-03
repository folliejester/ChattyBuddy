using System;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using ChattyBuddy.Wpf.Services;
using System.Diagnostics;
using System.IO;

namespace ChattyBuddy.Wpf
{
    public partial class App : Application
    {
        private const long BootThresholdMs = 120_000;

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            var updater = new UpdateService();
            var latest = await updater.GetLatestTagAsync();
            var current = updater.GetCurrentVersion();
            if (!string.IsNullOrWhiteSpace(latest) && !string.IsNullOrWhiteSpace(current) && updater.IsNewer(latest, current))
            {
                var msg = $"A new version ({latest}) is available. Download and run installer now?";
                var res = MessageBox.Show(msg, "Update available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (res == MessageBoxResult.Yes)
                {
                    var assetUrl = await updater.GetLatestExeAssetUrlAsync();
                    if (!string.IsNullOrWhiteSpace(assetUrl))
                    {
                        var outFile = Path.Combine(Path.GetTempPath(), "ChattyBuddy-setup.exe");
                        var ok = await updater.DownloadWithBitsAsync(assetUrl, outFile);
                        if (ok && File.Exists(outFile))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo(outFile) { UseShellExecute = true });
                                Shutdown();
                                return;
                            }
                            catch
                            {
                            }
                        }
                        try
                        {
                            Process.Start(new ProcessStartInfo("https://github.com/folliejester/ChattyBuddy/releases") { UseShellExecute = true });
                            Shutdown();
                            return;
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo("https://github.com/folliejester/ChattyBuddy/releases") { UseShellExecute = true });
                            Shutdown();
                            return;
                        }
                        catch
                        {
                        }
                    }
                }
            }

            var main = new MainWindow(false);

            var isAutoStart = false;
            if (e.Args != null)
            {
                foreach (var arg in e.Args)
                {
                    if (string.Equals(arg, "/autostart", StringComparison.OrdinalIgnoreCase))
                    {
                        isAutoStart = true;
                        break;
                    }
                }
            }

            var isLikelyBootLaunch = Environment.TickCount64 < BootThresholdMs;
            if (isAutoStart || isLikelyBootLaunch)
                await Task.Delay(TimeSpan.FromMinutes(1));

            main.Show();
        }
    }
}

