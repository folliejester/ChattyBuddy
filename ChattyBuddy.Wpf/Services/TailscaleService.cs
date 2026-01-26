using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ChattyBuddy.Wpf.Models;

namespace ChattyBuddy.Wpf.Services
{
    public class TailscaleService
    {
        public async Task<IList<TailscaleDevice>> GetOnlineDevicesAsync()
        {
            var result = new List<TailscaleDevice>();
            var json = await RunTailscaleStatusJson();
            if (string.IsNullOrWhiteSpace(json))
                return result;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Peer", out var peersElement))
                return result;

            foreach (var peerProperty in peersElement.EnumerateObject())
            {
                var peerId = peerProperty.Name;
                var peer = peerProperty.Value;

                bool isOnline = false;
                if (peer.TryGetProperty("Online", out var onlineProp))
                    isOnline = onlineProp.GetBoolean();

                string name = peer.TryGetProperty("DNSName", out var dnsNameProp) ? dnsNameProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) && peer.TryGetProperty("HostName", out var hostNameProp))
                    name = hostNameProp.GetString();
                if (string.IsNullOrWhiteSpace(name))
                    name = peerId;

                var displayName = ExtractDeviceName(name);

                string address = null;
                if (peer.TryGetProperty("TailscaleIPs", out var ipsProp) && ipsProp.ValueKind == JsonValueKind.Array && ipsProp.GetArrayLength() > 0)
                    address = ipsProp[0].GetString();

                result.Add(new TailscaleDevice
                {
                    Id = peerId,
                    Name = displayName ?? peerId,
                    Address = address ?? string.Empty,
                    Online = isOnline
                });
            }

            return result;
        }

        private async Task<string> RunTailscaleStatusJson()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "tailscale",
                Arguments = "status --json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                return output;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> IsDeviceOnlineAsync(string deviceId)
        {
            var devices = await GetOnlineDevicesAsync();
            foreach (var device in devices)
            {
                if (string.Equals(device.Id, deviceId, StringComparison.OrdinalIgnoreCase) && device.Online)
                    return true;
            }

            return false;
        }

        private static string ExtractDeviceName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return rawName;

            var idx = rawName.IndexOf('.');
            if (idx > 0)
                return rawName.Substring(0, idx);

            return rawName;
        }
    }
}
