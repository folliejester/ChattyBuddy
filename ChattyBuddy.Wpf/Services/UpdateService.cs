using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace ChattyBuddy.Wpf.Services
{
    public class UpdateService
    {
        private readonly HttpClient http = new HttpClient();

        public UpdateService()
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ChattyBuddy-Updater");
        }

        public async Task<string> GetLatestTagAsync()
        {
            try
            {
                using var resp = await http.GetAsync("https://api.github.com/repos/folliejester/ChattyBuddy/releases/latest");
                if (!resp.IsSuccessStatusCode) return null;
                using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
                if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                    return tag.GetString();
            }
            catch
            {
            }
            return null;
        }

        public async Task<string> GetLatestExeAssetUrlAsync()
        {
            try
            {
                using var resp = await http.GetAsync("https://api.github.com/repos/folliejester/ChattyBuddy/releases/latest");
                if (!resp.IsSuccessStatusCode) return null;
                using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
                if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        if (asset.TryGetProperty("browser_download_url", out var urlProp))
                        {
                            var url = urlProp.GetString();
                            if (!string.IsNullOrWhiteSpace(url) && url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                return url;
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public async Task<bool> DownloadWithBitsAsync(string url, string destPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"Start-BitsTransfer -Source '{url}' -Destination '{destPath}'\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(startInfo);
                if (p == null) return false;
                await p.WaitForExitAsync();
                return File.Exists(destPath);
            }
            catch
            {
                return false;
            }
        }

        public string GetCurrentVersion()
        {
            var asm = Assembly.GetEntryAssembly();
            var ver = asm?.GetName().Version?.ToString();
            return ver;
        }

        public bool IsNewer(string latestTag, string currentVersion)
        {
            if (string.IsNullOrWhiteSpace(latestTag) || string.IsNullOrWhiteSpace(currentVersion))
                return false;
            var lt = NormalizeVersion(latestTag);
            var cv = NormalizeVersion(currentVersion);
            var la = lt.Split('.');
            var ca = cv.Split('.');
            var len = Math.Max(la.Length, ca.Length);
            for (int i = 0; i < len; i++)
            {
                int li = i < la.Length ? int.TryParse(la[i], out var v1) ? v1 : 0 : 0;
                int ci = i < ca.Length ? int.TryParse(ca[i], out var v2) ? v2 : 0 : 0;
                if (li > ci) return true;
                if (li < ci) return false;
            }
            return false;
        }

        private string NormalizeVersion(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return v;
            if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase)) v = v.Substring(1);
            var parts = v.Split('-', '+')[0];
            return parts;
        }
    }
}
