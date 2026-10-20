// ==============================================================================
// NetMonitor - Monitor de Rede Ultraleve para Barra de Tarefas (Windows 11)
// Autor: Eduardo Noé (eduardonoe)
// Version: 1.0.0
//
// v1.0.0 changelog:
// - Lançamento oficial v1.0.0 com interface completa e suporte multi-monitor.
// v1.0.6 changelog:
// - Tema Nativo do Windows 11 no Menu de Contexto (Modo Escuro / Claro automático).
// - Correção de invasão do segundo monitor: submenus contidos estritamente na tela atual (abertura à esquerda).
// - Correção do seletor recolhendo: timer Win32 pausado durante exibição das configurações.
// - Seletor completo de Tipografia: Família de Fonte, Tamanho e Estilo (Regular, Bold, Italic).
// - Sistema avançado de Cores: Cores unificadas ou individuais para Setas e Textos (Upload / Download).
// - Nova interface moderna de Configurações com abas Dark (Geral, Tipografia, Ícones, Cores) e Live Preview fixo.
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
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private const string REG_KEY_APP = @"Software\NetMonitor";
        private const string REG_KEY_RUN = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "NetMonitor";

        // Layout Dimensions
        private const int WIDGET_WIDTH = 106;
        private const int WIDGET_HEIGHT = 38;

        // Visual Colors (Windows 11 Dark Taskbar Theme default)
        private static readonly Color BG_COLOR = Color.FromArgb(28, 28, 28);

        // Fonts
        private string fontFamily = "Segoe UI";
        private float fontSize = 10.5f;
        private FontStyle fontStyle = FontStyle.Regular;
        private Font fontNum;
        private Font fontUnit;
        private Font fontUnicode;

        // Colors
        private string arrowColorMode = "Unified";   // "Unified" ou "Individual"
        private Color arrowColorUnified = Color.FromArgb(225, 225, 225);
        private Color arrowColorUp = Color.FromArgb(225, 225, 225);
        private Color arrowColorDown = Color.FromArgb(225, 225, 225);

        private string textColorMode = "Unified";    // "Unified" ou "Individual"
        private Color textColorUnified = Color.FromArgb(255, 255, 255);
        private Color textColorUp = Color.FromArgb(255, 255, 255);
        private Color textColorDown = Color.FromArgb(255, 255, 255);

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

        public static string ColorToHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        public static Color HexToColor(string hex, Color defaultColor)
        {
            if (string.IsNullOrEmpty(hex)) return defaultColor;
            try
            {
                return ColorTranslator.FromHtml(hex);
            }
            catch
            {
                return defaultColor;
            }
        }

        public static bool IsWindowsDarkTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object sysLight = key.GetValue("SystemUsesLightTheme");
                        if (sysLight != null) return Convert.ToInt32(sysLight) == 0;
                        object appLight = key.GetValue("AppsUseLightTheme");
                        if (appLight != null) return Convert.ToInt32(appLight) == 0;
                    }
                }
            }
            catch { }
            return true;
        }

        private void SetupFonts()
        {
            if (fontNum != null) fontNum.Dispose();
            if (fontUnit != null) fontUnit.Dispose();
            if (fontUnicode != null) fontUnicode.Dispose();

            try
            {
                fontNum = new Font(fontFamily, fontSize, fontStyle);
            }
            catch
            {
                fontNum = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            }

            try
            {
                float unitSize = Math.Max(7.0f, fontSize - 1.5f);
                fontUnit = new Font(fontFamily, unitSize, FontStyle.Regular);
            }
            catch
            {
                fontUnit = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            }

            float uniSize = Math.Max(8.0f, fontSize - 1.0f);
            fontUnicode = new Font("Segoe UI Symbol", uniSize, FontStyle.Regular);

            if (sfRight == null)
            {
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

                        object valFontF = key.GetValue("FontFamily");
                        if (valFontF != null) fontFamily = valFontF.ToString();

                        object valFontS = key.GetValue("FontSize");
                        if (valFontS != null) float.TryParse(valFontS.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out fontSize);

                        object valFontSt = key.GetValue("FontStyle");
                        if (valFontSt != null) fontStyle = (FontStyle)Convert.ToInt32(valFontSt);

                        object valAm = key.GetValue("ArrowColorMode");
                        if (valAm != null) arrowColorMode = valAm.ToString();
                        arrowColorUnified = HexToColor(key.GetValue("ArrowColorUnified") as string, Color.FromArgb(225, 225, 225));
                        arrowColorUp = HexToColor(key.GetValue("ArrowColorUp") as string, Color.FromArgb(225, 225, 225));
                        arrowColorDown = HexToColor(key.GetValue("ArrowColorDown") as string, Color.FromArgb(225, 225, 225));

                        object valTm = key.GetValue("TextColorMode");
                        if (valTm != null) textColorMode = valTm.ToString();
                        textColorUnified = HexToColor(key.GetValue("TextColorUnified") as string, Color.FromArgb(255, 255, 255));
                        textColorUp = HexToColor(key.GetValue("TextColorUp") as string, Color.FromArgb(255, 255, 255));
                        textColorDown = HexToColor(key.GetValue("TextColorDown") as string, Color.FromArgb(255, 255, 255));
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

                        key.SetValue("FontFamily", fontFamily);
                        key.SetValue("FontSize", fontSize.ToString(CultureInfo.InvariantCulture));
                        key.SetValue("FontStyle", (int)fontStyle);

                        key.SetValue("ArrowColorMode", arrowColorMode);
                        key.SetValue("ArrowColorUnified", ColorToHex(arrowColorUnified));
                        key.SetValue("ArrowColorUp", ColorToHex(arrowColorUp));
                        key.SetValue("ArrowColorDown", ColorToHex(arrowColorDown));

                        key.SetValue("TextColorMode", textColorMode);
                        key.SetValue("TextColorUnified", ColorToHex(textColorUnified));
                        key.SetValue("TextColorUp", ColorToHex(textColorUp));
                        key.SetValue("TextColorDown", ColorToHex(textColorDown));
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
            if (unitMode == "Kbps")
            {
                double kbps = bitsPerSec / 1000.0;
                num = (kbps >= 100) ? kbps.ToString("F0") : kbps.ToString("F1");
                unit = "Kbps";
            }
            else if (unitMode == "Mbps")
            {
                double mbps = bitsPerSec / 1000000.0;
                num = (mbps >= 100) ? mbps.ToString("F0") : mbps.ToString("F1");
                unit = "Mbps";
            }
            else if (unitMode == "Gbps")
            {
                double gbps = bitsPerSec / 1000000000.0;
                num = (gbps >= 10) ? gbps.ToString("F1") : gbps.ToString("F2");
                unit = "Gbps";
            }
            else
            {
                if (bitsPerSec >= 1000000000.0)
                {
                    double gbps = bitsPerSec / 1000000000.0;
                    num = (gbps >= 10) ? gbps.ToString("F1") : gbps.ToString("F2");
                    unit = "Gbps";
                }
                else if (bitsPerSec >= 1000000.0)
                {
                    double mbps = bitsPerSec / 1000000.0;
                    num = (mbps >= 100) ? mbps.ToString("F0") : mbps.ToString("F1");
                    unit = "Mbps";
                }
                else
                {
                    double kbps = bitsPerSec / 1000.0;
                    num = (kbps >= 100) ? kbps.ToString("F0") : kbps.ToString("F1");
                    unit = "Kbps";
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

            Color cTextUp = (textColorMode == "Individual") ? textColorUp : textColorUnified;
            Color cTextDown = (textColorMode == "Individual") ? textColorDown : textColorUnified;

            Color cArrowUp = (arrowColorMode == "Individual") ? arrowColorUp : arrowColorUnified;
            Color cArrowDown = (arrowColorMode == "Individual") ? arrowColorDown : arrowColorUnified;

            if (arrowOnRight)
            {
                // ==============================================================
                // LAYOUT: SETAS À DIREITA -> [ 823.6 Mbps  ▲ ]
                // ==============================================================
                RectangleF rUpNum = new RectangleF(2, 1, 46, 17);
                RectangleF rDownNum = new RectangleF(2, 19, 46, 17);

                using (Brush bNumUp = new SolidBrush(cTextUp))
                using (Brush bNumDown = new SolidBrush(cTextDown))
                {
                    g.DrawString(strUpNum, fontNum, bNumUp, rUpNum, sfRight);
                    g.DrawString(strDownNum, fontNum, bNumDown, rDownNum, sfRight);
                }

                RectangleF rUpUnit = new RectangleF(50, 1, 38, 17);
                RectangleF rDownUnit = new RectangleF(50, 19, 38, 17);

                Color cUnitUp = Color.FromArgb((int)(cTextUp.R * 0.85f), (int)(cTextUp.G * 0.85f), (int)(cTextUp.B * 0.85f));
                Color cUnitDown = Color.FromArgb((int)(cTextDown.R * 0.85f), (int)(cTextDown.G * 0.85f), (int)(cTextDown.B * 0.85f));

                using (Brush bUnitUp = new SolidBrush(cUnitUp))
                using (Brush bUnitDown = new SolidBrush(cUnitDown))
                {
                    g.DrawString(strUpUnit, fontUnit, bUnitUp, rUpUnit, sfLeft);
                    g.DrawString(strDownUnit, fontUnit, bUnitDown, rDownUnit, sfLeft);
                }

                DrawArrowIcon(g, 97.0f, cArrowUp, cArrowDown);
            }
            else
            {
                // ==============================================================
                // LAYOUT: SETAS À ESQUERDA -> [ ▲  823.6 Mbps ]
                // ==============================================================
                DrawArrowIcon(g, 10.0f, cArrowUp, cArrowDown);

                RectangleF rUpNum = new RectangleF(18, 1, 42, 17);
                RectangleF rDownNum = new RectangleF(18, 19, 42, 17);

                using (Brush bNumUp = new SolidBrush(cTextUp))
                using (Brush bNumDown = new SolidBrush(cTextDown))
                {
                    g.DrawString(strUpNum, fontNum, bNumUp, rUpNum, sfRight);
                    g.DrawString(strDownNum, fontNum, bNumDown, rDownNum, sfRight);
                }

                RectangleF rUpUnit = new RectangleF(62, 1, 40, 17);
                RectangleF rDownUnit = new RectangleF(62, 19, 40, 17);

                Color cUnitUp = Color.FromArgb((int)(cTextUp.R * 0.85f), (int)(cTextUp.G * 0.85f), (int)(cTextUp.B * 0.85f));
                Color cUnitDown = Color.FromArgb((int)(cTextDown.R * 0.85f), (int)(cTextDown.G * 0.85f), (int)(cTextDown.B * 0.85f));

                using (Brush bUnitUp = new SolidBrush(cUnitUp))
                using (Brush bUnitDown = new SolidBrush(cUnitDown))
                {
                    g.DrawString(strUpUnit, fontUnit, bUnitUp, rUpUnit, sfLeft);
                    g.DrawString(strDownUnit, fontUnit, bUnitDown, rDownUnit, sfLeft);
                }
            }
        }

        private void DrawArrowIcon(Graphics g, float centerX, Color colUp, Color colDown)
        {
            using (Brush bUp = new SolidBrush(colUp))
            using (Brush bDown = new SolidBrush(colDown))
            {
                if (iconTemplate == "VectorTriangle")
                {
                    PointF[] upArrow = new PointF[] {
                        new PointF(centerX, 5.0f),
                        new PointF(centerX - 4.0f, 10.2f),
                        new PointF(centerX + 4.0f, 10.2f)
                    };
                    g.FillPolygon(bUp, upArrow);

                    PointF[] downArrow = new PointF[] {
                        new PointF(centerX, 31.5f),
                        new PointF(centerX - 4.0f, 26.3f),
                        new PointF(centerX + 4.0f, 26.3f)
                    };
                    g.FillPolygon(bDown, downArrow);
                }
                else if (iconTemplate == "VectorChevron")
                {
                    using (Pen penUp = new Pen(colUp, 1.4f))
                    using (Pen penDown = new Pen(colDown, 1.4f))
                    {
                        g.DrawLines(penUp, new PointF[] {
                            new PointF(centerX - 3.8f, 9.5f),
                            new PointF(centerX, 5.5f),
                            new PointF(centerX + 3.8f, 9.5f)
                        });
                        g.DrawLines(penDown, new PointF[] {
                            new PointF(centerX - 3.8f, 27.0f),
                            new PointF(centerX, 31.0f),
                            new PointF(centerX + 3.8f, 27.0f)
                        });
                    }
                }
                else
                {
                    string upCh = ParseUnicodeString(customUpChar, "▲");
                    string downCh = ParseUnicodeString(customDownChar, "▼");

                    RectangleF rUp = new RectangleF(centerX - 8, 1, 16, 17);
                    RectangleF rDown = new RectangleF(centerX - 8, 19, 16, 17);

                    g.DrawString(upCh, fontUnicode, bUp, rUp, sfCenter);
                    g.DrawString(downCh, fontUnicode, bDown, rDown, sfCenter);
                }
            }
        }

        private void BuildContextMenu()
        {
            bool isPt = currentLanguage == "pt";
            bool isDark = IsWindowsDarkTheme();

            contextMenu = new ContextMenuStrip();
            contextMenu.Renderer = new Windows11MenuRenderer(isDark);
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
            ConfigureSubmenu(mnuAdapters);
            PopulateAdaptersMenu(mnuAdapters);
            contextMenu.Items.Add(mnuAdapters);

            // 2. Unidade / Unit
            ToolStripMenuItem mnuUnits = new ToolStripMenuItem(isPt ? "Unidade" : "Unit");
            ConfigureSubmenu(mnuUnits);
            PopulateUnitsMenu(mnuUnits);
            contextMenu.Items.Add(mnuUnits);

            // 3. Posição das Setas / Layout
            ToolStripMenuItem mnuLayout = new ToolStripMenuItem(isPt ? "Posição das Setas" : "Arrow Position");
            ConfigureSubmenu(mnuLayout);

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
            ConfigureSubmenu(mnuLang);

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
            ToolStripMenuItem mnuAbout = new ToolStripMenuItem("NetMonitor v1.0.0 — Eduardo Noé");
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

        private void ConfigureSubmenu(ToolStripMenuItem item)
        {
            ToolStripDropDownMenu menu = item.DropDown as ToolStripDropDownMenu;
            if (menu != null)
            {
                menu.ShowCheckMargin = true;
                menu.ShowImageMargin = false;
                menu.Renderer = contextMenu.Renderer;
            }

            // Impede expansão para o segundo monitor:
            // Submenus abrem obrigatoriamente para a ESQUERDA da barra direita.
            item.DropDownDirection = ToolStripDropDownDirection.Left;
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
            // Pausa o timer para que SetWindowPos não cancele popups de combo
            updateTimer.Stop();
            try
            {
                using (SettingsForm sf = new SettingsForm(
                    this,
                    currentLanguage,
                    unitMode,
                    arrowOnRight,
                    isLocked,
                    iconTemplate,
                    customUpChar,
                    customDownChar,
                    fontFamily,
                    fontSize,
                    fontStyle,
                    arrowColorMode,
                    arrowColorUnified,
                    arrowColorUp,
                    arrowColorDown,
                    textColorMode,
                    textColorUnified,
                    textColorUp,
                    textColorDown))
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

                        fontFamily = sf.SelectedFontFamily;
                        fontSize = sf.SelectedFontSize;
                        fontStyle = sf.SelectedFontStyle;

                        arrowColorMode = sf.SelectedArrowColorMode;
                        arrowColorUnified = sf.SelectedArrowColorUnified;
                        arrowColorUp = sf.SelectedArrowColorUp;
                        arrowColorDown = sf.SelectedArrowColorDown;

                        textColorMode = sf.SelectedTextColorMode;
                        textColorUnified = sf.SelectedTextColorUnified;
                        textColorUp = sf.SelectedTextColorUp;
                        textColorDown = sf.SelectedTextColorDown;

                        SaveSettings();
                        SetupFonts();
                        BuildContextMenu();
                        this.Invalidate();
                    }
                }
            }
            finally
            {
                updateTimer.Start();
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
    // RENDERIZADOR NATIVO DO WINDOWS 11 (TEMA ESCURO E CLARO)
    // ==========================================================================
    public class Windows11MenuRenderer : ToolStripProfessionalRenderer
    {
        private bool isDark;

        public Windows11MenuRenderer(bool isDark) : base(new Windows11ColorTable(isDark))
        {
            this.isDark = isDark;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                Rectangle rc = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                Color hoverBg = isDark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(230, 230, 230);
                using (SolidBrush b = new SolidBrush(hoverBg))
                {
                    e.Graphics.FillRectangle(b, rc);
                }
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (isDark)
            {
                e.TextColor = e.Item.Enabled ? Color.FromArgb(240, 240, 240) : Color.FromArgb(120, 120, 120);
            }
            else
            {
                e.TextColor = e.Item.Enabled ? Color.FromArgb(30, 30, 30) : Color.FromArgb(140, 140, 140);
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            r.Inflate(-2, -2);
            Color boxBg = Color.FromArgb(0, 120, 212);
            using (SolidBrush b = new SolidBrush(boxBg))
            {
                e.Graphics.FillRectangle(b, r);
            }

            using (Pen p = new Pen(Color.White, 1.8f))
            {
                PointF pt1 = new PointF(r.Left + 3f, r.Top + r.Height * 0.48f);
                PointF pt2 = new PointF(r.Left + r.Width * 0.4f, r.Bottom - 3.5f);
                PointF pt3 = new PointF(r.Right - 3f, r.Top + 3.5f);
                e.Graphics.DrawLines(p, new PointF[] { pt1, pt2, pt3 });
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = isDark ? Color.FromArgb(190, 190, 190) : Color.FromArgb(80, 80, 80);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int midY = e.Item.Height / 2;
            Color sepColor = isDark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(220, 220, 220);
            using (Pen p = new Pen(sepColor))
            {
                e.Graphics.DrawLine(p, 26, midY, e.Item.Width - 6, midY);
            }
        }
    }

    public class Windows11ColorTable : ProfessionalColorTable
    {
        private bool isDark;

        public Windows11ColorTable(bool isDark)
        {
            this.isDark = isDark;
            this.UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground { get { return isDark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(250, 250, 250); } }
        public override Color MenuBorder { get { return isDark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(215, 215, 215); } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return isDark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(235, 235, 235); } }
        public override Color MenuItemSelectedGradientBegin { get { return isDark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(235, 235, 235); } }
        public override Color MenuItemSelectedGradientEnd { get { return isDark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(235, 235, 235); } }
        public override Color MenuItemPressedGradientBegin { get { return isDark ? Color.FromArgb(40, 40, 40) : Color.FromArgb(225, 225, 225); } }
        public override Color MenuItemPressedGradientEnd { get { return isDark ? Color.FromArgb(40, 40, 40) : Color.FromArgb(225, 225, 225); } }
        public override Color ImageMarginGradientBegin { get { return isDark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(250, 250, 250); } }
        public override Color ImageMarginGradientMiddle { get { return isDark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(250, 250, 250); } }
        public override Color ImageMarginGradientEnd { get { return isDark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(250, 250, 250); } }
        public override Color CheckBackground { get { return Color.Transparent; } }
        public override Color CheckSelectedBackground { get { return Color.Transparent; } }
        public override Color CheckPressedBackground { get { return Color.Transparent; } }
        public override Color SeparatorDark { get { return isDark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(220, 220, 220); } }
        public override Color SeparatorLight { get { return Color.Transparent; } }
    }

    // ==========================================================================
    // JANELA MODERNA DE CONFIGURAÇÕES E PERSONALIZAÇÃO (v1.0.0)
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

        public string SelectedFontFamily { get; private set; }
        public float SelectedFontSize { get; private set; }
        public FontStyle SelectedFontStyle { get; private set; }

        public string SelectedArrowColorMode { get; private set; }
        public Color SelectedArrowColorUnified { get; private set; }
        public Color SelectedArrowColorUp { get; private set; }
        public Color SelectedArrowColorDown { get; private set; }

        public string SelectedTextColorMode { get; private set; }
        public Color SelectedTextColorUnified { get; private set; }
        public Color SelectedTextColorUp { get; private set; }
        public Color SelectedTextColorDown { get; private set; }

        // Abas
        private Button btnTabGeral;
        private Button btnTabFonte;
        private Button btnTabCores;
        private Button btnTabIcones;

        private Panel pnlGeral;
        private Panel pnlFonte;
        private Panel pnlCores;
        private Panel pnlIcones;

        // Geral
        private ComboBox cbLang;
        private ComboBox cbUnits;
        private RadioButton rbRight;
        private RadioButton rbLeft;
        private CheckBox chkLock;

        // Fonte
        private ComboBox cbFontFamily;
        private ComboBox cbFontSize;
        private ComboBox cbFontStyle;

        // Cores
        private RadioButton rbArrowUnified;
        private RadioButton rbArrowIndividual;
        private Button btnColorArrowBoth;
        private Button btnColorArrowUp;
        private Button btnColorArrowDown;

        private RadioButton rbTextUnified;
        private RadioButton rbTextIndividual;
        private Button btnColorTextBoth;
        private Button btnColorTextUp;
        private Button btnColorTextDown;

        // Ícones
        private ComboBox cbTemplate;
        private TextBox txtUp;
        private TextBox txtDown;

        // Prévia
        private Panel pnlPreview;

        public SettingsForm(
            Form parent,
            string lang,
            string unit,
            bool arrowRight,
            bool locked,
            string tpl,
            string upChar,
            string downChar,
            string fontFam,
            float fontSz,
            FontStyle fontSt,
            string arrColMode,
            Color arrColUni,
            Color arrColUp,
            Color arrColDn,
            string txtColMode,
            Color txtColUni,
            Color txtColUp,
            Color txtColDn)
        {
            this.SelectedLanguage = lang;
            this.SelectedUnitMode = unit;
            this.SelectedArrowOnRight = arrowRight;
            this.SelectedIsLocked = locked;
            this.SelectedIconTemplate = tpl;
            this.SelectedCustomUpChar = upChar;
            this.SelectedCustomDownChar = downChar;

            this.SelectedFontFamily = fontFam;
            this.SelectedFontSize = fontSz;
            this.SelectedFontStyle = fontSt;

            this.SelectedArrowColorMode = arrColMode;
            this.SelectedArrowColorUnified = arrColUni;
            this.SelectedArrowColorUp = arrColUp;
            this.SelectedArrowColorDown = arrColDn;

            this.SelectedTextColorMode = txtColMode;
            this.SelectedTextColorUnified = txtColUni;
            this.SelectedTextColorUp = txtColUp;
            this.SelectedTextColorDown = txtColDn;

            InitializeUI();
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        }

        private void InitializeUI()
        {
            bool isPt = SelectedLanguage == "pt";

            this.Text = isPt ? "Personalização do NetMonitor v1.0.0" : "NetMonitor Customization v1.0.0";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(540, 520);
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // ==================================================================
            // BOTÕES DE NAVEGAÇÃO DE ABAS
            // ==================================================================
            btnTabGeral = CreateTabButton(isPt ? "Geral" : "General", 15);
            btnTabFonte = CreateTabButton(isPt ? "Tipografia" : "Typography", 140);
            btnTabCores = CreateTabButton(isPt ? "Cores" : "Colors", 265);
            btnTabIcones = CreateTabButton(isPt ? "Ícones & Unicode" : "Icons & Unicode", 390);

            this.Controls.Add(btnTabGeral);
            this.Controls.Add(btnTabFonte);
            this.Controls.Add(btnTabCores);
            this.Controls.Add(btnTabIcones);

            btnTabGeral.Click += (s, e) => SwitchTab(0);
            btnTabFonte.Click += (s, e) => SwitchTab(1);
            btnTabCores.Click += (s, e) => SwitchTab(2);
            btnTabIcones.Click += (s, e) => SwitchTab(3);

            // ==================================================================
            // ÁREA INFERIOR FIXA: LIVE PREVIEW & BOTÕES SALVAR / CANCELAR
            // ==================================================================
            GroupBox gbPreview = new GroupBox
            {
                Text = isPt ? "Pré-visualização em Tempo Real (Live Preview)" : "Real-Time Live Preview",
                Location = new Point(15, 355),
                Size = new Size(510, 95),
                ForeColor = Color.FromArgb(200, 200, 200)
            };
            this.Controls.Add(gbPreview);

            pnlPreview = new Panel
            {
                Location = new Point(20, 28),
                Size = new Size(120, 48),
                BackColor = Color.FromArgb(28, 28, 28),
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlPreview.Paint += RenderLivePreview;
            gbPreview.Controls.Add(pnlPreview);

            Label lblPreviewHint = new Label
            {
                Text = isPt ? "Atualiza dinamicamente com todas as fontes, cores, ícones e layout escolhidos." : "Dynamically reflects chosen fonts, colors, icons and layout.",
                Location = new Point(155, 36),
                Size = new Size(340, 36),
                ForeColor = Color.FromArgb(170, 170, 170),
                Font = new Font(this.Font.FontFamily, 8.5f)
            };
            gbPreview.Controls.Add(lblPreviewHint);

            Button btnSave = new Button
            {
                Text = isPt ? "Salvar" : "Save",
                Location = new Point(315, 465),
                Size = new Size(100, 34),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(this.Font, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => ApplyAndClose();
            this.Controls.Add(btnSave);

            Button btnCancel = new Button
            {
                Text = isPt ? "Cancelar" : "Cancel",
                Location = new Point(425, 465),
                Size = new Size(100, 34),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(btnCancel);

            // ==================================================================
            // PAINEL 1: GERAL
            // ==================================================================
            pnlGeral = new Panel { Location = new Point(15, 55), Size = new Size(510, 290), BackColor = Color.Transparent };
            BuildPanelGeral(pnlGeral, isPt);
            this.Controls.Add(pnlGeral);

            // ==================================================================
            // PAINEL 2: TIPOGRAFIA
            // ==================================================================
            pnlFonte = new Panel { Location = new Point(15, 55), Size = new Size(510, 290), BackColor = Color.Transparent, Visible = false };
            BuildPanelFonte(pnlFonte, isPt);
            this.Controls.Add(pnlFonte);

            // ==================================================================
            // PAINEL 3: CORES
            // ==================================================================
            pnlCores = new Panel { Location = new Point(15, 55), Size = new Size(510, 290), BackColor = Color.Transparent, Visible = false };
            BuildPanelCores(pnlCores, isPt);
            this.Controls.Add(pnlCores);

            // ==================================================================
            // PAINEL 4: ÍCONES & UNICODE
            // ==================================================================
            pnlIcones = new Panel { Location = new Point(15, 55), Size = new Size(510, 290), BackColor = Color.Transparent, Visible = false };
            BuildPanelIcones(pnlIcones, isPt);
            this.Controls.Add(pnlIcones);

            SwitchTab(0);
        }

        private Button CreateTabButton(string text, int x)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, 14),
                Size = new Size(120, 32),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void SwitchTab(int tabIndex)
        {
            btnTabGeral.BackColor = (tabIndex == 0) ? Color.FromArgb(0, 120, 212) : Color.FromArgb(45, 45, 45);
            btnTabFonte.BackColor = (tabIndex == 1) ? Color.FromArgb(0, 120, 212) : Color.FromArgb(45, 45, 45);
            btnTabCores.BackColor = (tabIndex == 2) ? Color.FromArgb(0, 120, 212) : Color.FromArgb(45, 45, 45);
            btnTabIcones.BackColor = (tabIndex == 3) ? Color.FromArgb(0, 120, 212) : Color.FromArgb(45, 45, 45);

            pnlGeral.Visible = (tabIndex == 0);
            pnlFonte.Visible = (tabIndex == 1);
            pnlCores.Visible = (tabIndex == 2);
            pnlIcones.Visible = (tabIndex == 3);
        }

        private void BuildPanelGeral(Panel pnl, bool isPt)
        {
            int y = 10;

            // Idioma
            Label lblL = new Label { Text = isPt ? "Idioma / Language:" : "Language / Idioma:", Location = new Point(10, y + 3), AutoSize = true };
            pnl.Controls.Add(lblL);
            cbLang = new ComboBox { Location = new Point(200, y), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            cbLang.Items.Add("Português (Brasil)");
            cbLang.Items.Add("English");
            cbLang.SelectedIndex = (SelectedLanguage == "pt") ? 0 : 1;
            pnl.Controls.Add(cbLang);

            y += 40;

            // Unidade
            Label lblU = new Label { Text = isPt ? "Unidade de Velocidade:" : "Speed Unit:", Location = new Point(10, y + 3), AutoSize = true };
            pnl.Controls.Add(lblU);
            cbUnits = new ComboBox { Location = new Point(200, y), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            cbUnits.Items.Add(isPt ? "Automático (Kbps / Mbps / Gbps)" : "Automatic (Kbps / Mbps / Gbps)");
            cbUnits.Items.Add("Sempre em Kbps / Always Kbps");
            cbUnits.Items.Add("Sempre em Mbps / Always Mbps");
            cbUnits.Items.Add("Sempre em Gbps / Always Gbps");
            if (SelectedUnitMode == "Kbps") cbUnits.SelectedIndex = 1;
            else if (SelectedUnitMode == "Mbps") cbUnits.SelectedIndex = 2;
            else if (SelectedUnitMode == "Gbps") cbUnits.SelectedIndex = 3;
            else cbUnits.SelectedIndex = 0;
            cbUnits.SelectedIndexChanged += (s, e) => pnlPreview.Invalidate();
            pnl.Controls.Add(cbUnits);

            y += 45;

            // Posição das Setas
            GroupBox gbLayout = new GroupBox
            {
                Text = isPt ? "Posição das Setas na Interface" : "Arrow Position",
                Location = new Point(10, y),
                Size = new Size(490, 85),
                ForeColor = Color.FromArgb(200, 200, 200)
            };
            pnl.Controls.Add(gbLayout);

            rbRight = new RadioButton
            {
                Text = isPt ? "Setas à Direita: [ 823.6 Mbps ▲ ] (Recomendado)" : "Arrows on Right: [ 823.6 Mbps ▲ ] (Recommended)",
                Location = new Point(15, 24),
                AutoSize = true,
                Checked = SelectedArrowOnRight,
                ForeColor = Color.White
            };
            rbLeft = new RadioButton
            {
                Text = isPt ? "Setas à Esquerda: [ ▲ 823.6 Mbps ]" : "Arrows on Left: [ ▲ 823.6 Mbps ]",
                Location = new Point(15, 52),
                AutoSize = true,
                Checked = !SelectedArrowOnRight,
                ForeColor = Color.White
            };
            rbRight.CheckedChanged += (s, e) => pnlPreview.Invalidate();
            rbLeft.CheckedChanged += (s, e) => pnlPreview.Invalidate();
            gbLayout.Controls.Add(rbRight);
            gbLayout.Controls.Add(rbLeft);

            y += 100;

            // Travar Posição
            chkLock = new CheckBox
            {
                Text = isPt ? "Travar posição na barra de tarefas (bloqueia arrasto acidental)" : "Lock position on taskbar (prevents accidental drag)",
                Location = new Point(10, y),
                AutoSize = true,
                Checked = SelectedIsLocked,
                ForeColor = Color.FromArgb(230, 230, 230)
            };
            pnl.Controls.Add(chkLock);
        }

        private void BuildPanelFonte(Panel pnl, bool isPt)
        {
            int y = 15;

            // Família
            Label lblFam = new Label { Text = isPt ? "Família da Fonte:" : "Font Family:", Location = new Point(10, y + 3), AutoSize = true };
            pnl.Controls.Add(lblFam);

            cbFontFamily = new ComboBox { Location = new Point(190, y), Width = 290, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            cbFontFamily.Items.Add("Segoe UI Variable Display");
            cbFontFamily.Items.Add("Segoe UI");
            cbFontFamily.Items.Add("Cascadia Code");
            cbFontFamily.Items.Add("Consolas");
            cbFontFamily.Items.Add("Arial");
            cbFontFamily.Items.Add("Bahnschrift");
            cbFontFamily.Items.Add("Calibri");
            cbFontFamily.Items.Add("Trebuchet MS");
            cbFontFamily.Items.Add("Verdana");

            int idxFam = 1;
            for (int i = 0; i < cbFontFamily.Items.Count; i++)
            {
                if (string.Equals(cbFontFamily.Items[i].ToString(), SelectedFontFamily, StringComparison.OrdinalIgnoreCase))
                {
                    idxFam = i;
                    break;
                }
            }
            cbFontFamily.SelectedIndex = idxFam;
            cbFontFamily.SelectedIndexChanged += (s, e) => pnlPreview.Invalidate();
            pnl.Controls.Add(cbFontFamily);

            y += 45;

            // Tamanho
            Label lblSz = new Label { Text = isPt ? "Tamanho da Fonte:" : "Font Size:", Location = new Point(10, y + 3), AutoSize = true };
            pnl.Controls.Add(lblSz);

            cbFontSize = new ComboBox { Location = new Point(190, y), Width = 290, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            cbFontSize.Items.Add("8.5 pt");
            cbFontSize.Items.Add("9.0 pt");
            cbFontSize.Items.Add("9.5 pt");
            cbFontSize.Items.Add("10.0 pt");
            cbFontSize.Items.Add("10.5 pt (Padrão)");
            cbFontSize.Items.Add("11.0 pt");
            cbFontSize.Items.Add("11.5 pt");
            cbFontSize.Items.Add("12.0 pt");

            if (Math.Abs(SelectedFontSize - 8.5f) < 0.1) cbFontSize.SelectedIndex = 0;
            else if (Math.Abs(SelectedFontSize - 9.0f) < 0.1) cbFontSize.SelectedIndex = 1;
            else if (Math.Abs(SelectedFontSize - 9.5f) < 0.1) cbFontSize.SelectedIndex = 2;
            else if (Math.Abs(SelectedFontSize - 10.0f) < 0.1) cbFontSize.SelectedIndex = 3;
            else if (Math.Abs(SelectedFontSize - 11.0f) < 0.1) cbFontSize.SelectedIndex = 5;
            else if (Math.Abs(SelectedFontSize - 11.5f) < 0.1) cbFontSize.SelectedIndex = 6;
            else if (Math.Abs(SelectedFontSize - 12.0f) < 0.1) cbFontSize.SelectedIndex = 7;
            else cbFontSize.SelectedIndex = 4; // 10.5pt
            cbFontSize.SelectedIndexChanged += (s, e) => pnlPreview.Invalidate();
            pnl.Controls.Add(cbFontSize);

            y += 45;

            // Estilo
            Label lblSt = new Label { Text = isPt ? "Estilo da Fonte:" : "Font Style:", Location = new Point(10, y + 3), AutoSize = true };
            pnl.Controls.Add(lblSt);

            cbFontStyle = new ComboBox { Location = new Point(190, y), Width = 290, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            cbFontStyle.Items.Add(isPt ? "Regular (Normal)" : "Regular");
            cbFontStyle.Items.Add(isPt ? "Negrito (Bold)" : "Bold");
            cbFontStyle.Items.Add(isPt ? "Itálico (Italic)" : "Italic");

            if (SelectedFontStyle == FontStyle.Bold) cbFontStyle.SelectedIndex = 1;
            else if (SelectedFontStyle == FontStyle.Italic) cbFontStyle.SelectedIndex = 2;
            else cbFontStyle.SelectedIndex = 0;
            cbFontStyle.SelectedIndexChanged += (s, e) => pnlPreview.Invalidate();
            pnl.Controls.Add(cbFontStyle);

            y += 55;

            Label lblTip = new Label
            {
                Text = isPt ? "Dica: A fonte dos números adapta a unidade proporcionalmente mantendo a legibilidade na barra de tarefas." : "Tip: The font size automatically scales units while preserving taskbar legibility.",
                Location = new Point(10, y),
                Size = new Size(480, 50),
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font(this.Font.FontFamily, 8.5f)
            };
            pnl.Controls.Add(lblTip);
        }

        private void BuildPanelCores(Panel pnl, bool isPt)
        {
            int y = 5;

            // Grupo Cores das Setas
            GroupBox gbArrowCol = new GroupBox
            {
                Text = isPt ? "Cores das Setas" : "Arrow Colors",
                Location = new Point(10, y),
                Size = new Size(490, 110),
                ForeColor = Color.FromArgb(200, 200, 200)
            };
            pnl.Controls.Add(gbArrowCol);

            rbArrowUnified = new RadioButton { Text = isPt ? "Cor Única para Ambas:" : "Unified Color:", Location = new Point(15, 22), AutoSize = true, Checked = (SelectedArrowColorMode != "Individual"), ForeColor = Color.White };
            rbArrowIndividual = new RadioButton { Text = isPt ? "Individuais (Upload / Download):" : "Individual (Up / Down):", Location = new Point(15, 62), AutoSize = true, Checked = (SelectedArrowColorMode == "Individual"), ForeColor = Color.White };

            btnColorArrowBoth = CreateColorSwatchButton(SelectedArrowColorUnified, 270, 20);
            btnColorArrowUp = CreateColorSwatchButton(SelectedArrowColorUp, 270, 60);
            btnColorArrowDown = CreateColorSwatchButton(SelectedArrowColorDown, 360, 60);

            Label lblArrUp = new Label { Text = "Up", Location = new Point(245, 65), AutoSize = true, ForeColor = Color.FromArgb(180, 180, 180) };
            Label lblArrDn = new Label { Text = "Down", Location = new Point(320, 65), AutoSize = true, ForeColor = Color.FromArgb(180, 180, 180) };

            gbArrowCol.Controls.Add(rbArrowUnified);
            gbArrowCol.Controls.Add(rbArrowIndividual);
            gbArrowCol.Controls.Add(btnColorArrowBoth);
            gbArrowCol.Controls.Add(lblArrUp);
            gbArrowCol.Controls.Add(btnColorArrowUp);
            gbArrowCol.Controls.Add(lblArrDn);
            gbArrowCol.Controls.Add(btnColorArrowDown);

            btnColorArrowBoth.Click += (s, e) => PickColor(btnColorArrowBoth, c => { SelectedArrowColorUnified = c; pnlPreview.Invalidate(); });
            btnColorArrowUp.Click += (s, e) => PickColor(btnColorArrowUp, c => { SelectedArrowColorUp = c; pnlPreview.Invalidate(); });
            btnColorArrowDown.Click += (s, e) => PickColor(btnColorArrowDown, c => { SelectedArrowColorDown = c; pnlPreview.Invalidate(); });

            rbArrowUnified.CheckedChanged += (s, e) => UpdateColorControlsState();
            rbArrowIndividual.CheckedChanged += (s, e) => UpdateColorControlsState();

            y += 120;

            // Grupo Cores dos Textos
            GroupBox gbTextCol = new GroupBox
            {
                Text = isPt ? "Cores dos Textos (Velocidade & Unidade)" : "Text Colors (Speed & Units)",
                Location = new Point(10, y),
                Size = new Size(490, 110),
                ForeColor = Color.FromArgb(200, 200, 200)
            };
            pnl.Controls.Add(gbTextCol);

            rbTextUnified = new RadioButton { Text = isPt ? "Cor Única para Ambos:" : "Unified Color:", Location = new Point(15, 22), AutoSize = true, Checked = (SelectedTextColorMode != "Individual"), ForeColor = Color.White };
            rbTextIndividual = new RadioButton { Text = isPt ? "Individuais (Upload / Download):" : "Individual (Up / Down):", Location = new Point(15, 62), AutoSize = true, Checked = (SelectedTextColorMode == "Individual"), ForeColor = Color.White };

            btnColorTextBoth = CreateColorSwatchButton(SelectedTextColorUnified, 270, 20);
            btnColorTextUp = CreateColorSwatchButton(SelectedTextColorUp, 270, 60);
            btnColorTextDown = CreateColorSwatchButton(SelectedTextColorDown, 360, 60);

            Label lblTxtUp = new Label { Text = "Up", Location = new Point(245, 65), AutoSize = true, ForeColor = Color.FromArgb(180, 180, 180) };
            Label lblTxtDn = new Label { Text = "Down", Location = new Point(320, 65), AutoSize = true, ForeColor = Color.FromArgb(180, 180, 180) };

            gbTextCol.Controls.Add(rbTextUnified);
            gbTextCol.Controls.Add(rbTextIndividual);
            gbTextCol.Controls.Add(btnColorTextBoth);
            gbTextCol.Controls.Add(lblTxtUp);
            gbTextCol.Controls.Add(btnColorTextUp);
            gbTextCol.Controls.Add(lblTxtDn);
            gbTextCol.Controls.Add(btnColorTextDown);

            btnColorTextBoth.Click += (s, e) => PickColor(btnColorTextBoth, c => { SelectedTextColorUnified = c; pnlPreview.Invalidate(); });
            btnColorTextUp.Click += (s, e) => PickColor(btnColorTextUp, c => { SelectedTextColorUp = c; pnlPreview.Invalidate(); });
            btnColorTextDown.Click += (s, e) => PickColor(btnColorTextDown, c => { SelectedTextColorDown = c; pnlPreview.Invalidate(); });

            rbTextUnified.CheckedChanged += (s, e) => UpdateColorControlsState();
            rbTextIndividual.CheckedChanged += (s, e) => UpdateColorControlsState();

            y += 115;

            // Paletas Rápidas
            Label lblPresets = new Label { Text = isPt ? "Paletas Rápidas:" : "Quick Palettes:", Location = new Point(10, y + 4), AutoSize = true, ForeColor = Color.FromArgb(180, 180, 180) };
            pnl.Controls.Add(lblPresets);

            Button btnP1 = CreatePresetButton(isPt ? "Branco" : "White", 125, y, Color.White, Color.FromArgb(225, 225, 225));
            Button btnP2 = CreatePresetButton("Verde/Verm.", 205, y, Color.FromArgb(0, 230, 118), Color.FromArgb(255, 82, 82));
            Button btnP3 = CreatePresetButton("Ciano/Laran.", 300, y, Color.FromArgb(0, 229, 255), Color.FromArgb(255, 145, 0));
            Button btnP4 = CreatePresetButton("Azul Fluent", 400, y, Color.FromArgb(0, 120, 212), Color.White);

            pnl.Controls.Add(btnP1);
            pnl.Controls.Add(btnP2);
            pnl.Controls.Add(btnP3);
            pnl.Controls.Add(btnP4);

            btnP1.Click += (s, e) =>
            {
                rbArrowUnified.Checked = true;
                rbTextUnified.Checked = true;
                SelectedArrowColorUnified = Color.FromArgb(225, 225, 225);
                SelectedTextColorUnified = Color.White;
                btnColorArrowBoth.BackColor = SelectedArrowColorUnified;
                btnColorTextBoth.BackColor = SelectedTextColorUnified;
                pnlPreview.Invalidate();
            };

            btnP2.Click += (s, e) =>
            {
                rbArrowIndividual.Checked = true;
                rbTextIndividual.Checked = true;
                SelectedArrowColorUp = Color.FromArgb(0, 230, 118);
                SelectedTextColorUp = Color.FromArgb(0, 230, 118);
                SelectedArrowColorDown = Color.FromArgb(255, 82, 82);
                SelectedTextColorDown = Color.FromArgb(255, 82, 82);
                btnColorArrowUp.BackColor = SelectedArrowColorUp;
                btnColorTextUp.BackColor = SelectedTextColorUp;
                btnColorArrowDown.BackColor = SelectedArrowColorDown;
                btnColorTextDown.BackColor = SelectedTextColorDown;
                pnlPreview.Invalidate();
            };

            btnP3.Click += (s, e) =>
            {
                rbArrowIndividual.Checked = true;
                rbTextIndividual.Checked = true;
                SelectedArrowColorUp = Color.FromArgb(0, 229, 255);
                SelectedTextColorUp = Color.FromArgb(0, 229, 255);
                SelectedArrowColorDown = Color.FromArgb(255, 145, 0);
                SelectedTextColorDown = Color.FromArgb(255, 145, 0);
                btnColorArrowUp.BackColor = SelectedArrowColorUp;
                btnColorTextUp.BackColor = SelectedTextColorUp;
                btnColorArrowDown.BackColor = SelectedArrowColorDown;
                btnColorTextDown.BackColor = SelectedTextColorDown;
                pnlPreview.Invalidate();
            };

            btnP4.Click += (s, e) =>
            {
                rbArrowUnified.Checked = true;
                rbTextUnified.Checked = true;
                SelectedArrowColorUnified = Color.FromArgb(0, 120, 212);
                SelectedTextColorUnified = Color.White;
                btnColorArrowBoth.BackColor = SelectedArrowColorUnified;
                btnColorTextBoth.BackColor = SelectedTextColorUnified;
                pnlPreview.Invalidate();
            };

            UpdateColorControlsState();
        }

        private Button CreateColorSwatchButton(Color col, int x, int y)
        {
            Button btn = new Button
            {
                Location = new Point(x, y),
                Size = new Size(36, 26),
                BackColor = col,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
            return btn;
        }

        private Button CreatePresetButton(string text, int x, int y, Color c1, Color c2)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(88, 28),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            return btn;
        }

        private void PickColor(Button targetBtn, Action<Color> onSelected)
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = targetBtn.BackColor;
                if (cd.ShowDialog(this) == DialogResult.OK)
                {
                    targetBtn.BackColor = cd.Color;
                    onSelected(cd.Color);
                }
            }
        }

        private void UpdateColorControlsState()
        {
            if (rbArrowUnified == null || btnColorArrowBoth == null ||
                btnColorArrowUp == null || btnColorArrowDown == null ||
                rbTextUnified == null || btnColorTextBoth == null ||
                btnColorTextUp == null || btnColorTextDown == null)
            {
                return;
            }

            bool arrowUni = rbArrowUnified.Checked;
            btnColorArrowBoth.Enabled = arrowUni;
            btnColorArrowUp.Enabled = !arrowUni;
            btnColorArrowDown.Enabled = !arrowUni;

            bool textUni = rbTextUnified.Checked;
            btnColorTextBoth.Enabled = textUni;
            btnColorTextUp.Enabled = !textUni;
            btnColorTextDown.Enabled = !textUni;

            if (pnlPreview != null) pnlPreview.Invalidate();
        }

        private void BuildPanelIcones(Panel pnl, bool isPt)
        {
            int y = 15;

            Label lblTpl = new Label { Text = isPt ? "Modelo Pré-definido:" : "Preset Template:", Location = new Point(10, y + 3), AutoSize = true };
            pnl.Controls.Add(lblTpl);

            cbTemplate = new ComboBox
            {
                Location = new Point(190, y),
                Width = 290,
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

            pnl.Controls.Add(cbTemplate);

            y += 50;

            Label lblUp = new Label { Text = isPt ? "Upload (Caractere ou U+XXXX):" : "Upload (Char or U+XXXX):", Location = new Point(10, y + 3), AutoSize = true };
            pnl.Controls.Add(lblUp);
            txtUp = new TextBox { Location = new Point(230, y), Width = 100, Text = SelectedCustomUpChar, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            pnl.Controls.Add(txtUp);

            y += 40;

            Label lblDown = new Label { Text = isPt ? "Download (Caractere ou U+XXXX):" : "Download (Char or U+XXXX):", Location = new Point(10, y + 3), AutoSize = true };
            pnl.Controls.Add(lblDown);
            txtDown = new TextBox { Location = new Point(230, y), Width = 100, Text = SelectedCustomDownChar, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            pnl.Controls.Add(txtDown);

            y += 50;

            Label lblHint = new Label
            {
                Text = isPt ? "Suporta caracteres colados diretamente (▲, 🠕, ↑) ou códigos hexadecimais como U+25B2, 0x25B2 ou 25B2." : "Supports direct chars (▲, ↑) or hexadecimal codes like U+25B2, 0x25B2 or 25B2.",
                Location = new Point(10, y),
                Size = new Size(480, 50),
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font(this.Font.FontFamily, 8.5f)
            };
            pnl.Controls.Add(lblHint);

            txtUp.TextChanged += (s, e) => pnlPreview.Invalidate();
            txtDown.TextChanged += (s, e) => pnlPreview.Invalidate();

            cbTemplate.SelectedIndexChanged += (s, e) =>
            {
                if (cbTemplate.SelectedIndex == 0) { txtUp.Text = "▲"; txtDown.Text = "▼"; }
                else if (cbTemplate.SelectedIndex == 1) { txtUp.Text = "∧"; txtDown.Text = "∨"; }
                else if (cbTemplate.SelectedIndex == 2) { txtUp.Text = "↑"; txtDown.Text = "↓"; }
                else if (cbTemplate.SelectedIndex == 3) { txtUp.Text = "▲"; txtDown.Text = "▼"; }
                pnlPreview.Invalidate();
            };
        }

        private void RenderLivePreview(object sender, PaintEventArgs e)
        {
            if (cbFontFamily == null || cbFontSize == null || cbFontStyle == null ||
                rbRight == null || rbTextUnified == null || rbArrowUnified == null ||
                btnColorTextBoth == null || btnColorTextUp == null || btnColorTextDown == null ||
                btnColorArrowBoth == null || btnColorArrowUp == null || btnColorArrowDown == null ||
                txtUp == null || txtDown == null || cbTemplate == null)
            {
                return;
            }

            Graphics g = e.Graphics;
            g.Clear(Color.FromArgb(28, 28, 28));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            string fontFam = cbFontFamily.SelectedItem != null ? cbFontFamily.SelectedItem.ToString() : "Segoe UI";
            float fontSz = 10.5f;
            if (cbFontSize.SelectedIndex == 0) fontSz = 8.5f;
            else if (cbFontSize.SelectedIndex == 1) fontSz = 9.0f;
            else if (cbFontSize.SelectedIndex == 2) fontSz = 9.5f;
            else if (cbFontSize.SelectedIndex == 3) fontSz = 10.0f;
            else if (cbFontSize.SelectedIndex == 5) fontSz = 11.0f;
            else if (cbFontSize.SelectedIndex == 6) fontSz = 11.5f;
            else if (cbFontSize.SelectedIndex == 7) fontSz = 12.0f;

            FontStyle fSt = FontStyle.Regular;
            if (cbFontStyle.SelectedIndex == 1) fSt = FontStyle.Bold;
            else if (cbFontStyle.SelectedIndex == 2) fSt = FontStyle.Italic;

            bool isRight = rbRight.Checked;

            Color cTextUp = rbTextUnified.Checked ? btnColorTextBoth.BackColor : btnColorTextUp.BackColor;
            Color cTextDn = rbTextUnified.Checked ? btnColorTextBoth.BackColor : btnColorTextDown.BackColor;

            Color cArrUp = rbArrowUnified.Checked ? btnColorArrowBoth.BackColor : btnColorArrowUp.BackColor;
            Color cArrDn = rbArrowUnified.Checked ? btnColorArrowBoth.BackColor : btnColorArrowDown.BackColor;

            string upChar = NetMonitorApp.ParseUnicodeString(txtUp.Text, "▲");
            string dnChar = NetMonitorApp.ParseUnicodeString(txtDown.Text, "▼");

            using (Font fNum = new Font(fontFam, fontSz, fSt))
            using (Font fUnit = new Font(fontFam, Math.Max(7.0f, fontSz - 1.5f), FontStyle.Regular))
            using (Font fUni = new Font("Segoe UI Symbol", Math.Max(8.0f, fontSz - 1.0f), FontStyle.Regular))
            using (Brush bTxtUp = new SolidBrush(cTextUp))
            using (Brush bTxtDn = new SolidBrush(cTextDn))
            using (Brush bArrUp = new SolidBrush(cArrUp))
            using (Brush bArrDn = new SolidBrush(cArrDn))
            using (StringFormat sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            using (StringFormat sfL = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (StringFormat sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                if (isRight)
                {
                    g.DrawString("823.6", fNum, bTxtUp, new RectangleF(2, 4, 50, 18), sfR);
                    g.DrawString("142.0", fNum, bTxtDn, new RectangleF(2, 24, 50, 18), sfR);

                    g.DrawString("Mbps", fUnit, bTxtUp, new RectangleF(53, 4, 38, 18), sfL);
                    g.DrawString("Mbps", fUnit, bTxtDn, new RectangleF(53, 24, 38, 18), sfL);

                    if (cbTemplate.SelectedIndex == 0)
                    {
                        g.FillPolygon(bArrUp, new PointF[] { new PointF(105, 7), new PointF(101, 13), new PointF(109, 13) });
                        g.FillPolygon(bArrDn, new PointF[] { new PointF(105, 39), new PointF(101, 33), new PointF(109, 33) });
                    }
                    else if (cbTemplate.SelectedIndex == 1)
                    {
                        using (Pen pU = new Pen(cArrUp, 1.4f))
                        using (Pen pD = new Pen(cArrDn, 1.4f))
                        {
                            g.DrawLines(pU, new PointF[] { new PointF(101, 12), new PointF(105, 8), new PointF(109, 12) });
                            g.DrawLines(pD, new PointF[] { new PointF(101, 34), new PointF(105, 38), new PointF(109, 34) });
                        }
                    }
                    else
                    {
                        g.DrawString(upChar, fUni, bArrUp, new RectangleF(97, 4, 16, 18), sfC);
                        g.DrawString(dnChar, fUni, bArrDn, new RectangleF(97, 24, 16, 18), sfC);
                    }
                }
                else
                {
                    if (cbTemplate.SelectedIndex == 0)
                    {
                        g.FillPolygon(bArrUp, new PointF[] { new PointF(10, 7), new PointF(6, 13), new PointF(14, 13) });
                        g.FillPolygon(bArrDn, new PointF[] { new PointF(10, 39), new PointF(6, 33), new PointF(14, 33) });
                    }
                    else if (cbTemplate.SelectedIndex == 1)
                    {
                        using (Pen pU = new Pen(cArrUp, 1.4f))
                        using (Pen pD = new Pen(cArrDn, 1.4f))
                        {
                            g.DrawLines(pU, new PointF[] { new PointF(6, 12), new PointF(10, 8), new PointF(14, 12) });
                            g.DrawLines(pD, new PointF[] { new PointF(6, 34), new PointF(10, 38), new PointF(14, 34) });
                        }
                    }
                    else
                    {
                        g.DrawString(upChar, fUni, bArrUp, new RectangleF(2, 4, 16, 18), sfC);
                        g.DrawString(dnChar, fUni, bArrDn, new RectangleF(2, 24, 16, 18), sfC);
                    }

                    g.DrawString("823.6", fNum, bTxtUp, new RectangleF(18, 4, 46, 18), sfR);
                    g.DrawString("142.0", fNum, bTxtDn, new RectangleF(18, 24, 46, 18), sfR);

                    g.DrawString("Mbps", fUnit, bTxtUp, new RectangleF(66, 4, 38, 18), sfL);
                    g.DrawString("Mbps", fUnit, bTxtDn, new RectangleF(66, 24, 38, 18), sfL);
                }
            }
        }

        private void ApplyAndClose()
        {
            SelectedLanguage = (cbLang.SelectedIndex == 0) ? "pt" : "en";

            if (cbUnits.SelectedIndex == 1) SelectedUnitMode = "Kbps";
            else if (cbUnits.SelectedIndex == 2) SelectedUnitMode = "Mbps";
            else if (cbUnits.SelectedIndex == 3) SelectedUnitMode = "Gbps";
            else SelectedUnitMode = "Auto";

            SelectedArrowOnRight = rbRight.Checked;
            SelectedIsLocked = chkLock.Checked;

            SelectedFontFamily = cbFontFamily.SelectedItem != null ? cbFontFamily.SelectedItem.ToString() : "Segoe UI";

            if (cbFontSize.SelectedIndex == 0) SelectedFontSize = 8.5f;
            else if (cbFontSize.SelectedIndex == 1) SelectedFontSize = 9.0f;
            else if (cbFontSize.SelectedIndex == 2) SelectedFontSize = 9.5f;
            else if (cbFontSize.SelectedIndex == 3) SelectedFontSize = 10.0f;
            else if (cbFontSize.SelectedIndex == 5) SelectedFontSize = 11.0f;
            else if (cbFontSize.SelectedIndex == 6) SelectedFontSize = 11.5f;
            else if (cbFontSize.SelectedIndex == 7) SelectedFontSize = 12.0f;
            else SelectedFontSize = 10.5f;

            if (cbFontStyle.SelectedIndex == 1) SelectedFontStyle = FontStyle.Bold;
            else if (cbFontStyle.SelectedIndex == 2) SelectedFontStyle = FontStyle.Italic;
            else SelectedFontStyle = FontStyle.Regular;

            SelectedArrowColorMode = rbArrowIndividual.Checked ? "Individual" : "Unified";
            SelectedArrowColorUnified = btnColorArrowBoth.BackColor;
            SelectedArrowColorUp = btnColorArrowUp.BackColor;
            SelectedArrowColorDown = btnColorArrowDown.BackColor;

            SelectedTextColorMode = rbTextIndividual.Checked ? "Individual" : "Unified";
            SelectedTextColorUnified = btnColorTextBoth.BackColor;
            SelectedTextColorUp = btnColorTextUp.BackColor;
            SelectedTextColorDown = btnColorTextDown.BackColor;

            if (cbTemplate.SelectedIndex == 0) SelectedIconTemplate = "VectorTriangle";
            else if (cbTemplate.SelectedIndex == 1) SelectedIconTemplate = "VectorChevron";
            else if (cbTemplate.SelectedIndex == 2) SelectedIconTemplate = "UnicodeClassic";
            else if (cbTemplate.SelectedIndex == 3) SelectedIconTemplate = "UnicodeSolid";
            else SelectedIconTemplate = "UnicodeCustom";

            SelectedCustomUpChar = NetMonitorApp.ParseUnicodeString(txtUp.Text, "▲");
            SelectedCustomDownChar = NetMonitorApp.ParseUnicodeString(txtDown.Text, "▼");

            this.Close();
        }
    }
}
