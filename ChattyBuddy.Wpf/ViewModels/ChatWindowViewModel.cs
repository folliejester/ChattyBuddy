using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using ChattyBuddy.Wpf.Infrastructure;
using ChattyBuddy.Wpf.Models;
using ChattyBuddy.Wpf.Services;

namespace ChattyBuddy.Wpf.ViewModels
{
    public class ChatWindowViewModel : BaseViewModel
    {
        public class ChatMessage : BaseViewModel
        {
            string text;
            bool isOwn;
            DateTime timestamp;
            bool isTyping;
            bool isDelivered;

            public string Text
            {
                get => text;
                set => SetField(ref text, value);
            }

            public bool IsOwn
            {
                get => isOwn;
                set => SetField(ref isOwn, value);
            }

            public DateTime Timestamp
            {
                get => timestamp;
                set => SetField(ref timestamp, value);
            }

            public bool IsTyping
            {
                get => isTyping;
                set => SetField(ref isTyping, value);
            }

            public bool IsDelivered
            {
                get => isDelivered;
                set => SetField(ref isDelivered, value);
            }
        }

        class HistoryMessage
        {
            public string Text { get; set; }
            public bool IsOwn { get; set; }
            public DateTime Timestamp { get; set; }
            public bool IsDelivered { get; set; }
        }

        const string TypingStartMessage = "__typing__:start";
        const string TypingStopMessage = "__typing__:stop";

        readonly ChatService chatService;
        readonly string remoteAddress;
        readonly int remotePort;
        readonly string historyFilePath;

        string outgoingText;
        bool lastTypingStateSent;
        DateTime lastTypingActivityUtc;
        bool typingMonitorRunning;
        ChatMessage typingMessage;

        int pendingNudgeCount;
        ChatMessage lastNudgeMessage;

        public TailscaleDevice Device { get; }

        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();

        public string OutgoingText
        {
            get => outgoingText;
            set
            {
                if (SetField(ref outgoingText, value))
                {
                    if (SendCommand is RelayCommand relay)
                        relay.RaiseCanExecuteChanged();
                    RegisterTypingActivity();
                }
            }
        }

        public ICommand SendCommand { get; }

        public ChatWindowViewModel(TailscaleDevice device, ChatService chatService, int remotePort)
        {
            Device = device;
            this.chatService = chatService;
            remoteAddress = device.Address;
            this.remotePort = remotePort;
            historyFilePath = BuildHistoryFilePath(device.Id, remoteAddress);
            SendCommand = new RelayCommand(async _ => await SendAsync(), _ => !string.IsNullOrWhiteSpace(OutgoingText));
        }

        public void AddIncomingMessage(string text)
        {
            ClearPendingNudge();

            text = NormalizeMessage(text);

            if (string.Equals(text, TypingStartMessage, StringComparison.Ordinal))
            {
                if (typingMessage == null)
                {
                    typingMessage = new ChatMessage
                    {
                        Text = "Typing...",
                        IsOwn = false,
                        IsTyping = true,
                        Timestamp = DateTime.Now
                    };
                    Messages.Add(typingMessage);
                }
                return;
            }

            if (string.Equals(text, TypingStopMessage, StringComparison.Ordinal))
            {
                if (typingMessage != null)
                {
                    Messages.Remove(typingMessage);
                    typingMessage = null;
                }
                return;
            }

            if (text == null)
                return;

            Messages.Add(new ChatMessage
            {
                Text = text,
                IsOwn = false,
                Timestamp = DateTime.Now,
                IsDelivered = true
            });
        }

        public void AddIncomingNudge()
        {
            pendingNudgeCount++;

            if (lastNudgeMessage == null)
            {
                lastNudgeMessage = new ChatMessage
                {
                    Text = "Nudge",
                    IsOwn = false,
                    Timestamp = DateTime.Now
                };
                Messages.Add(lastNudgeMessage);
            }
            else
            {
                lastNudgeMessage.Text = pendingNudgeCount > 1 ? $"Nudge x{pendingNudgeCount}" : "Nudge";
                lastNudgeMessage.Timestamp = DateTime.Now;
            }
        }

        async Task SendAsync()
        {
            ClearPendingNudge();

            var text = NormalizeMessage(OutgoingText);
            text = TrimEdgesEmptyLines(text);
            if (string.IsNullOrWhiteSpace(text))
                return;

            var message = new ChatMessage
            {
                Text = text,
                IsOwn = true,
                Timestamp = DateTime.Now,
                IsDelivered = false
            };

            Messages.Add(message);

            OutgoingText = string.Empty;

            if (!string.IsNullOrWhiteSpace(remoteAddress))
            {
                var delivered = await chatService.SendMessageAsync(remoteAddress, remotePort, text);
                if (delivered)
                    message.IsDelivered = true;
            }

            RegisterTypingActivity();
        }

        void RegisterTypingActivity()
        {
            var hasText = !string.IsNullOrWhiteSpace(outgoingText);

            if (hasText)
            {
                lastTypingActivityUtc = DateTime.UtcNow;

                if (!lastTypingStateSent)
                {
                    lastTypingStateSent = true;
                    if (!string.IsNullOrWhiteSpace(remoteAddress))
                        _ = chatService.SendMessageAsync(remoteAddress, remotePort, TypingStartMessage);
                    if (!typingMonitorRunning)
                    {
                        typingMonitorRunning = true;
                        _ = TypingMonitorLoopAsync();
                    }
                }
            }
            else
            {
                if (lastTypingStateSent)
                {
                    lastTypingStateSent = false;
                    if (!string.IsNullOrWhiteSpace(remoteAddress))
                        _ = chatService.SendMessageAsync(remoteAddress, remotePort, TypingStopMessage);
                }
            }
        }

        async Task TypingMonitorLoopAsync()
        {
            const int idleMs = 2000;

            try
            {
                while (lastTypingStateSent)
                {
                    await Task.Delay(500).ConfigureAwait(false);

                    if (!lastTypingStateSent)
                        break;

                    var idle = (DateTime.UtcNow - lastTypingActivityUtc).TotalMilliseconds;
                    if (idle >= idleMs)
                    {
                        lastTypingStateSent = false;
                        if (!string.IsNullOrWhiteSpace(remoteAddress))
                            _ = chatService.SendMessageAsync(remoteAddress, remotePort, TypingStopMessage);
                        break;
                    }
                }
            }
            finally
            {
                typingMonitorRunning = false;
            }
        }

        public async Task LoadHistoryAsync()
        {
            if (string.IsNullOrWhiteSpace(historyFilePath))
                return;
            if (!File.Exists(historyFilePath))
                return;

            try
            {
                var json = await File.ReadAllTextAsync(historyFilePath);
                var items = JsonSerializer.Deserialize<List<HistoryMessage>>(json);
                if (items == null)
                    return;

                Messages.Clear();
                foreach (var item in items)
                {
                    Messages.Add(new ChatMessage
                    {
                        Text = item.Text,
                        IsOwn = item.IsOwn,
                        Timestamp = item.Timestamp,
                        IsDelivered = item.IsDelivered
                    });
                }
            }
            catch
            {
            }
        }

        public async Task SaveHistoryAsync()
        {
            if (string.IsNullOrWhiteSpace(historyFilePath))
                return;

            try
            {
                var dir = Path.GetDirectoryName(historyFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var list = new List<HistoryMessage>();
                foreach (var m in Messages)
                {
                    if (m.IsTyping)
                        continue;

                    list.Add(new HistoryMessage
                    {
                        Text = m.Text,
                        IsOwn = m.IsOwn,
                        Timestamp = m.Timestamp,
                        IsDelivered = m.IsDelivered
                    });
                }

                var json = JsonSerializer.Serialize(list);
                await File.WriteAllTextAsync(historyFilePath, json);
            }
            catch
            {
            }
        }

        public async Task ClearAsync()
        {
            Messages.Clear();
            ClearPendingNudge();

            if (!string.IsNullOrWhiteSpace(historyFilePath))
            {
                try
                {
                    if (File.Exists(historyFilePath))
                        File.Delete(historyFilePath);
                }
                catch
                {
                }
            }

            await Task.CompletedTask;
        }

        void ClearPendingNudge()
        {
            pendingNudgeCount = 0;
            lastNudgeMessage = null;
        }

        static string BuildHistoryFilePath(string deviceId, string address)
        {
            if (string.IsNullOrWhiteSpace(deviceId) && string.IsNullOrWhiteSpace(address))
                return null;

            var key = !string.IsNullOrWhiteSpace(deviceId) ? deviceId : address;
            var safe = key.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(root, "ChattyBuddy", "history");
            return Path.Combine(folder, safe + ".json");
        }

        static string NormalizeMessage(string text)
        {
            if (text == null)
                return null;

            var normalized = text.Replace("\r\n", "\n");
            return normalized.Replace("\n", Environment.NewLine);
        }

        static string TrimTrailingEmptyLines(string text)
        {
            if (text == null)
                return null;

            var normalized = text.Replace("\r\n", "\n");
            var parts = normalized.Split('\n');
            var end = parts.Length - 1;
            while (end >= 0 && string.IsNullOrWhiteSpace(parts[end]))
                end--;
            if (end < 0)
                return string.Empty;
            return string.Join(Environment.NewLine, parts, 0, end + 1);
        }

        static string TrimEdgesEmptyLines(string text)
        {
            if (text == null)
                return null;

            var normalized = text.Replace("\r\n", "\n");
            var parts = normalized.Split('\n');
            int start = 0;
            int end = parts.Length - 1;
            while (start <= end && string.IsNullOrWhiteSpace(parts[start]))
                start++;
            while (end >= start && string.IsNullOrWhiteSpace(parts[end]))
                end--;
            if (start > end)
                return string.Empty;
            return string.Join(Environment.NewLine, parts, start, end - start + 1);
        }
    }
}
