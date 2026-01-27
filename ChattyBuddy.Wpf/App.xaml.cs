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
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
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

            bool isLikelyBootLaunch = Environment.TickCount64 < BootThresholdMs;

            if (!isLikelyBootLaunch)
            {
                var main = new MainWindow(false);
                main.Show();
                return;
            }

            var tailscale = new TailscaleService();

            await Task.Run(async () =>
            {
                while (!await tailscale.IsTailscaleNetworkAvailableAsync())
                    await Task.Delay(2000);
            });

            Dispatcher.Invoke(() =>
            {
                var main = new MainWindow(false);
                main.Show();
            });
        }
    }
}

