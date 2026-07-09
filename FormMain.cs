using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SensorMonitor
{
    public class FormMain : Form
    {
        // ── 核心组件 ──
        private SerialSimulator _simulator;
        private DbManager _db;
        private TcpServerHelper _tcpServer;
        private System.Windows.Forms.Timer _alarmTimer;

        // ── UI 控件 ──
        private PictureBox _wavePanel;
        private CircularGauge[] _gauges = new CircularGauge[4];
        private Label[] _gaugeLabels = new Label[4];
        private Button _btnStart, _btnStop, _btnHistory, _btnExport, _btnTcp;
        private RichTextBox _logBox;
        private Label _lblStatus;

        // ── 数据缓冲（生产者-消费者模式的桥梁） ──
        private readonly Queue<(int ch, double t, double h, DateTime time)> _dataQueue
            = new Queue<(int, double, double, DateTime)>();
        private readonly object _lock = new object();

        // ── 波形数据（每个通道保留最近200个点，GDI+自绘） ──
        private readonly Dictionary<int, List<float>> _waveData = new Dictionary<int, List<float>>();
        private const int MAX_WAVE_POINTS = 200;
        private readonly Color[] _waveColors = { Color.Cyan, Color.Orange, Color.Lime, Color.Magenta };

        // ── 报警阈值 ──
        private const double TEMP_HIGH = 35.0;
        private const double TEMP_LOW  = 0.0;
        private const double HUMID_HIGH = 85.0;

        // ── 采样计数器（每N条存入数据库一次） ──
        private int _sampleCount = 0;
        private const int DB_BATCH = 5;

        public FormMain()
        {
            Text = "多通道温湿度监控上位机 — SensorMonitor v1.0";
            Size = new Size(1280, 820);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;
            Font = new Font("Microsoft YaHei", 9F);

            BuildUI();
            InitWaveData();

            _db = new DbManager("sensor_data.db");
            _simulator = new SerialSimulator();
            _simulator.OnDataReceived += OnSensorData;

            _tcpServer = new TcpServerHelper();
            _tcpServer.OnStatusChanged += msg => AppendLog(msg);
        }

        private void InitWaveData()
        {
            for (int ch = 1; ch <= 4; ch++)
                _waveData[ch] = new List<float>();
        }

        // ════════════════════════════════════════════
        //  UI 构建
        // ════════════════════════════════════════════
        private void BuildUI()
        {
            int pad = 12;

            // 顶部工具栏
            var toolbar = new Panel
            {
                Dock = DockStyle.Top, Height = 50,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            _btnStart = NewBtn("▶ 开始采集", Color.FromArgb(0, 150, 100), 10);
            _btnStop  = NewBtn("■ 停止采集", Color.FromArgb(180, 60, 60), 130);
            _btnHistory = NewBtn("📋 历史查询", Color.FromArgb(70, 120, 180), 250);
            _btnExport  = NewBtn("📤 导出CSV", Color.FromArgb(70, 120, 180), 370);
            _btnTcp     = NewBtn("🌐 启动TCP", Color.FromArgb(120, 80, 160), 490);

            _lblStatus = new Label
            {
                Text = "● 就绪", ForeColor = Color.Gray,
                Location = new Point(630, 14), AutoSize = true,
                Font = new Font("Microsoft YaHei", 10, FontStyle.Bold)
            };

            toolbar.Controls.AddRange(new Control[] {
                _btnStart, _btnStop, _btnHistory, _btnExport, _btnTcp, _lblStatus
            });

            // 左侧：仪表盘面板（2x2）
            var gaugePanel = new Panel
            {
                Dock = DockStyle.Left, Width = 400,
                BackColor = Color.FromArgb(35, 35, 40),
                Padding = new Padding(pad)
            };

            string[] titles = { "通道1 温度", "通道2 温度", "通道3 温度", "通道4 温度" };
            for (int i = 0; i < 4; i++)
            {
                int row = i / 2, col = i % 2;
                int gx = pad + col * 190, gy = pad + row * 200;

                _gauges[i] = new CircularGauge
                {
                    Location = new Point(gx, gy),
                    Size = new Size(170, 170),
                };
                _gauges[i].SetRange(-10, 60, "°C", Color.Red);

                _gaugeLabels[i] = new Label
                {
                    Text = titles[i],
                    ForeColor = Color.LightGray,
                    Location = new Point(gx + 25, gy + 170),
                    AutoSize = true,
                    Font = new Font("Microsoft YaHei", 9)
                };

                gaugePanel.Controls.Add(_gauges[i]);
                gaugePanel.Controls.Add(_gaugeLabels[i]);
            }

            // 右侧：GDI+自绘波形 + 日志
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill, Padding = new Padding(pad)
            };

            _wavePanel = new PictureBox
            {
                Dock = DockStyle.Top, Height = 420,
                BackColor = Color.FromArgb(20, 22, 26)
            };
            _wavePanel.Paint += WavePanel_Paint;

            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 25, 28),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };
            _logBox.AppendText("=== SensorMonitor 启动 ===\r\n");
            _logBox.AppendText("亮点：Queue解耦 | GDI+自绘波形+仪表盘 | SQLite存储 | TCP远程 | 阈值报警\r\n\r\n");

            rightPanel.Controls.Add(_logBox);
            rightPanel.Controls.Add(_wavePanel);

            Controls.Add(rightPanel);
            Controls.Add(gaugePanel);
            Controls.Add(toolbar);

            // 事件绑定
            _btnStart.Click  += (s, e) => StartAcquisition();
            _btnStop.Click   += (s, e) => StopAcquisition();
            _btnHistory.Click += (s, e) => ShowHistory();
            _btnExport.Click  += (s, e) => ExportData();
            _btnTcp.Click     += (s, e) => ToggleTcp();

            // 报警定时器
            _alarmTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _alarmTimer.Tick += (s, e) =>
            {
                foreach (var g in _gauges) g.Alarm = !g.Alarm;
            };

            // UI 刷新定时器：每100ms从队列消费数据
            var uiTimer = new System.Windows.Forms.Timer { Interval = 100 };
            uiTimer.Tick += (s, e) => ConsumeDataQueue();
            uiTimer.Start();

            FormClosing += (s, e) =>
            {
                _simulator?.Stop();
                _tcpServer?.Stop();
                _alarmTimer?.Stop();
            };
        }

        // ════════════════════════════════════════════
        //  GDI+ 自绘波形（替换Chart控件，面试超级亮点）
        // ════════════════════════════════════════════
        private void WavePanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = _wavePanel.Width, h = _wavePanel.Height;
            int marginLeft = 50, marginRight = 20, marginTop = 20, marginBottom = 40;
            int plotW = w - marginLeft - marginRight;
            int plotH = h - marginTop - marginBottom;

            // 背景
            g.Clear(Color.FromArgb(20, 22, 26));

            // 绘制网格线
            using (var gridPen = new Pen(Color.FromArgb(40, 42, 48), 1))
            {
                for (int i = 0; i <= 5; i++)
                {
                    int y = marginTop + plotH * i / 5;
                    g.DrawLine(gridPen, marginLeft, y, marginLeft + plotW, y);
                }
                for (int i = 0; i <= 8; i++)
                {
                    int x = marginLeft + plotW * i / 8;
                    g.DrawLine(gridPen, x, marginTop, x, marginTop + plotH);
                }
            }

            // Y轴标签（温度 -10 ~ 60）
            using (var font = new Font("Consolas", 8))
            using (var brush = new SolidBrush(Color.Gray))
            {
                for (int i = 0; i <= 5; i++)
                {
                    double val = -10 + 70.0 * i / 5;
                    int y = marginTop + plotH - plotH * i / 5;
                    g.DrawString($"{val:F0}°C", font, brush, 2, y - 8);
                }
            }

            // 绘制4通道曲线
            for (int ch = 1; ch <= 4; ch++)
            {
                var points = _waveData[ch];
                if (points.Count < 2) continue;

                using (var pen = new Pen(_waveColors[ch - 1], 2))
                {
                    // 计算比例：X轴按点数均匀分布，Y轴映射到温度范围
                    float tempMin = -10f, tempMax = 60f;
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        float x1 = marginLeft + (float)i / (MAX_WAVE_POINTS - 1) * plotW;
                        float y1 = marginTop + plotH - (points[i] - tempMin) / (tempMax - tempMin) * plotH;
                        float x2 = marginLeft + (float)(i + 1) / (MAX_WAVE_POINTS - 1) * plotW;
                        float y2 = marginTop + plotH - (points[i + 1] - tempMin) / (tempMax - tempMin) * plotH;
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }
            }

            // 图例
            int legendX = marginLeft + 10, legendY = marginTop + 5;
            for (int ch = 1; ch <= 4; ch++)
            {
                using (var brush = new SolidBrush(_waveColors[ch - 1]))
                using (var font = new Font("Microsoft YaHei", 8))
                {
                    g.FillRectangle(brush, legendX, legendY, 12, 3);
                    g.DrawString($"CH{ch}", font, Brushes.LightGray, legendX + 16, legendY - 6);
                    legendX += 60;
                }
            }
        }

        // ════════════════════════════════════════════
        //  数据采集控制
        // ════════════════════════════════════════════
        private void StartAcquisition()
        {
            _simulator.Start(600);
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _lblStatus.Text = "● 采集中";
            _lblStatus.ForeColor = Color.Lime;
            AppendLog("开始数据采集（模拟4通道）");
        }

        private void StopAcquisition()
        {
            _simulator.Stop();
            _btnStart.Enabled = true;
            _btnStop.Enabled = false;
            _lblStatus.Text = "● 已停止";
            _lblStatus.ForeColor = Color.Gray;
            AppendLog("停止数据采集");
        }

        private void OnSensorData(int ch, double temp, double hum)
        {
            lock (_lock)
            {
                _dataQueue.Enqueue((ch, temp, hum, DateTime.Now));
            }
        }

        private void ConsumeDataQueue()
        {
            List<(int ch, double t, double h, DateTime time)> batch;
            lock (_lock)
            {
                if (_dataQueue.Count == 0) return;
                batch = _dataQueue.ToList();
                _dataQueue.Clear();
            }

            bool anyAlarm = false;
            foreach (var d in batch)
            {
                int idx = d.ch - 1;
                if (idx >= 0 && idx < 4)
                {
                    _gauges[idx].GaugeValue = (float)d.t;
                    _gaugeLabels[idx].Text = $"通道{d.ch}  温度:{d.t}°C  湿度:{d.h}%";
                }

                // 波形数据追加
                if (_waveData.TryGetValue(d.ch, out var list))
                {
                    list.Add((float)d.t);
                    if (list.Count > MAX_WAVE_POINTS)
                        list.RemoveAt(0);
                }

                if (d.t > TEMP_HIGH || d.t < TEMP_LOW || d.h > HUMID_HIGH)
                    anyAlarm = true;

                _sampleCount++;
                if (_sampleCount >= DB_BATCH)
                {
                    _sampleCount = 0;
                    _db.Insert(d.ch, d.t, d.h);
                }
            }

            // 触发波形重绘
            _wavePanel.Invalidate();

            // 报警控制
            if (anyAlarm && !_alarmTimer.Enabled)
            {
                _alarmTimer.Start();
                AppendLog("⚠ 报警：温度或湿度超限！");
            }
            else if (!anyAlarm && _alarmTimer.Enabled)
            {
                _alarmTimer.Stop();
                foreach (var g in _gauges) g.Alarm = false;
            }
        }

        // ════════════════════════════════════════════
        //  历史查询与导出
        // ════════════════════════════════════════════
        private void ShowHistory()
        {
            var from = DateTime.Now.AddHours(-1);
            var to   = DateTime.Now;
            DataTable dt = _db.Query(from, to);

            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("近1小时内无数据记录。", "历史查询",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var form = new Form
            {
                Text = $"历史数据 ({from:HH:mm} ~ {to:HH:mm})  共{dt.Rows.Count}条",
                Size = new Size(700, 450), StartPosition = FormStartPosition.CenterParent
            };
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill, DataSource = dt,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false, ReadOnly = true
            };
            form.Controls.Add(dgv);
            form.ShowDialog(this);
        }

        private void ExportData()
        {
            var from = DateTime.Now.AddHours(-2);
            var to   = DateTime.Now;
            DataTable dt = _db.Query(from, to);

            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("近2小时内无数据可导出。", "导出",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string filePath = $"sensor_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            _db.ExportCsv(dt, filePath);
            AppendLog($"数据已导出: {filePath} ({dt.Rows.Count}条)");
            MessageBox.Show(
                $"导出成功！\r\n文件：{System.IO.Path.GetFullPath(filePath)}\r\n共 {dt.Rows.Count} 条记录",
                "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ════════════════════════════════════════════
        //  TCP 远程服务
        // ════════════════════════════════════════════
        private void ToggleTcp()
        {
            if (_btnTcp.Text.Contains("启动"))
            {
                _tcpServer.Start(8899);
                _btnTcp.Text = "🌐 停止TCP";
                _btnTcp.BackColor = Color.FromArgb(180, 60, 60);
            }
            else
            {
                _tcpServer.Stop();
                _btnTcp.Text = "🌐 启动TCP";
                _btnTcp.BackColor = Color.FromArgb(120, 80, 160);
            }
        }

        // ════════════════════════════════════════════
        //  辅助方法
        // ════════════════════════════════════════════
        private void AppendLog(string msg)
        {
            if (_logBox.InvokeRequired)
            {
                _logBox.Invoke(new Action<string>(AppendLog), msg);
                return;
            }
            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
            _logBox.ScrollToCaret();
        }

        private Button NewBtn(string text, Color bg, int x)
        {
            return new Button
            {
                Text = text, BackColor = bg, ForeColor = Color.White,
                Location = new Point(x, 10), Size = new Size(110, 30),
                FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei", 9),
                Cursor = Cursors.Hand
            };
        }
    }
}
