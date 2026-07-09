using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SensorMonitor
{
    /// <summary>
    /// 自定义圆环仪表盘控件 — GDI+ 手绘，面试亮点
    /// 支持温度（-10~60℃）和湿度（0~100%）两种量程
    /// </summary>
    public class CircularGauge : Control
    {
        private float _value = 0;
        private float _min = -10, _max = 60;
        private string _unit = "°C";
        private Color _needleColor = Color.Red;
        private bool _alarm = false;

        public CircularGauge()
        {
            DoubleBuffered = true;
            Size = new Size(160, 160);
        }

        public float GaugeValue
        {
            get => _value;
            set { _value = value; Invalidate(); }
        }

        /// <summary>设置量程（温度：-10~60, 湿度：0~100）</summary>
        public void SetRange(float min, float max, string unit, Color needle)
        {
            _min = min; _max = max; _unit = unit; _needleColor = needle; Invalidate();
        }

        /// <summary>报警闪烁控制</summary>
        public bool Alarm { get => _alarm; set { _alarm = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Width, h = Height;
            int size = Math.Min(w, h) - 10;
            int x = (w - size) / 2, y = (h - size) / 2;
            var rect = new Rectangle(x, y, size, size);

            // 背景圆环
            using (var bgPen = new Pen(Color.FromArgb(60, 60, 60), 12))
                g.DrawArc(bgPen, rect, 135, 270);

            // 前景圆环（按比例）
            float angle = 270 * (_value - _min) / (_max - _min);
            Color arcColor = _alarm ? Color.Red : Color.FromArgb(0, 200, 100);
            using (var fgPen = new Pen(arcColor, 12))
                g.DrawArc(fgPen, rect, 135, angle);

            // 中心数值
            string txt = $"{_value:F1}{_unit}";
            using (var font = new Font("Segoe UI", 16, FontStyle.Bold))
            {
                var txtSize = g.MeasureString(txt, font);
                g.DrawString(txt, font, Brushes.White,
                    (w - txtSize.Width) / 2, (h - txtSize.Height) / 2 - 10);
            }

            // 指针
            float rad = (float)((135 + angle) * Math.PI / 180);
            int cx = w / 2, cy = h / 2;
            int r = size / 2 - 25;
            int px = cx + (int)(r * Math.Cos(rad));
            int py = cy + (int)(r * Math.Sin(rad));
            using (var pen = new Pen(_needleColor, 3))
                g.DrawLine(pen, cx, cy, px, py);
        }
    }
}
