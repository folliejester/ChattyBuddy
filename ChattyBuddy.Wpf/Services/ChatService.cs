using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChattyBuddy.Wpf.Services
{
    public class ChatService
    {
        CancellationTokenSource cancellationTokenSource;
        TcpListener listener;

        public void Start(int port, Action<string> onMessageReceived)
        {
            if (listener != null)
                return;

            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            var localListener = listener;

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        TcpClient client = null;
                        try
                        {
                            client = await localListener.AcceptTcpClientAsync();
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                        catch
                        {
                            if (token.IsCancellationRequested)
                                break;
                            continue;
                        }

                        _ = HandleClientAsync(client, onMessageReceived, token);
                    }
                }
                catch
                {
                }
            }, token);
        }

        private async Task HandleClientAsync(TcpClient client, Action<string> onMessageReceived, CancellationToken token)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                while (!token.IsCancellationRequested && !reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null)
                        break;
                    onMessageReceived?.Invoke(line);
                }
            }
        }

        public async Task<bool> SendMessageAsync(string address, int port, string message)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(address, port);
                using var stream = client.GetStream();
                var bytes = Encoding.UTF8.GetBytes(message + Environment.NewLine);
                await stream.WriteAsync(bytes, 0, bytes.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Stop()
        {
            try
            {
                cancellationTokenSource?.Cancel();
                listener?.Stop();
            }
            catch
            {
            }
        }
    }
}
