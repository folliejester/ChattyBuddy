using System;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Media;
using System.IO;
using ChattyBuddy.Wpf.Models;
using ChattyBuddy.Wpf.Services;
using ChattyBuddy.Wpf.ViewModels;

namespace ChattyBuddy.Wpf
{
    public partial class ChatWindow : Window
    {
        const int GWL_STYLE = -16;
        const int WS_MAXIMIZEBOX = 0x00010000;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        const uint FLASHW_ALL = 3;
        const uint FLASHW_TIMERNOFG = 12;

        private readonly ChatService chatService;
        private readonly ChatWindowViewModel viewModel;
        private const int ChatPort = 54545;
        private HwndSource hwndSource;

        public ChatWindow(TailscaleDevice device)
        {
            InitializeComponent();
            Title = device.Name;

            chatService = new ChatService();
            viewModel = new ChatWindowViewModel(device, chatService, ChatPort);
            DataContext = viewModel;

            StateChanged += ChatWindow_OnStateChanged;
            Loaded += ChatWindow_OnLoaded;
            Closed += ChatWindow_OnClosed;
            SourceInitialized += ChatWindow_OnSourceInitialized;
        }

        private void ChatWindow_OnSourceInitialized(object sender, EventArgs e)
        {
            hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            if (hwndSource != null)
                hwndSource.AddHook(WndProc);
        }

        private async void ChatWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            SystemTheme.Apply(this);

            Width = 260;
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width;
            Top = workArea.Bottom - Height;

            await viewModel.LoadHistoryAsync();
            MessagesScrollViewer.ScrollToEnd();

            chatService.Start(ChatPort, message =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (string.Equals(message, "__nudge__", StringComparison.Ordinal))
                    {
                        viewModel.AddIncomingNudge();
                        PlayNudgeSound();
                        FlashWindow();
                    }
                    else
                    {
                        MergeOrAddIncomingMessage(message);
                    }
                });
            });

            viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
            UpdateMaximizeBoxVisibility(true);
            UpdateActionButtonContent();
        }

        void FlashWindow()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var info = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                hwnd = hwnd,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = 3,
                dwTimeout = 0
            };
            FlashWindowEx(ref info);
        }

        private void ChatWindow_OnStateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                var mainWindow = new MainWindow(true);
                mainWindow.Show();
                Close();
            }
            else if (WindowState == WindowState.Minimized)
            {
                UpdateMaximizeBoxVisibility(false);
            }
            else if (WindowState == WindowState.Normal)
            {
                UpdateMaximizeBoxVisibility(true);
            }
        }

        private void ChatWindow_OnClosed(object sender, EventArgs e)
        {
            if (hwndSource != null)
                hwndSource.RemoveHook(WndProc);

            viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
            chatService.Stop();
        }

        private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            MessagesScrollViewer.ScrollToEnd();
            _ = viewModel.SaveHistoryAsync();
        }

        private void MessageTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (viewModel.SendCommand.CanExecute(null))
                    viewModel.SendCommand.Execute(null);
                e.Handled = true;
            }
        }

        private async void ClearChatMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await viewModel.ClearAsync();
            MessagesScrollViewer.ScrollToEnd();
        }

        private void MessageTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateActionButtonContent();
        }

        private void UpdateActionButtonContent()
        {
            if (ActionButton == null)
                return;
            var text = viewModel.OutgoingText;
            if (string.IsNullOrWhiteSpace(text))
                ActionButton.Content = new System.Windows.Controls.TextBlock { Text = "🔔", FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            else
                ActionButton.Content = new System.Windows.Controls.TextBlock { Text = "➤", FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }

        private async void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            var text = viewModel.OutgoingText;
            if (string.IsNullOrWhiteSpace(text))
            {
                if (!string.IsNullOrWhiteSpace(viewModel.Device?.Address))
                    await chatService.SendMessageAsync(viewModel.Device.Address, ChatPort, "__nudge__");
            }
            else
            {
                if (viewModel.SendCommand.CanExecute(null))
                    viewModel.SendCommand.Execute(null);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_SETTINGCHANGE = 0x001A;
            const int WM_THEMECHANGED = 0x031A;
            if (msg == WM_SETTINGCHANGE || msg == WM_THEMECHANGED)
                SystemTheme.Apply(this);
            return IntPtr.Zero;
        }

        void UpdateMaximizeBoxVisibility(bool visible)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            var style = GetWindowLong(hwnd, GWL_STYLE);
            if (visible)
                style |= WS_MAXIMIZEBOX;
            else
                style &= ~WS_MAXIMIZEBOX;

            SetWindowLong(hwnd, GWL_STYLE, style);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        private void PlayNudgeSound()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory ?? string.Empty, "Assets", "Nudge.wav"),
                Path.Combine(Environment.CurrentDirectory ?? string.Empty, "Assets", "Nudge.wav"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "Assets", "Nudge.wav"),
                Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? string.Empty, "Assets", "Nudge.wav")
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        var player = new SoundPlayer(candidate);
                        player.Load();
                        player.Play();
                        return;
                    }
                }
                catch
                {
                }
            }

            try
            {
                var uri = new Uri("pack://application:,,,/Assets/Nudge.wav");
                var info = Application.GetResourceStream(uri);
                if (info?.Stream != null)
                {
                    using var stream = info.Stream;
                    var player = new SoundPlayer(stream);
                    player.Load();
                    player.Play();
                    return;
                }
            }
            catch
            {
            }

            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch
            {
            }
        }

        private static string NormalizeMessageLocal(string text)
        {
            if (text == null)
                return null;

            var normalized = text.Replace("\r\n", "\n");
            return normalized.Replace("\n", Environment.NewLine);
        }

        private void MergeOrAddIncomingMessage(string rawMessage)
        {
            if (string.Equals(rawMessage, "__typing__:start", StringComparison.Ordinal) || string.Equals(rawMessage, "__typing__:stop", StringComparison.Ordinal))
            {
                viewModel.AddIncomingMessage(rawMessage);
                return;
            }

            var text = NormalizeMessageLocal(rawMessage);
            if (text == null)
                return;

            var last = viewModel.Messages.LastOrDefault();
            if (last != null && !last.IsOwn && !last.IsTyping)
            {
                var ageMs = (DateTime.Now - last.Timestamp).TotalMilliseconds;
                const int mergeMs = 300;
                if (ageMs <= mergeMs)
                {
                    last.Text = string.IsNullOrEmpty(last.Text) ? text : last.Text + Environment.NewLine + text;
                    last.Timestamp = DateTime.Now;
                    _ = viewModel.SaveHistoryAsync();
                    MessagesScrollViewer.ScrollToEnd();
                    return;
                }
            }

            viewModel.AddIncomingMessage(text);
            MessagesScrollViewer.ScrollToEnd();
        }
    }
}
