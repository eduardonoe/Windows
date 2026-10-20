// ==============================================================================
// NetMonitor - Monitor de Rede Ultraleve para Barra de Tarefas (Windows 11)
// Autor: Eduardo Noé (eduardonoe)
// Version: 1.0.5
//
// v1.0.5 changelog:
// - Checkmarks visíveis: habilitado ShowCheckMargin nos menus e submenus para
//   exibição nítida dos tickets de seleção (✓).
// - Setas posicionadas à direita por padrão: [ 823.6 Mbps ▲ ].
// - Seleção fixa de unidades (Automático, Kbps, Mbps, Gbps).
// - Correção definitiva da largura da unidade para nunca cortar a letra 's' em "Mbps".
// - Opção de Travar Posição (Lock Position) para evitar arrasto acidental na barra.
// - Janela visual completa de Configurações / Customização da UI:
//     * Escolha da ordem (Setas à direita vs Setas à esquerda).
//     * Templates prontos de ícones (Triângulos, Chevrons, Setas finas, etc.).
//     * Campo para inserir qualquer caractere Unicode ou código Hex (ex: U+25B2).
//     * Prévia ao vivo da aparência configurada.
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NetMonitor
{
    public class NetMonitorApp : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int awareness);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int GWLP_HWNDPARENT = -8;

        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_POPUP = unchecked((int)0x80000000);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        private const string REG_KEY_APP = @"Software\NetMonitor";
        private const string REG_KEY_RUN = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "NetMonitor";

        // Layout Dimensions
        private const int WIDGET_WIDTH = 106;
        private const int WIDGET_HEIGHT = 38;

        // Visual Colors (Windows 11 Dark Taskbar Theme)
        private static readonly Color BG_COLOR = Color.FromArgb(28, 28, 28);
        private static readonly Color ARROW_COLOR = Color.FromArgb(225, 225, 225);
        private static readonly Color NUM_COLOR = Color.FromArgb(255, 255, 255);
        private static readonly Color UNIT_COLOR = Color.FromArgb(195, 195, 195);

        // Fonts
        private Font fontNum;
        private Font fontUnit;
        private Font fontUnicode;

        // Formatting alignment
        private StringFormat sfRight;
        private StringFormat sfLeft;
        private StringFormat sfCenter;

        // Configuration state
        private string selectedNicId = "Auto";
        private string activeNicDisplayName = "Auto";
        private string currentLanguage = "pt";
        private string unitMode = "Auto";             // "Auto", "Kbps", "Mbps", "Gbps"
        private bool arrowOnRight = true;             // Seta à direita por padrão
        private bool isLocked = false;                // Trava de posição
        private string iconTemplate = "VectorTriangle"; // "VectorTriangle", "VectorChevron", "Unicode"
        private string customUpChar = "▲";
        private string customDownChar = "▼";

        // Network measurement state
        private Timer updateTimer;
        private long lastBytesIn = -1;
        private long lastBytesOut = -1;
        private DateTime lastMeasurementTime = DateTime.MinValue;

        // Current display values
        private string strUpNum = "0.0";
        private string strUpUnit = "Mbps";
        private string strDownNum = "0.0";
        private string strDownUnit = "Mbps";

        // UI Components
        private ContextMenuStrip contextMenu;
        private NotifyIcon trayIcon;

        public NetMonitorApp()
        {
            InitializeComponent();
            LoadSettings();
            SetupFonts();
            BuildContextMenu();
            SetupTimer();
            PositionWidget();
            BindToTaskbarOwner();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style = WS_POPUP;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(WIDGET_WIDTH, WIDGET_HEIGHT);
            this.BackColor = BG_COLOR;
            this.DoubleBuffered = true;
            this.Text = "NetMonitor";

            this.trayIcon = new NotifyIcon();
            this.trayIcon.Text = "NetMonitor";
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                this.trayIcon.Icon = this.Icon;
            }
            catch
            {
                this.trayIcon.Icon = SystemIcons.Application;
            }
            this.trayIcon.Visible = true;

            this.MouseDown += NetMonitorApp_MouseDown;
            this.MouseUp += NetMonitorApp_MouseUp;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        private void SetupFonts()
        {
            string fontName = "Segoe UI";
            using (Font test = new Font("Segoe UI Variable Display", 10f))
            {
                if (test.Name.IndexOf("Variable", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fontName = "Segoe UI Variable Display";
                }
            }

            // Fonte regular mais fina
            fontNum = new Font(fontName, 10.5f, FontStyle.Regular);
            fontUnit = new Font(fontName, 9.0f, FontStyle.Regular);
            fontUnicode = new Font("Segoe UI Symbol", 9.5f, FontStyle.Regular);

            sfRight = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center
            };

            sfLeft = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center
            };

            sfCenter = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
        }

        private void SetupTimer()
        {
            updateTimer = new Timer();
            updateTimer.Interval = 1000;
            updateTimer.Tick += (s, e) =>
            {
                UpdateTraffic();
                SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            };
            updateTimer.Start();
            UpdateTraffic();
        }

        private void BindToTaskbarOwner()
        {
            try
            {
                IntPtr trayWnd = FindWindow("Shell_TrayWnd", null);
                if (trayWnd != IntPtr.Zero)
                {
                    SetWindowLongPtr(this.Handle, GWLP_HWNDPARENT, trayWnd);
                }
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_WINDOWPOSCHANGING)
            {
                WINDOWPOS pos = (WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS));
                pos.hwndInsertAfter = HWND_TOPMOST;
                Marshal.StructureToPtr(pos, m.LParam, false);
            }
            base.WndProc(ref m);
        }

        public void LoadSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_KEY_APP))
                {
                    if (key != null)
                    {
                        object valNic = key.GetValue("SelectedAdapter");
                        if (valNic != null) selectedNicId = valNic.ToString();

                        object valLang = key.GetValue("Language");
                        if (valLang != null) currentLanguage = valLang.ToString();
                        else currentLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase) ? "pt" : "en";

                        object valUnit = key.GetValue("UnitMode");
                        if (valUnit != null) unitMode = valUnit.ToString();

                        object valArrow = key.GetValue("ArrowOnRight");
                        if (valArrow != null) arrowOnRight = Convert.ToInt32(valArrow) == 1;

                        object valLock = key.GetValue("IsLocked");
                        if (valLock != null) isLocked = Convert.ToInt32(valLock) == 1;

                        object valTpl = key.GetValue("IconTemplate");
                        if (valTpl != null) iconTemplate = valTpl.ToString();

                        object valUp = key.GetValue("CustomUpChar");
                        if (valUp != null) customUpChar = valUp.ToString();

                        object valDown = key.GetValue("CustomDownChar");
                        if (valDown != null) customDownChar = valDown.ToString();
                    }
                }
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REG_KEY_APP))
                {
                    if (key != null)
                    {
                        key.SetValue("SelectedAdapter", selectedNicId);
                        key.SetValue("Language", currentLanguage);
                        key.SetValue("UnitMode", unitMode);
                        key.SetValue("ArrowOnRight", arrowOnRight ? 1 : 0);
                        key.SetValue("IsLocked", isLocked ? 1 : 0);
                        key.SetValue("IconTemplate", iconTemplate);
                        key.SetValue("CustomUpChar", customUpChar);
                        key.SetValue("CustomDownChar", customDownChar);
                        key.SetValue("CustomX", this.Location.X);
                        key.SetValue("CustomY", this.Location.Y);
                    }
                }
            }
            catch { }
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_KEY_RUN, false))
                {
                    if (key != null)
                    {
                        return key.GetValue(APP_NAME) != null;
                    }
                }
            }
            catch { }
            return false;
        }

        private void ToggleAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_KEY_RUN, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue(APP_NAME, "\"" + Application.ExecutablePath + "\"");
                        }
                        else
                        {
                            key.DeleteValue(APP_NAME, false);
                        }
                    }
                }
            }
            catch { }
        }

        public void PositionWidget(bool forceReset = false)
        {
            int posX = -1;
            int posY = -1;

            if (!forceReset)
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_KEY_APP))
                    {
                        if (key != null)
                        {
                            object xVal = key.GetValue("CustomX");
                            object yVal = key.GetValue("CustomY");
                            if (xVal != null && yVal != null)
                            {
                                posX = Convert.ToInt32(xVal);
                                posY = Convert.ToInt32(yVal);
                            }
                        }
                    }
                }
                catch { }
            }

            Screen primary = Screen.PrimaryScreen;
            int taskbarTop = primary.WorkingArea.Bottom;
            int taskbarH = primary.Bounds.Height - taskbarTop;
            if (taskbarH <= 0) taskbarH = 48;

            int calculatedY = taskbarTop + (taskbarH - WIDGET_HEIGHT) / 2;

            if (posX <= 0 || posY <= 0 || forceReset)
            {
                IntPtr trayWnd = FindWindow("Shell_TrayWnd", null);
                if (trayWnd != IntPtr.Zero)
                {
                    IntPtr notifyWnd = FindWindowEx(trayWnd, IntPtr.Zero, "TrayNotifyWnd", null);
                    if (notifyWnd != IntPtr.Zero)
                    {
                        RECT notifyRect;
                        GetWindowRect(notifyWnd, out notifyRect);
                        posX = notifyRect.Left - WIDGET_WIDTH - 6;
                    }
                    else
                    {
                        posX = primary.Bounds.Width - 360;
                    }
                }
                else
                {
                    posX = primary.Bounds.Width - 360;
                }
                posY = calculatedY;
            }

            if (posY < taskbarTop || posY > primary.Bounds.Height - WIDGET_HEIGHT)
            {
                posY = calculatedY;
            }

            this.Location = new Point(posX, posY);
            SetWindowPos(this.Handle, HWND_TOPMOST, posX, posY, WIDGET_WIDTH, WIDGET_HEIGHT, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private NetworkInterface GetTargetNic()
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            if (!string.Equals(selectedNicId, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var nic in nics)
                {
                    if (nic.Id == selectedNicId || nic.Name == selectedNicId)
                    {
                        activeNicDisplayName = nic.Name;
                        return nic;
                    }
                }
            }

            NetworkInterface bestNic = null;
            long maxBytes = -1;

            foreach (var nic in nics)
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                var ipProps = nic.GetIPProperties();
                bool hasGateway = ipProps.GatewayAddresses != null && ipProps.GatewayAddresses.Count > 0;

                try
                {
                    var stats = nic.GetIPv4Statistics();
                    long totalTraffic = stats.BytesReceived + stats.BytesSent;

                    if (hasGateway)
                    {
                        activeNicDisplayName = nic.Name;
                        return nic;
                    }

                    if (totalTraffic > maxBytes)
                    {
                        maxBytes = totalTraffic;
                        bestNic = nic;
                    }
                }
                catch { }
            }

            if (bestNic != null)
                activeNicDisplayName = bestNic.Name;
            else
                activeNicDisplayName = currentLanguage == "pt" ? "Nenhum" : "None";

            return bestNic;
        }

        private void UpdateTraffic()
        {
            NetworkInterface nic = GetTargetNic();
            if (nic == null)
            {
                strUpNum = "0.0";
                strUpUnit = (unitMode == "Auto") ? "Mbps" : unitMode;
                strDownNum = "0.0";
                strDownUnit = strUpUnit;
                this.Invalidate();
                return;
            }

            try
            {
                var stats = nic.GetIPv4Statistics();
                long currentIn = stats.BytesReceived;
                long currentOut = stats.BytesSent;
                DateTime now = DateTime.UtcNow;

                if (lastBytesIn >= 0 && lastMeasurementTime != DateTime.MinValue)
                {
                    double elapsed = (now - lastMeasurementTime).TotalSeconds;
                    if (elapsed > 0.1)
                    {
                        long diffIn = currentIn - lastBytesIn;
                        long diffOut = currentOut - lastBytesOut;

                        if (diffIn < 0) diffIn = 0;
                        if (diffOut < 0) diffOut = 0;

                        double bitsInPerSec = (diffIn * 8.0) / elapsed;
                        double bitsOutPerSec = (diffOut * 8.0) / elapsed;

                        FormatSpeed(bitsOutPerSec, out strUpNum, out strUpUnit);
                        FormatSpeed(bitsInPerSec, out strDownNum, out strDownUnit);
                    }
                }

                lastBytesIn = currentIn;
                lastBytesOut = currentOut;
                lastMeasurementTime = now;
            }
            catch
            {
                strUpNum = "0.0";
                strUpUnit = (unitMode == "Auto") ? "Mbps" : unitMode;
                strDownNum = "0.0";
                strDownUnit = strUpUnit;
            }

            this.Invalidate();
        }

        private void FormatSpeed(double bitsPerSec, out string num, out string unit)
        {
            if (string.Equals(unitMode, "Gbps", StringComparison.OrdinalIgnoreCase))
            {
                double gbps = bitsPerSec / (1000.0 * 1000.0 * 1000.0);
                num = gbps.ToString("0.00", CultureInfo.InvariantCulture);
                unit = "Gbps";
            }
            else if (string.Equals(unitMode, "Mbps", StringComparison.OrdinalIgnoreCase))
            {
                double mbps = bitsPerSec / (1000.0 * 1000.0);
                num = mbps.ToString("0.0", CultureInfo.InvariantCulture);
                unit = "Mbps";
            }
            else if (string.Equals(unitMode, "Kbps", StringComparison.OrdinalIgnoreCase))
            {
                double kbps = bitsPerSec / 1000.0;
                num = kbps.ToString("0", CultureInfo.InvariantCulture);
                unit = "Kbps";
            }
            else // Auto
            {
                if (bitsPerSec >= 1000.0 * 1000.0 * 1000.0)
                {
                    double gbps = bitsPerSec / (1000.0 * 1000.0 * 1000.0);
                    num = gbps.ToString("0.00", CultureInfo.InvariantCulture);
                    unit = "Gbps";
                }
                else if (bitsPerSec >= 1000.0 * 1000.0)
                {
                    double mbps = bitsPerSec / (1000.0 * 1000.0);
                    num = mbps.ToString("0.0", CultureInfo.InvariantCulture);
                    unit = "Mbps";
                }
                else if (bitsPerSec >= 1000.0)
                {
                    double kbps = bitsPerSec / 1000.0;
                    num = kbps.ToString("0", CultureInfo.InvariantCulture);
                    unit = "Kbps";
                }
                else
                {
                    num = "0.0";
                    unit = "Mbps";
                }
            }
        }

        public static string ParseUnicodeString(string input, string fallback)
        {
            if (string.IsNullOrWhiteSpace(input)) return fallback;
            input = input.Trim();
            if (input.Length == 1 || (input.Length == 2 && char.IsSurrogatePair(input, 0)))
                return input;

            string hex = input;
            if (hex.StartsWith("U+", StringComparison.OrdinalIgnoreCase) ||
                hex.StartsWith("\\u", StringComparison.OrdinalIgnoreCase) ||
                hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex.Substring(2);
            }

            int codePoint;
            if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codePoint))
            {
                try
                {
                    return char.ConvertFromUtf32(codePoint);
                }
                catch { }
            }

            return input;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            g.Clear(BG_COLOR);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (arrowOnRight)
            {
                // ==============================================================
                // LAYOUT: SETAS À DIREITA -> [ 823.6 Mbps  ▲ ]
                // ==============================================================
                // 1. Números justificados à direita (X: 2 a 48)
                RectangleF rUpNum = new RectangleF(2, 1, 46, 17);
                RectangleF rDownNum = new RectangleF(2, 19, 46, 17);

                using (Brush bNum = new SolidBrush(NUM_COLOR))
                {
                    g.DrawString(strUpNum, fontNum, bNum, rUpNum, sfRight);
                    g.DrawString(strDownNum, fontNum, bNum, rDownNum, sfRight);
                }

                // 2. Unidade (X: 50 a 88 - espaço amplo para NUNCA cortar a letra 's')
                RectangleF rUpUnit = new RectangleF(50, 1, 38, 17);
                RectangleF rDownUnit = new RectangleF(50, 19, 38, 17);

                using (Brush bUnit = new SolidBrush(UNIT_COLOR))
                {
                    g.DrawString(strUpUnit, fontUnit, bUnit, rUpUnit, sfLeft);
                    g.DrawString(strDownUnit, fontUnit, bUnit, rDownUnit, sfLeft);
                }

                // 3. Setas à direita
                DrawArrowIcon(g, 97.0f, true);
            }
            else
            {
                // ==============================================================
                // LAYOUT: SETAS À ESQUERDA -> [ ▲  823.6 Mbps ]
                // ==============================================================
                // 1. Setas à esquerda
                DrawArrowIcon(g, 10.0f, false);

                // 2. Números justificados à direita (X: 18 a 60)
                RectangleF rUpNum = new RectangleF(18, 1, 42, 17);
                RectangleF rDownNum = new RectangleF(18, 19, 42, 17);

                using (Brush bNum = new SolidBrush(NUM_COLOR))
                {
                    g.DrawString(strUpNum, fontNum, bNum, rUpNum, sfRight);
                    g.DrawString(strDownNum, fontNum, bNum, rDownNum, sfRight);
                }

                // 3. Unidade (X: 62 a 104)
                RectangleF rUpUnit = new RectangleF(62, 1, 40, 17);
                RectangleF rDownUnit = new RectangleF(62, 19, 40, 17);

                using (Brush bUnit = new SolidBrush(UNIT_COLOR))
                {
                    g.DrawString(strUpUnit, fontUnit, bUnit, rUpUnit, sfLeft);
                    g.DrawString(strDownUnit, fontUnit, bUnit, rDownUnit, sfLeft);
                }
            }
        }

        private void DrawArrowIcon(Graphics g, float centerX, bool isRight)
        {
            using (Brush brushArrow = new SolidBrush(ARROW_COLOR))
            {
                if (iconTemplate == "VectorTriangle")
                {
                    // Triângulos vetoriais refinados (+15%)
                    PointF[] upArrow = new PointF[] {
                        new PointF(centerX, 5.0f),
                        new PointF(centerX - 4.0f, 10.2f),
                        new PointF(centerX + 4.0f, 10.2f)
                    };
                    g.FillPolygon(brushArrow, upArrow);

                    PointF[] downArrow = new PointF[] {
                        new PointF(centerX, 31.5f),
                        new PointF(centerX - 4.0f, 26.3f),
                        new PointF(centerX + 4.0f, 26.3f)
                    };
                    g.FillPolygon(brushArrow, downArrow);
                }
                else if (iconTemplate == "VectorChevron")
                {
                    // Chevrons traço suave
                    using (Pen penArrow = new Pen(ARROW_COLOR, 1.4f))
                    {
                        g.DrawLines(penArrow, new PointF[] {
                            new PointF(centerX - 3.8f, 9.5f),
                            new PointF(centerX, 5.5f),
                            new PointF(centerX + 3.8f, 9.5f)
                        });
                        g.DrawLines(penArrow, new PointF[] {
                            new PointF(centerX - 3.8f, 27.0f),
                            new PointF(centerX, 31.0f),
                            new PointF(centerX + 3.8f, 27.0f)
                        });
                    }
                }
                else
                {
                    // Caracteres Unicode customizados (ou mapa de caracteres)
                    string upCh = ParseUnicodeString(customUpChar, "▲");
                    string downCh = ParseUnicodeString(customDownChar, "▼");

                    RectangleF rUp = new RectangleF(centerX - 8, 1, 16, 17);
                    RectangleF rDown = new RectangleF(centerX - 8, 19, 16, 17);

                    g.DrawString(upCh, fontUnicode, brushArrow, rUp, sfCenter);
                    g.DrawString(downCh, fontUnicode, brushArrow, rDown, sfCenter);
                }
            }
        }

        private void BuildContextMenu()
        {
            bool isPt = currentLanguage == "pt";
            contextMenu = new ContextMenuStrip();
            // CHECKMARKS NÍTIDOS E VISÍVEIS
            contextMenu.ShowCheckMargin = true;
            contextMenu.ShowImageMargin = false;

            // 0. Configurações / Settings (Item de destaque)
            ToolStripMenuItem mnuSettings = new ToolStripMenuItem(isPt ? "Configurações..." : "Settings...");
            mnuSettings.Font = new Font(mnuSettings.Font, FontStyle.Bold);
            mnuSettings.Click += (s, e) => OpenSettingsDialog();
            contextMenu.Items.Add(mnuSettings);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 1. Placa de Rede / Network Adapter
            ToolStripMenuItem mnuAdapters = new ToolStripMenuItem(isPt ? "Placa de Rede" : "Network Adapter");
            EnableCheckMargin(mnuAdapters);
            PopulateAdaptersMenu(mnuAdapters);
            contextMenu.Items.Add(mnuAdapters);

            // 2. Unidade / Unit
            ToolStripMenuItem mnuUnits = new ToolStripMenuItem(isPt ? "Unidade" : "Unit");
            EnableCheckMargin(mnuUnits);
            PopulateUnitsMenu(mnuUnits);
            contextMenu.Items.Add(mnuUnits);

            // 3. Posição das Setas / Layout
            ToolStripMenuItem mnuLayout = new ToolStripMenuItem(isPt ? "Posição das Setas" : "Arrow Position");
            EnableCheckMargin(mnuLayout);

            ToolStripMenuItem itmRight = new ToolStripMenuItem(isPt ? "Setas à Direita (823 Mbps ▲)" : "Arrows on Right (823 Mbps ▲)");
            itmRight.Checked = arrowOnRight;
            itmRight.Click += (s, e) => { arrowOnRight = true; SaveSettings(); BuildContextMenu(); this.Invalidate(); };

            ToolStripMenuItem itmLeft = new ToolStripMenuItem(isPt ? "Setas à Esquerda (▲ 823 Mbps)" : "Arrows on Left (▲ 823 Mbps)");
            itmLeft.Checked = !arrowOnRight;
            itmLeft.Click += (s, e) => { arrowOnRight = false; SaveSettings(); BuildContextMenu(); this.Invalidate(); };

            mnuLayout.DropDownItems.Add(itmRight);
            mnuLayout.DropDownItems.Add(itmLeft);
            contextMenu.Items.Add(mnuLayout);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 4. Travar Posição / Lock Position
            ToolStripMenuItem mnuLock = new ToolStripMenuItem(isPt ? "Travar Posição na Barra" : "Lock Position on Taskbar");
            mnuLock.Checked = isLocked;
            mnuLock.Click += (s, e) =>
            {
                isLocked = !isLocked;
                mnuLock.Checked = isLocked;
                SaveSettings();
            };
            contextMenu.Items.Add(mnuLock);

            // 5. Iniciar com o Windows / Start with Windows
            ToolStripMenuItem mnuBoot = new ToolStripMenuItem(isPt ? "Iniciar com o Windows (Boot)" : "Start with Windows (Boot)");
            mnuBoot.Checked = IsAutoStartEnabled();
            mnuBoot.Click += (s, e) =>
            {
                bool newState = !mnuBoot.Checked;
                ToggleAutoStart(newState);
                mnuBoot.Checked = newState;
            };
            contextMenu.Items.Add(mnuBoot);

            // 6. Redefinir Posição / Reset Position
            ToolStripMenuItem mnuReset = new ToolStripMenuItem(isPt ? "Redefinir Posição (Auto-Acoplar)" : "Reset Position (Auto-Dock)");
            mnuReset.Click += (s, e) =>
            {
                PositionWidget(forceReset: true);
                SaveSettings();
            };
            contextMenu.Items.Add(mnuReset);

            // 7. Idioma / Language
            ToolStripMenuItem mnuLang = new ToolStripMenuItem(isPt ? "Idioma / Language" : "Language / Idioma");
            EnableCheckMargin(mnuLang);

            ToolStripMenuItem itmPt = new ToolStripMenuItem("Português (Brasil)");
            itmPt.Checked = isPt;
            itmPt.Click += (s, e) => { currentLanguage = "pt"; SaveSettings(); BuildContextMenu(); };

            ToolStripMenuItem itmEn = new ToolStripMenuItem("English");
            itmEn.Checked = !isPt;
            itmEn.Click += (s, e) => { currentLanguage = "en"; SaveSettings(); BuildContextMenu(); };

            mnuLang.DropDownItems.Add(itmPt);
            mnuLang.DropDownItems.Add(itmEn);
            contextMenu.Items.Add(mnuLang);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 8. About label
            ToolStripMenuItem mnuAbout = new ToolStripMenuItem("NetMonitor v1.0.5 — Eduardo Noé");
            mnuAbout.Enabled = false;
            contextMenu.Items.Add(mnuAbout);

            // 9. Sair / Exit
            ToolStripMenuItem mnuExit = new ToolStripMenuItem(isPt ? "Sair do NetMonitor" : "Exit NetMonitor");
            mnuExit.Click += (s, e) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            };
            contextMenu.Items.Add(mnuExit);

            this.trayIcon.ContextMenuStrip = contextMenu;
        }

        private static void EnableCheckMargin(ToolStripMenuItem item)
        {
            ToolStripDropDownMenu menu = item.DropDown as ToolStripDropDownMenu;
            if (menu != null)
            {
                menu.ShowCheckMargin = true;
                menu.ShowImageMargin = false;
            }
        }

        private void PopulateUnitsMenu(ToolStripMenuItem parent)
        {
            bool isPt = currentLanguage == "pt";
            parent.DropDownItems.Clear();

            string[] modes = new string[] { "Auto", "Kbps", "Mbps", "Gbps" };
            string[] labelsPt = new string[] { "Automático (Kbps / Mbps / Gbps)", "Sempre em Kbps", "Sempre em Mbps", "Sempre em Gbps" };
            string[] labelsEn = new string[] { "Automatic (Kbps / Mbps / Gbps)", "Always in Kbps", "Always in Mbps", "Always in Gbps" };

            for (int i = 0; i < modes.Length; i++)
            {
                string mode = modes[i];
                string label = isPt ? labelsPt[i] : labelsEn[i];
                ToolStripMenuItem itm = new ToolStripMenuItem(label);
                itm.Checked = string.Equals(unitMode, mode, StringComparison.OrdinalIgnoreCase);
                itm.Click += (s, e) =>
                {
                    unitMode = mode;
                    SaveSettings();
                    PopulateUnitsMenu(parent);
                    UpdateTraffic();
                };
                parent.DropDownItems.Add(itm);
            }
        }

        private void PopulateAdaptersMenu(ToolStripMenuItem parent)
        {
            bool isPt = currentLanguage == "pt";
            parent.DropDownItems.Clear();

            string autoText = isPt ? string.Format("Automático ({0})", activeNicDisplayName) : string.Format("Automatic ({0})", activeNicDisplayName);
            ToolStripMenuItem itmAuto = new ToolStripMenuItem(autoText);
            itmAuto.Checked = string.Equals(selectedNicId, "Auto", StringComparison.OrdinalIgnoreCase);
            itmAuto.Click += (s, e) =>
            {
                selectedNicId = "Auto";
                lastBytesIn = -1;
                lastBytesOut = -1;
                SaveSettings();
                PopulateAdaptersMenu(parent);
                UpdateTraffic();
            };
            parent.DropDownItems.Add(itmAuto);
            parent.DropDownItems.Add(new ToolStripSeparator());

            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in nics)
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                string id = nic.Id;
                string name = string.Format("{0} ({1})", nic.Name, nic.Description);
                if (name.Length > 45) name = name.Substring(0, 42) + "...";

                ToolStripMenuItem itm = new ToolStripMenuItem(name);
                itm.Checked = (selectedNicId == id || selectedNicId == nic.Name);
                itm.Click += (s, e) =>
                {
                    selectedNicId = id;
                    lastBytesIn = -1;
                    lastBytesOut = -1;
                    SaveSettings();
                    PopulateAdaptersMenu(parent);
                    UpdateTraffic();
                };
                parent.DropDownItems.Add(itm);
            }
        }

        private void OpenSettingsDialog()
        {
            using (SettingsForm sf = new SettingsForm(this, currentLanguage, unitMode, arrowOnRight, isLocked, iconTemplate, customUpChar, customDownChar))
            {
                if (sf.ShowDialog() == DialogResult.OK)
                {
                    currentLanguage = sf.SelectedLanguage;
                    unitMode = sf.SelectedUnitMode;
                    arrowOnRight = sf.SelectedArrowOnRight;
                    isLocked = sf.SelectedIsLocked;
                    iconTemplate = sf.SelectedIconTemplate;
                    customUpChar = sf.SelectedCustomUpChar;
                    customDownChar = sf.SelectedCustomDownChar;

                    SaveSettings();
                    BuildContextMenu();
                    this.Invalidate();
                }
            }
        }

        private void NetMonitorApp_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (!isLocked)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                BuildContextMenu();

                // Força abertura PARA CIMA da barra de tarefas
                int menuHeight = contextMenu.GetPreferredSize(Size.Empty).Height;
                contextMenu.Show(this, new Point(e.X, -menuHeight));
            }
        }

        private void NetMonitorApp_MouseUp(object sender, MouseEventArgs e)
        {
            if (!isLocked)
            {
                SaveSettings();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (updateTimer != null) updateTimer.Dispose();
                if (trayIcon != null) trayIcon.Dispose();
                if (fontNum != null) fontNum.Dispose();
                if (fontUnit != null) fontUnit.Dispose();
                if (fontUnicode != null) fontUnicode.Dispose();
                if (sfRight != null) sfRight.Dispose();
                if (sfLeft != null) sfLeft.Dispose();
                if (sfCenter != null) sfCenter.Dispose();
            }
            base.Dispose(disposing);
        }

        [STAThread]
        public static void Main()
        {
            try { SetProcessDpiAwareness(2); } catch { try { SetProcessDPIAware(); } catch { } }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new NetMonitorApp());
        }
    }

    // ==========================================================================
    // JANELA DE CONFIGURAÇÕES E PERSONALIZAÇÃO (SETTINGS MODAL)
    // ==========================================================================
    public class SettingsForm : Form
    {
        public string SelectedLanguage { get; private set; }
        public string SelectedUnitMode { get; private set; }
        public bool SelectedArrowOnRight { get; private set; }
        public bool SelectedIsLocked { get; private set; }
        public string SelectedIconTemplate { get; private set; }
        public string SelectedCustomUpChar { get; private set; }
        public string SelectedCustomDownChar { get; private set; }

        private ComboBox cbLang;
        private ComboBox cbUnits;
        private RadioButton rbRight;
        private RadioButton rbLeft;
        private CheckBox chkLock;
        private ComboBox cbTemplate;
        private TextBox txtUp;
        private TextBox txtDown;
        private Label lblPreview;
        private Panel pnlPreview;

        public SettingsForm(Form parent, string lang, string unit, bool arrowRight, bool locked, string tpl, string upChar, string downChar)
        {
            this.SelectedLanguage = lang;
            this.SelectedUnitMode = unit;
            this.SelectedArrowOnRight = arrowRight;
            this.SelectedIsLocked = locked;
            this.SelectedIconTemplate = tpl;
            this.SelectedCustomUpChar = upChar;
            this.SelectedCustomDownChar = downChar;

            InitializeUI();
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        }

        private void InitializeUI()
        {
            bool isPt = SelectedLanguage == "pt";

            this.Text = isPt ? "Personalização do NetMonitor" : "NetMonitor Customization";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(500, 480);
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            int curY = 16;

            // 1. Idioma / Language
            Label lblL = new Label { Text = isPt ? "Idioma / Language:" : "Language / Idioma:", Location = new Point(20, curY), AutoSize = true };
            this.Controls.Add(lblL);
            cbLang = new ComboBox { Location = new Point(200, curY - 3), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            cbLang.Items.Add("Português (Brasil)");
            cbLang.Items.Add("English");
            cbLang.SelectedIndex = isPt ? 0 : 1;
            this.Controls.Add(cbLang);

            curY += 36;

            // 2. Unidade / Unit Mode
            Label lblU = new Label { Text = isPt ? "Unidade de Velocidade:" : "Speed Unit:", Location = new Point(20, curY), AutoSize = true };
            this.Controls.Add(lblU);
            cbUnits = new ComboBox { Location = new Point(200, curY - 3), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            cbUnits.Items.Add(isPt ? "Automático (Kbps / Mbps / Gbps)" : "Automatic (Kbps / Mbps / Gbps)");
            cbUnits.Items.Add("Sempre em Kbps / Always Kbps");
            cbUnits.Items.Add("Sempre em Mbps / Always Mbps");
            cbUnits.Items.Add("Sempre em Gbps / Always Gbps");
            if (SelectedUnitMode == "Kbps") cbUnits.SelectedIndex = 1;
            else if (SelectedUnitMode == "Mbps") cbUnits.SelectedIndex = 2;
            else if (SelectedUnitMode == "Gbps") cbUnits.SelectedIndex = 3;
            else cbUnits.SelectedIndex = 0;
            this.Controls.Add(cbUnits);

            curY += 40;

            // 3. Ordem das Setas (Posicionamento)
            GroupBox gbLayout = new GroupBox
            {
                Text = isPt ? "Posição das Setas na Interface" : "Arrow Position in Interface",
                Location = new Point(20, curY),
                Size = new Size(460, 75),
                ForeColor = Color.FromArgb(200, 200, 200)
            };
            this.Controls.Add(gbLayout);

            rbRight = new RadioButton
            {
                Text = isPt ? "Setas à Direita: [ 823.6 Mbps ▲ ] (Recomendado)" : "Arrows on Right: [ 823.6 Mbps ▲ ] (Recommended)",
                Location = new Point(15, 22),
                AutoSize = true,
                Checked = SelectedArrowOnRight,
                ForeColor = Color.White
            };
            rbLeft = new RadioButton
            {
                Text = isPt ? "Setas à Esquerda: [ ▲ 823.6 Mbps ]" : "Arrows on Left: [ ▲ 823.6 Mbps ]",
                Location = new Point(15, 46),
                AutoSize = true,
                Checked = !SelectedArrowOnRight,
                ForeColor = Color.White
            };
            gbLayout.Controls.Add(rbRight);
            gbLayout.Controls.Add(rbLeft);

            curY += 85;

            // 4. Customização de Ícones / Setas / Unicode
            GroupBox gbIcons = new GroupBox
            {
                Text = isPt ? "Ícones das Setas & Caracteres Unicode" : "Arrow Icons & Unicode Characters",
                Location = new Point(20, curY),
                Size = new Size(460, 140),
                ForeColor = Color.FromArgb(200, 200, 200)
            };
            this.Controls.Add(gbIcons);

            Label lblTpl = new Label { Text = isPt ? "Modelo Pré-definido:" : "Preset Template:", Location = new Point(15, 24), AutoSize = true, ForeColor = Color.White };
            gbIcons.Controls.Add(lblTpl);

            cbTemplate = new ComboBox
            {
                Location = new Point(170, 21),
                Width = 265,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            cbTemplate.Items.Add(isPt ? "1. Triângulos Sutis (Vetor Nativo)" : "1. Subtle Triangles (Native Vector)");
            cbTemplate.Items.Add(isPt ? "2. Chevrons Fluentes ( ∧ / ∨ )" : "2. Fluent Chevrons ( ∧ / ∨ )");
            cbTemplate.Items.Add(isPt ? "3. Setas Clássicas ( ↑ / ↓ )" : "3. Classic Arrows ( ↑ / ↓ )");
            cbTemplate.Items.Add(isPt ? "4. Triângulos Cheios ( ▲ / ▼ )" : "4. Solid Triangles ( ▲ / ▼ )");
            cbTemplate.Items.Add(isPt ? "5. Personalizado (Mapa de Caracteres / Unicode)" : "5. Custom (Character Map / Unicode)");

            if (SelectedIconTemplate == "VectorChevron") cbTemplate.SelectedIndex = 1;
            else if (SelectedIconTemplate == "UnicodeClassic") cbTemplate.SelectedIndex = 2;
            else if (SelectedIconTemplate == "UnicodeSolid") cbTemplate.SelectedIndex = 3;
            else if (SelectedIconTemplate == "UnicodeCustom") cbTemplate.SelectedIndex = 4;
            else cbTemplate.SelectedIndex = 0;
            gbIcons.Controls.Add(cbTemplate);

            // Campos Unicode Customizados
            Label lblUp = new Label { Text = isPt ? "Upload (Char/U+XXXX):" : "Upload (Char/U+XXXX):", Location = new Point(15, 60), AutoSize = true, ForeColor = Color.FromArgb(180, 180, 180) };
            gbIcons.Controls.Add(lblUp);
            txtUp = new TextBox { Location = new Point(205, 57), Width = 70, Text = SelectedCustomUpChar, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            gbIcons.Controls.Add(txtUp);

            Label lblDown = new Label { Text = isPt ? "Download (Char/U+XXXX):" : "Download (Char/U+XXXX):", Location = new Point(15, 95), AutoSize = true, ForeColor = Color.FromArgb(180, 180, 180) };
            gbIcons.Controls.Add(lblDown);
            txtDown = new TextBox { Location = new Point(205, 92), Width = 70, Text = SelectedCustomDownChar, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            gbIcons.Controls.Add(txtDown);

            lblPreview = new Label { Text = isPt ? "Prévia:" : "Preview:", Location = new Point(300, 54), AutoSize = true, ForeColor = Color.FromArgb(170, 170, 170), Font = new Font(this.Font.FontFamily, 8f) };
            gbIcons.Controls.Add(lblPreview);

            pnlPreview = new Panel { Location = new Point(345, 52), Size = new Size(100, 68), BackColor = Color.FromArgb(20, 20, 20), BorderStyle = BorderStyle.FixedSingle };
            pnlPreview.Paint += (s, pe) =>
            {
                Graphics g = pe.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                string upC = NetMonitorApp.ParseUnicodeString(txtUp.Text, "▲");
                string downC = NetMonitorApp.ParseUnicodeString(txtDown.Text, "▼");
                bool right = rbRight.Checked;
                using (Font fSpeed = new Font("Segoe UI", 9f, FontStyle.Regular))
                using (Font fArrow = new Font("Segoe UI Symbol", 8.5f, FontStyle.Regular))
                using (Brush bText = new SolidBrush(Color.White))
                using (Brush bArrow = new SolidBrush(Color.FromArgb(190, 190, 190)))
                {
                    if (right)
                    {
                        g.DrawString("823 Mbps", fSpeed, bText, new PointF(4, 8));
                        g.DrawString(upC, fArrow, bArrow, new PointF(76, 8));
                        g.DrawString("142 Mbps", fSpeed, bText, new PointF(4, 38));
                        g.DrawString(downC, fArrow, bArrow, new PointF(76, 38));
                    }
                    else
                    {
                        g.DrawString(upC, fArrow, bArrow, new PointF(4, 8));
                        g.DrawString("823 Mbps", fSpeed, bText, new PointF(20, 8));
                        g.DrawString(downC, fArrow, bArrow, new PointF(4, 38));
                        g.DrawString("142 Mbps", fSpeed, bText, new PointF(20, 38));
                    }
                }
            };
            gbIcons.Controls.Add(pnlPreview);

            txtUp.TextChanged += (s, e) => pnlPreview.Invalidate();
            txtDown.TextChanged += (s, e) => pnlPreview.Invalidate();
            rbRight.CheckedChanged += (s, e) => pnlPreview.Invalidate();
            rbLeft.CheckedChanged += (s, e) => pnlPreview.Invalidate();

            cbTemplate.SelectedIndexChanged += (s, e) =>
            {
                if (cbTemplate.SelectedIndex == 0) { txtUp.Text = "▲"; txtDown.Text = "▼"; }
                else if (cbTemplate.SelectedIndex == 1) { txtUp.Text = "∧"; txtDown.Text = "∨"; }
                else if (cbTemplate.SelectedIndex == 2) { txtUp.Text = "↑"; txtDown.Text = "↓"; }
                else if (cbTemplate.SelectedIndex == 3) { txtUp.Text = "▲"; txtDown.Text = "▼"; }
                pnlPreview.Invalidate();
            };

            curY += 150;

            // 5. Trava de Posição
            chkLock = new CheckBox
            {
                Text = isPt ? "Travar posição na barra de tarefas (bloqueia arrasto acidental)" : "Lock position on taskbar (prevents accidental drag)",
                Location = new Point(20, curY),
                AutoSize = true,
                Checked = SelectedIsLocked,
                ForeColor = Color.FromArgb(230, 230, 230)
            };
            this.Controls.Add(chkLock);

            curY += 36;

            // Botões Salvar / Cancelar
            Button btnSave = new Button
            {
                Text = isPt ? "Salvar" : "Save",
                Location = new Point(285, curY),
                Size = new Size(95, 32),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(this.Font, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) =>
            {
                SelectedLanguage = (cbLang.SelectedIndex == 0) ? "pt" : "en";

                if (cbUnits.SelectedIndex == 1) SelectedUnitMode = "Kbps";
                else if (cbUnits.SelectedIndex == 2) SelectedUnitMode = "Mbps";
                else if (cbUnits.SelectedIndex == 3) SelectedUnitMode = "Gbps";
                else SelectedUnitMode = "Auto";

                SelectedArrowOnRight = rbRight.Checked;
                SelectedIsLocked = chkLock.Checked;

                if (cbTemplate.SelectedIndex == 0) SelectedIconTemplate = "VectorTriangle";
                else if (cbTemplate.SelectedIndex == 1) SelectedIconTemplate = "VectorChevron";
                else SelectedIconTemplate = "Unicode";

                SelectedCustomUpChar = NetMonitorApp.ParseUnicodeString(txtUp.Text, "▲");
                SelectedCustomDownChar = NetMonitorApp.ParseUnicodeString(txtDown.Text, "▼");

                this.Close();
            };
            this.Controls.Add(btnSave);

            Button btnCancel = new Button
            {
                Text = isPt ? "Cancelar" : "Cancel",
                Location = new Point(390, curY),
                Size = new Size(90, 32),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(btnCancel);
        }
    }
}
