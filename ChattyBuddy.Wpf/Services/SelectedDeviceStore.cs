using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChattyBuddy.Wpf.Services
{
    public class SelectedDeviceStore
    {
        private const string FileName = "settings.json";

        private string GetSettingsPath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChattyBuddy");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, FileName);
        }

        public async Task SaveSelectedDeviceIdAsync(string deviceId)
        {
            var path = GetSettingsPath();
            var obj = new SettingsModel
            {
                SelectedDeviceId = deviceId
            };
            var json = JsonSerializer.Serialize(obj);
            await File.WriteAllTextAsync(path, json);
        }

        public async Task<string> LoadSelectedDeviceIdAsync()
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(path);
                var obj = JsonSerializer.Deserialize<SettingsModel>(json);
                return obj != null ? obj.SelectedDeviceId : null;
            }
            catch
            {
                return null;
            }
        }

        private class SettingsModel
        {
            public string SelectedDeviceId { get; set; }
        }
    }
}
