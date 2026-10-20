// ==============================================================================
// NetMonitor - Monitor de Rede Ultraleve para Barra de Tarefas (Windows 11)
// Autor: Eduardo Noé (eduardonoe)
// Version: 1.0.4
//
// v1.0.4 changelog:
// - Tipografia mais fina: números de velocidade agora usam FontStyle.Regular (sem o peso bold excessivo).
// - Triângulos ampliados em 15% mantendo o traço vetorial suave e anti-aliased.
// - Correção do Menu de Contexto em múltiplos monitores (abre obrigatoriamente para CIMA, nunca para baixo).
// - Redução de ~30% do espaço vazio à direita: largura otimizada para 98px aproximando o widget da bandeja.
// - Nomenclatura oficial portátil: "NetMonitor Portable".
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

        // Layout Dimensions (v1.0.4: 98x38 otimizado para eliminar espaço vazio à direita)
        private const int WIDGET_WIDTH = 98;
        private const int WIDGET_HEIGHT = 38;

        // Visual Colors (Windows 11 Dark Taskbar Theme)
        private static readonly Color BG_COLOR = Color.FromArgb(28, 28, 28);
        private static readonly Color ARROW_COLOR = Color.FromArgb(225, 225, 225);
        private static readonly Color NUM_COLOR = Color.FromArgb(255, 255, 255);
        private static readonly Color UNIT_COLOR = Color.FromArgb(195, 195, 195);

        // Fonts
        private Font fontNum;
        private Font fontUnit;

        // Formatting alignment
        private StringFormat sfRight;
        private StringFormat sfLeft;

        // Network state
        private Timer updateTimer;
        private string selectedNicId = "Auto";
        private string activeNicDisplayName = "Auto";
        private string currentLanguage = "pt";
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

            // v1.0.4: Fonte mais fina (Regular) conforme solicitado
            fontNum = new Font(fontName, 10.5f, FontStyle.Regular);
            fontUnit = new Font(fontName, 9.0f, FontStyle.Regular);

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

        private void LoadSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_KEY_APP))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("SelectedAdapter");
                        if (val != null) selectedNicId = val.ToString();

                        object lang = key.GetValue("Language");
                        if (lang != null) currentLanguage = lang.ToString();
                        else
                        {
                            currentLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase) ? "pt" : "en";
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REG_KEY_APP))
                {
                    if (key != null)
                    {
                        key.SetValue("SelectedAdapter", selectedNicId);
                        key.SetValue("Language", currentLanguage);
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
                        // v1.0.4: Ajustado mais à direita (reduzindo o espaço em 30%)
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
                strUpUnit = "Mbps";
                strDownNum = "0.0";
                strDownUnit = "Mbps";
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
                strUpUnit = "Mbps";
                strDownNum = "0.0";
                strDownUnit = "Mbps";
            }

            this.Invalidate();
        }

        private void FormatSpeed(double bitsPerSec, out string num, out string unit)
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            g.Clear(BG_COLOR);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. SETAS VETORIAIS AUMENTADAS EM 15% (v1.0.4)
            // Largura: 8.0px, Altura: 5.2px
            using (Brush brushArrow = new SolidBrush(ARROW_COLOR))
            {
                // Seta Upload (X central: 13, Y: 5.0 a 10.2)
                PointF[] upArrow = new PointF[] {
                    new PointF(13.0f, 5.0f),
                    new PointF(9.0f, 10.2f),
                    new PointF(17.0f, 10.2f)
                };
                g.FillPolygon(brushArrow, upArrow);

                // Seta Download (X central: 13, Y: 26.3 a 31.5)
                PointF[] downArrow = new PointF[] {
                    new PointF(13.0f, 31.5f),
                    new PointF(9.0f, 26.3f),
                    new PointF(17.0f, 26.3f)
                };
                g.FillPolygon(brushArrow, downArrow);
            }

            // 2. NÚMEROS DE VELOCIDADE (Fonte Regular mais fina, justificados à direita em X = 58)
            RectangleF rUpNum = new RectangleF(17, 1, 42, 17);
            RectangleF rDownNum = new RectangleF(17, 19, 42, 17);

            using (Brush bNum = new SolidBrush(NUM_COLOR))
            {
                g.DrawString(strUpNum, fontNum, bNum, rUpNum, sfRight);
                g.DrawString(strDownNum, fontNum, bNum, rDownNum, sfRight);
            }

            // 3. UNIDADE (Travada em X = 63, terminando antes de 98px)
            RectangleF rUpUnit = new RectangleF(63, 1, 35, 17);
            RectangleF rDownUnit = new RectangleF(63, 19, 35, 17);

            using (Brush bUnit = new SolidBrush(UNIT_COLOR))
            {
                g.DrawString(strUpUnit, fontUnit, bUnit, rUpUnit, sfLeft);
                g.DrawString(strDownUnit, fontUnit, bUnit, rDownUnit, sfLeft);
            }
        }

        private void BuildContextMenu()
        {
            bool isPt = currentLanguage == "pt";
            contextMenu = new ContextMenuStrip();
            contextMenu.ShowImageMargin = false;

            ToolStripMenuItem mnuAdapters = new ToolStripMenuItem(isPt ? "Placa de Rede" : "Network Adapter");
            PopulateAdaptersMenu(mnuAdapters);
            contextMenu.Items.Add(mnuAdapters);

            contextMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem mnuBoot = new ToolStripMenuItem(isPt ? "Iniciar com o Windows (Boot)" : "Start with Windows (Boot)");
            mnuBoot.Checked = IsAutoStartEnabled();
            mnuBoot.Click += (s, e) =>
            {
                bool newState = !mnuBoot.Checked;
                ToggleAutoStart(newState);
                mnuBoot.Checked = newState;
            };
            contextMenu.Items.Add(mnuBoot);

            ToolStripMenuItem mnuReset = new ToolStripMenuItem(isPt ? "Redefinir Posição (Auto-Acoplar)" : "Reset Position (Auto-Dock)");
            mnuReset.Click += (s, e) =>
            {
                PositionWidget(forceReset: true);
                SaveSettings();
            };
            contextMenu.Items.Add(mnuReset);

            ToolStripMenuItem mnuLang = new ToolStripMenuItem(isPt ? "Idioma / Language" : "Language / Idioma");
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

            ToolStripMenuItem mnuAbout = new ToolStripMenuItem("NetMonitor v1.0.4 — Eduardo Noé");
            mnuAbout.Enabled = false;
            contextMenu.Items.Add(mnuAbout);

            ToolStripMenuItem mnuExit = new ToolStripMenuItem(isPt ? "Sair do NetMonitor" : "Exit NetMonitor");
            mnuExit.Click += (s, e) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            };
            contextMenu.Items.Add(mnuExit);

            // Não atribuímos a this.ContextMenuStrip para evitar que o Windows abra o menu para baixo no monitor secundário
            this.trayIcon.ContextMenuStrip = contextMenu;
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

        private void NetMonitorApp_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
            else if (e.Button == MouseButtons.Right)
            {
                ToolStripMenuItem mnuAdapters = contextMenu.Items[0] as ToolStripMenuItem;
                if (mnuAdapters != null) PopulateAdaptersMenu(mnuAdapters);

                // Força o menu a abrir rigorosamente PARA CIMA da barra de tarefas
                // evitando que se projete no monitor inferior em setups verticais
                int menuHeight = contextMenu.GetPreferredSize(Size.Empty).Height;
                contextMenu.Show(this, new Point(e.X, -menuHeight));
            }
        }

        private void NetMonitorApp_MouseUp(object sender, MouseEventArgs e)
        {
            SaveSettings();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (updateTimer != null) updateTimer.Dispose();
                if (trayIcon != null) trayIcon.Dispose();
                if (fontNum != null) fontNum.Dispose();
                if (fontUnit != null) fontUnit.Dispose();
                if (sfRight != null) sfRight.Dispose();
                if (sfLeft != null) sfLeft.Dispose();
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
}
