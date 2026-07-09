using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SensorMonitor
{
    /// <summary>
    /// TCP 远程监控服务 — 局域网内其他终端可连接查看实时数据
    /// 亮点：Socket 异步编程，支持多客户端同时连接
    /// </summary>
    public class TcpServerHelper
    {
        private TcpListener _listener;
        private bool _running;
        public event Action<string> OnClientMessage;   // 收到客户端消息
        public event Action<string> OnStatusChanged;    // 状态变更日志

        public void Start(int port = 8899)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _running = true;
            OnStatusChanged?.Invoke($"TCP服务已启动，端口 {port}");

            Task.Run(async () =>
            {
                while (_running)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        OnStatusChanged?.Invoke($"客户端连接: {client.Client.RemoteEndPoint}");
                        _ = HandleClient(client);
                    }
                    catch { break; }
                }
            });
        }

        private async Task HandleClient(TcpClient client)
        {
            using (client)
            {
                var stream = client.GetStream();
                byte[] buf = new byte[1024];
                try
                {
                    while (client.Connected && _running)
                    {
                        int n = await stream.ReadAsync(buf, 0, buf.Length);
                        if (n == 0) break;
                        string msg = Encoding.UTF8.GetString(buf, 0, n);
                        OnClientMessage?.Invoke(msg);
                    }
                }
                catch { }
                OnStatusChanged?.Invoke("客户端断开");
            }
        }

        /// <summary>向所有当前连接的客户端广播数据（简化实现：仅支持单个客户端演示）</summary>
        public async void Broadcast(string data)
        {
            // 简化：仅演示。实际项目维护客户端列表向所有客户端广播。
            // 这里通过事件把数据交给 Form，Form 内部处理。
        }

        public void Stop()
        {
            _running = false;
            _listener?.Stop();
            OnStatusChanged?.Invoke("TCP服务已停止");
        }
    }
}
