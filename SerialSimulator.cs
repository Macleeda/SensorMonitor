using System;
using System.Threading;
using System.Threading.Tasks;

namespace SensorMonitor
{
    /// <summary>
    /// 虚拟串口数据模拟器 — 无需真实硬件即可产生4通道温湿度数据
    /// 亮点：生产者-消费者模式，数据生成与消费解耦
    /// </summary>
    public class SerialSimulator
    {
        public event Action<int, double, double> OnDataReceived;

        private readonly Random _rnd = new Random();
        private readonly double[] _baseTemp  = { 25.0, 26.5, 24.0, 27.2 };
        private readonly double[] _baseHumid = { 55.0, 48.0, 62.0, 50.5 };
        private CancellationTokenSource _cts;
        private bool _running;

        /// <summary>启动数据模拟（4通道同时产生数据）</summary>
        public void Start(int intervalMs = 800)
        {
            if (_running) return;
            _running = true;
            _cts = new CancellationTokenSource();

            Task.Run(() =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    // 4个通道轮流产生数据
                    for (int ch = 1; ch <= 4; ch++)
                    {
                        double temp = _baseTemp[ch - 1] + (_rnd.NextDouble() * 4 - 2);
                        double hum  = _baseHumid[ch - 1] + (_rnd.NextDouble() * 10 - 5);
                        OnDataReceived?.Invoke(ch, Math.Round(temp, 1), Math.Round(hum, 1));
                    }
                    Thread.Sleep(intervalMs);
                }
            }, _cts.Token);
        }

        public void Stop()
        {
            _running = false;
            _cts?.Cancel();
        }
    }
}
