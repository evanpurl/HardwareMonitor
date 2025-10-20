using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using LibreHardwareMonitor.Hardware;
using Timer = System.Windows.Forms.Timer;

namespace HardwareMonitor
{
    public partial class Form1 : Form
    {
        private readonly Timer _timer;
        private readonly Label _label;
        private readonly Computer _computer;
        private ISensor? _cpuSensor;
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly ToolStripMenuItem _startupMenuItem;

        private const string StartupRegKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private readonly string _appName = "HardwareMonitor";

        public Form1()
        {
            InitializeComponent();

            // Transparent overlay setup
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(20, 20);
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            TransparencyKey = BackColor;

            _label = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 12, FontStyle.Bold),
                ForeColor = Color.Lime,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(_label);

            // Hardware setup
            _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
            _computer.Open();
            DetectCpuSensor();

            // Timer setup
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += (s, e) => UpdateTemps();
            _timer.Start();

            // Mouse drag support
            MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) DragMove(); };
            _label.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) DragMove(); };

            // System tray setup
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Exit", null, (s, e) => Application.Exit());

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Hardware Monitor",
                ContextMenuStrip = _trayMenu
            };

            UpdateTemps();
        }

        private void DetectCpuSensor()
        {
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    _cpuSensor = hardware.Sensors
                        .FirstOrDefault(s => s.SensorType == SensorType.Temperature &&
                                             (s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                                              s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                              s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                                              s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase)));
                }
            }
        }

        private void UpdateTemps()
        {
            float? cpuTemp = null, gpuTemp = null;

            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();

                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    if (_cpuSensor?.Value is float cVal)
                        cpuTemp = cVal;
                }

                if (hardware.HardwareType == HardwareType.GpuNvidia ||
                    hardware.HardwareType == HardwareType.GpuAmd ||
                    hardware.HardwareType == HardwareType.GpuIntel)
                {
                    var gpuSensor = hardware.Sensors
                        .FirstOrDefault(s => s.SensorType == SensorType.Temperature &&
                                             (s.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
                                              s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)));

                    if (gpuSensor?.Value is float gVal)
                        gpuTemp = gVal;
                }
            }

            _label.Text = $"CPU: {(cpuTemp?.ToString("0") ?? "--")}°C | GPU: {(gpuTemp?.ToString("0") ?? "--")}°C";
            AutoResizeToLabel();
        }

        private void AutoResizeToLabel()
        {
            using var g = CreateGraphics();
            var size = g.MeasureString(_label.Text, _label.Font);
            Size = new Size((int)Math.Ceiling(size.Width) + 20, (int)Math.Ceiling(size.Height) + 10);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            base.OnFormClosing(e);
        }

        // Drag support
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        private void DragMove()
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }
    }
}
