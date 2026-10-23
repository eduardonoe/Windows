// ==============================================================================
// NetMonitor - Monitor de Rede Ultraleve para Barra de Tarefas (Windows 11)
// Autor: Eduardo Noé (eduardonoe)
// Version: 1.0.0
//
// v1.0.0 changelog:
// - Arquitetura Proposta B: Menu de Configurações com Sidebar Lateral nativo Windows 11.
// - Seleção independente de cores para: Números, Unidades (Kbps/Mbps/MB/s) e Setas de Tráfego.
// - Suporte completo à alternância entre Bits (Mbps / Kbps / Gbps) e Bytes (MB/s / KB/s / GB/s), padrão Bits.
// - Padrão de fábrica calibrado: Segoe UI Variable Display, 9.5 pt Bold, Setas à Direita.
// - Novos modelos de setas: Chevrons Duplos Velocidade (▲▲/▼▼) e Stream Tech 45° (↗/↘).
// - Auto-ocultação no Screensaver e na Tela de Bloqueio do Windows (SessionSwitch + SPI_GETSCREENSAVERRUNNING).
// - Proteção estrita contra invasão de tela em múltiplos monitores (Clamp no monitor ativo).
// - Desempenho extremo: Cache de Brushes/Pens GDI+ e supressão de Invalidate() ocioso (0% CPU, RAM mínima).
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("NetMonitor")]
[assembly: AssemblyDescription("Monitor de Rede Ultraleve para Barra de Tarefas do Windows 11")]
[assembly: AssemblyCompany("Eduardo Noé")]
[assembly: AssemblyProduct("NetMonitor")]
[assembly: AssemblyCopyright("Copyright © 2026 Eduardo Noé")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

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

        private const uint SPI_GETSCREENSAVERRUNNING = 0x0072;

        private const string REG_KEY_APP = @"Software\NetMonitor";
        private const string REG_KEY_RUN = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "NetMonitor";

        // Dimensões do Widget na Barra de Tarefas
        private const int WIDGET_WIDTH = 106;
        private const int WIDGET_HEIGHT = 38;

        // Paleta base do widget
        private static readonly Color BG_COLOR_DARK = Color.FromArgb(28, 28, 28);
        private static readonly Color BG_COLOR_LIGHT = Color.FromArgb(243, 243, 243);
        private Color currentBgColor = Color.FromArgb(28, 28, 28);
        private bool followSystemTheme = true;
        private bool lastThemeIsDark = true;

        // Tipografia
        private string fontFamily = "Segoe UI Variable Display";
        private float fontSize = 9.5f;
        private FontStyle fontStyle = FontStyle.Bold;
        private Font fontNum;
        private Font fontUnit;
        private Font fontUnicode;

        // Cores dos Números
        private string textColorMode = "Unified";    // "Unified" ou "Individual"
        private Color textColorUnified = Color.FromArgb(255, 255, 255);
        private Color textColorUp = Color.FromArgb(255, 255, 255);
        private Color textColorDown = Color.FromArgb(255, 255, 255);

        // Cores das Unidades (Kbps / Mbps / MB/s)
        private string unitColorMode = "Inherit";    // "Inherit", "Unified", ou "Individual"
        private Color unitColorUnified = Color.FromArgb(190, 190, 190);
        private Color unitColorUp = Color.FromArgb(190, 190, 190);
        private Color unitColorDown = Color.FromArgb(190, 190, 190);

        // Cores das Setas
        private string arrowColorMode = "Unified";   // "Unified" ou "Individual"
        private Color arrowColorUnified = Color.FromArgb(235, 235, 235);
        private Color arrowColorUp = Color.FromArgb(235, 235, 235);
        private Color arrowColorDown = Color.FromArgb(235, 235, 235);

        // GDI+ Resource Cache (Zero alocação no OnPaint)
        private SolidBrush brushNumUp;
        private SolidBrush brushNumDown;
        private SolidBrush brushUnitUp;
        private SolidBrush brushUnitDown;
        private SolidBrush brushArrowUp;
        private SolidBrush brushArrowDown;
        private Pen penArrowUp;
        private Pen penArrowDown;

        // Alinhamentos de texto reutilizáveis
        private StringFormat sfRight;
        private StringFormat sfLeft;
        private StringFormat sfCenter;

        // Configurações
        private string selectedNicId = "Auto";
        private string activeNicDisplayName = "Auto";
        private string currentLanguage = "pt";
        private string speedUnitFormat = "Bits";      // "Bits" (Mbps/Kbps/Gbps) ou "Bytes" (MB/s/KB/s/GB/s)
        private string unitMode = "Auto";             // "Auto", ou fixa
        private bool arrowOnRight = true;             // Seta à direita por padrão
        private bool isLocked = false;                // Trava de posição
        private bool hideOnScreensaver = true;        // Ocultar em screensaver / lock screen
        private string iconTemplate = "VectorTriangle";
        private string customUpChar = "▲";
        private string customDownChar = "▼";

        // Estado do Screensaver / Sessão
        private bool isSessionLocked = false;
        private bool isScreensaverRunning = false;

        // Medição e Cache de Tráfego
        private Timer updateTimer;
        private NetworkInterface cachedNic = null;
        private long lastBytesIn = -1;
        private long lastBytesOut = -1;
        private DateTime lastMeasurementTime = DateTime.MinValue;

        // Valores de exibição atuais
        private string strUpNum = "0.0";
        private string strUpUnit = "Kbps";
        private string strDownNum = "0.0";
        private string strDownUnit = "Kbps";

        // Componentes UI
        private ContextMenuStrip contextMenu;
        private NotifyIcon trayIcon;

        public NetMonitorApp()
        {
            InitializeComponent();
            LoadSettings();
            ApplySystemTheme();
            SetupFonts();
            UpdateCachedGdiResources();
            BuildContextMenu();
            SetupTimer();
            PositionWidget();
            BindToTaskbarOwner();

            try
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                SystemEvents.SessionSwitch += OnSessionSwitch;
                GC.Collect(2, GCCollectionMode.Optimized);
            }
            catch { }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            try
            {
                if (e.Reason == SessionSwitchReason.SessionLock)
                {
                    isSessionLocked = true;
                    if (hideOnScreensaver) this.Visible = false;
                }
                else if (e.Reason == SessionSwitchReason.SessionUnlock)
                {
                    isSessionLocked = false;
                    if (hideOnScreensaver && !isScreensaverRunning)
                    {
                        this.Visible = true;
                        PositionWidget();
                        this.Invalidate();
                    }
                }
            }
            catch { }
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            try
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        ApplySystemTheme();
                    });
                }
                else
                {
                    ApplySystemTheme();
                }
            }
            catch { }
        }

        public void ApplySystemTheme()
        {
            try
            {
                bool isDark = IsWindowsDarkTheme();
                lastThemeIsDark = isDark;
                currentBgColor = isDark ? BG_COLOR_DARK : BG_COLOR_LIGHT;
                this.BackColor = currentBgColor;

                if (followSystemTheme)
                {
                    if (textColorMode == "Unified")
                    {
                        textColorUnified = isDark ? Color.FromArgb(255, 255, 255) : Color.FromArgb(20, 20, 20);
                    }
                    if (unitColorMode == "Inherit")
                    {
                        unitColorUnified = isDark ? Color.FromArgb(190, 190, 190) : Color.FromArgb(85, 85, 85);
                    }
                    if (arrowColorMode == "Unified")
                    {
                        arrowColorUnified = isDark ? Color.FromArgb(235, 235, 235) : Color.FromArgb(40, 40, 40);
                    }
                }

                UpdateCachedGdiResources();
                BuildContextMenu();
                this.Invalidate();
            }
            catch { }
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
            this.BackColor = currentBgColor;
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

            // Resolução de fonte com fallback seguro
            string targetFamily = fontFamily;
            if (!IsFontInstalled(targetFamily))
            {
                targetFamily = "Segoe UI";
            }

            try
            {
                fontNum = new Font(targetFamily, fontSize, fontStyle);
            }
            catch
            {
                fontNum = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            }

            try
            {
                float unitSize = Math.Max(7.0f, fontSize - 1.5f);
                fontUnit = new Font(targetFamily, unitSize, FontStyle.Regular);
            }
            catch
            {
                fontUnit = new Font("Segoe UI", 8.0f, FontStyle.Regular);
            }

            float uniSize = Math.Max(7.5f, fontSize - 1.0f);
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

        public static bool IsFontInstalled(string familyName)
        {
            try
            {
                using (InstalledFontCollection fonts = new InstalledFontCollection())
                {
                    foreach (var f in fonts.Families)
                    {
                        if (string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private void UpdateCachedGdiResources()
        {
            if (brushNumUp != null) brushNumUp.Dispose();
            if (brushNumDown != null) brushNumDown.Dispose();
            if (brushUnitUp != null) brushUnitUp.Dispose();
            if (brushUnitDown != null) brushUnitDown.Dispose();
            if (brushArrowUp != null) brushArrowUp.Dispose();
            if (brushArrowDown != null) brushArrowDown.Dispose();
            if (penArrowUp != null) penArrowUp.Dispose();
            if (penArrowDown != null) penArrowDown.Dispose();

            Color cNumUp = (textColorMode == "Individual") ? textColorUp : textColorUnified;
            Color cNumDown = (textColorMode == "Individual") ? textColorDown : textColorUnified;

            Color cUnitUp;
            Color cUnitDown;
            if (unitColorMode == "Individual")
            {
                cUnitUp = unitColorUp;
                cUnitDown = unitColorDown;
            }
            else if (unitColorMode == "Unified")
            {
                cUnitUp = unitColorUnified;
                cUnitDown = unitColorUnified;
            }
            else // Inherit
            {
                if (IsWindowsDarkTheme())
                {
                    cUnitUp = Color.FromArgb((int)(cNumUp.R * 0.85f), (int)(cNumUp.G * 0.85f), (int)(cNumUp.B * 0.85f));
                    cUnitDown = Color.FromArgb((int)(cNumDown.R * 0.85f), (int)(cNumDown.G * 0.85f), (int)(cNumDown.B * 0.85f));
                }
                else
                {
                    cUnitUp = Color.FromArgb(Math.Min(255, (int)(cNumUp.R + 45)), Math.Min(255, (int)(cNumUp.G + 45)), Math.Min(255, (int)(cNumUp.B + 45)));
                    cUnitDown = Color.FromArgb(Math.Min(255, (int)(cNumDown.R + 45)), Math.Min(255, (int)(cNumDown.G + 45)), Math.Min(255, (int)(cNumDown.B + 45)));
                }
            }

            Color cArrUp = (arrowColorMode == "Individual") ? arrowColorUp : arrowColorUnified;
            Color cArrDown = (arrowColorMode == "Individual") ? arrowColorDown : arrowColorUnified;

            brushNumUp = new SolidBrush(cNumUp);
            brushNumDown = new SolidBrush(cNumDown);
            brushUnitUp = new SolidBrush(cUnitUp);
            brushUnitDown = new SolidBrush(cUnitDown);
            brushArrowUp = new SolidBrush(cArrUp);
            brushArrowDown = new SolidBrush(cArrDown);
            penArrowUp = new Pen(cArrUp, 1.4f);
            penArrowDown = new Pen(cArrDown, 1.4f);
        }

        private void SetupTimer()
        {
            updateTimer = new Timer();
            updateTimer.Interval = 1000;
            updateTimer.Tick += (s, e) =>
            {
                // Checagem de Screensaver
                if (hideOnScreensaver)
                {
                    bool ss = false;
                    if (SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0, ref ss, 0))
                    {
                        if (ss != isScreensaverRunning)
                        {
                            isScreensaverRunning = ss;
                            if (isScreensaverRunning)
                            {
                                if (this.Visible) this.Visible = false;
                                return;
                            }
                            else if (!isSessionLocked && !this.Visible)
                            {
                                this.Visible = true;
                                PositionWidget();
                                this.Invalidate();
                            }
                        }
                    }
                    if (isScreensaverRunning || isSessionLocked) return;
                }

                UpdateTraffic();

                bool isDarkNow = IsWindowsDarkTheme();
                if (isDarkNow != lastThemeIsDark)
                {
                    ApplySystemTheme();
                }

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
            const int WM_SETTINGCHANGE = 0x001A;
            const int WM_THEMECHANGED = 0x031A;
            if (m.Msg == WM_SETTINGCHANGE || m.Msg == WM_THEMECHANGED)
            {
                ApplySystemTheme();
            }

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

                        object valFormat = key.GetValue("SpeedUnitFormat");
                        if (valFormat != null) speedUnitFormat = valFormat.ToString();
                        else speedUnitFormat = "Bits";

                        object valUnit = key.GetValue("UnitMode");
                        if (valUnit != null) unitMode = valUnit.ToString();

                        object valArrow = key.GetValue("ArrowOnRight");
                        if (valArrow != null) arrowOnRight = Convert.ToInt32(valArrow) == 1;

                        object valLock = key.GetValue("IsLocked");
                        if (valLock != null) isLocked = Convert.ToInt32(valLock) == 1;

                        object valSs = key.GetValue("HideOnScreensaver");
                        if (valSs != null) hideOnScreensaver = Convert.ToInt32(valSs) == 1;
                        else hideOnScreensaver = true;

                        object valFollow = key.GetValue("FollowSystemTheme");
                        if (valFollow != null) followSystemTheme = Convert.ToInt32(valFollow) == 1;
                        else followSystemTheme = true;

                        object valTpl = key.GetValue("IconTemplate");
                        if (valTpl != null) iconTemplate = valTpl.ToString();

                        object valUp = key.GetValue("CustomUpChar");
                        if (valUp != null) customUpChar = valUp.ToString();

                        object valDown = key.GetValue("CustomDownChar");
                        if (valDown != null) customDownChar = valDown.ToString();

                        object valFontF = key.GetValue("FontFamily");
                        if (valFontF != null) fontFamily = valFontF.ToString();
                        else fontFamily = "Segoe UI Variable Display";

                        object valFontS = key.GetValue("FontSize");
                        if (valFontS != null) float.TryParse(valFontS.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out fontSize);
                        else fontSize = 9.5f;

                        object valFontSt = key.GetValue("FontStyle");
                        if (valFontSt != null) fontStyle = (FontStyle)Convert.ToInt32(valFontSt);
                        else fontStyle = FontStyle.Bold;

                        // Cores dos Números
                        object valTm = key.GetValue("TextColorMode");
                        if (valTm != null) textColorMode = valTm.ToString();
                        textColorUnified = HexToColor(key.GetValue("TextColorUnified") as string, Color.FromArgb(255, 255, 255));
                        textColorUp = HexToColor(key.GetValue("TextColorUp") as string, Color.FromArgb(255, 255, 255));
                        textColorDown = HexToColor(key.GetValue("TextColorDown") as string, Color.FromArgb(255, 255, 255));

                        // Cores das Unidades
                        object valUm = key.GetValue("UnitColorMode");
                        if (valUm != null) unitColorMode = valUm.ToString();
                        else unitColorMode = "Inherit";
                        unitColorUnified = HexToColor(key.GetValue("UnitColorUnified") as string, Color.FromArgb(190, 190, 190));
                        unitColorUp = HexToColor(key.GetValue("UnitColorUp") as string, Color.FromArgb(190, 190, 190));
                        unitColorDown = HexToColor(key.GetValue("UnitColorDown") as string, Color.FromArgb(190, 190, 190));

                        // Cores das Setas
                        object valAm = key.GetValue("ArrowColorMode");
                        if (valAm != null) arrowColorMode = valAm.ToString();
                        arrowColorUnified = HexToColor(key.GetValue("ArrowColorUnified") as string, Color.FromArgb(235, 235, 235));
                        arrowColorUp = HexToColor(key.GetValue("ArrowColorUp") as string, Color.FromArgb(235, 235, 235));
                        arrowColorDown = HexToColor(key.GetValue("ArrowColorDown") as string, Color.FromArgb(235, 235, 235));
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
                        key.SetValue("SpeedUnitFormat", speedUnitFormat);
                        key.SetValue("UnitMode", unitMode);
                        key.SetValue("ArrowOnRight", arrowOnRight ? 1 : 0);
                        key.SetValue("IsLocked", isLocked ? 1 : 0);
                        key.SetValue("HideOnScreensaver", hideOnScreensaver ? 1 : 0);
                        key.SetValue("FollowSystemTheme", followSystemTheme ? 1 : 0);
                        key.SetValue("CustomX", this.Location.X);
                        key.SetValue("CustomY", this.Location.Y);

                        key.SetValue("FontFamily", fontFamily);
                        key.SetValue("FontSize", fontSize.ToString(CultureInfo.InvariantCulture));
                        key.SetValue("FontStyle", (int)fontStyle);

                        key.SetValue("TextColorMode", textColorMode);
                        key.SetValue("TextColorUnified", ColorToHex(textColorUnified));
                        key.SetValue("TextColorUp", ColorToHex(textColorUp));
                        key.SetValue("TextColorDown", ColorToHex(textColorDown));

                        key.SetValue("UnitColorMode", unitColorMode);
                        key.SetValue("UnitColorUnified", ColorToHex(unitColorUnified));
                        key.SetValue("UnitColorUp", ColorToHex(unitColorUp));
                        key.SetValue("UnitColorDown", ColorToHex(unitColorDown));

                        key.SetValue("ArrowColorMode", arrowColorMode);
                        key.SetValue("ArrowColorUnified", ColorToHex(arrowColorUnified));
                        key.SetValue("ArrowColorUp", ColorToHex(arrowColorUp));
                        key.SetValue("ArrowColorDown", ColorToHex(arrowColorDown));

                        key.SetValue("IconTemplate", iconTemplate);
                        key.SetValue("CustomUpChar", customUpChar);
                        key.SetValue("CustomDownChar", customDownChar);
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

            // Proteção estrita multi-monitor
            Screen targetScreen = Screen.FromPoint(new Point(posX, posY));
            if (targetScreen == null) targetScreen = primary;

            posX = Math.Max(targetScreen.WorkingArea.Left, Math.Min(posX, targetScreen.WorkingArea.Right - WIDGET_WIDTH));
            posY = Math.Max(targetScreen.WorkingArea.Top, Math.Min(posY, targetScreen.Bounds.Bottom - WIDGET_HEIGHT));

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
            // Otimização: Reutiliza o nic em cache enquanto ativo
            if (cachedNic == null || cachedNic.OperationalStatus != OperationalStatus.Up)
            {
                cachedNic = GetTargetNic();
            }

            if (cachedNic == null)
            {
                string fallbackUnit = (speedUnitFormat == "Bytes") ? "KB/s" : "Kbps";
                if (strUpNum != "0.0" || strUpUnit != fallbackUnit || strDownNum != "0.0")
                {
                    strUpNum = "0.0";
                    strUpUnit = (unitMode == "Auto") ? fallbackUnit : unitMode;
                    strDownNum = "0.0";
                    strDownUnit = strUpUnit;
                    this.Invalidate();
                }
                return;
            }

            try
            {
                var stats = cachedNic.GetIPv4Statistics();
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

                        string newUpNum, newUpUnit, newDownNum, newDownUnit;
                        FormatSpeed(bitsOutPerSec, out newUpNum, out newUpUnit);
                        FormatSpeed(bitsInPerSec, out newDownNum, out newDownUnit);

                        // Otimização zero CPU: Só invalida a tela se o valor realmente mudou
                        if (newUpNum != strUpNum || newUpUnit != strUpUnit ||
                            newDownNum != strDownNum || newDownUnit != strDownUnit)
                        {
                            strUpNum = newUpNum;
                            strUpUnit = newUpUnit;
                            strDownNum = newDownNum;
                            strDownUnit = newDownUnit;
                            this.Invalidate();
                        }
                    }
                }

                lastBytesIn = currentIn;
                lastBytesOut = currentOut;
                lastMeasurementTime = now;
            }
            catch
            {
                cachedNic = null;
            }
        }

        public void FormatSpeed(double bitsPerSec, out string num, out string unit)
        {
            if (speedUnitFormat == "Bytes")
            {
                double bytesPerSec = bitsPerSec / 8.0;

                if (unitMode == "KB/s" || unitMode == "Kbps")
                {
                    double kb = bytesPerSec / 1024.0;
                    num = (kb >= 100) ? kb.ToString("F0") : kb.ToString("F1");
                    unit = "KB/s";
                }
                else if (unitMode == "MB/s" || unitMode == "Mbps")
                {
                    double mb = bytesPerSec / (1024.0 * 1024.0);
                    num = (mb >= 100) ? mb.ToString("F0") : mb.ToString("F1");
                    unit = "MB/s";
                }
                else if (unitMode == "GB/s" || unitMode == "Gbps")
                {
                    double gb = bytesPerSec / (1024.0 * 1024.0 * 1024.0);
                    num = (gb >= 10) ? gb.ToString("F1") : gb.ToString("F2");
                    unit = "GB/s";
                }
                else // Auto
                {
                    if (bytesPerSec >= (1024.0 * 1024.0 * 1024.0))
                    {
                        double gb = bytesPerSec / (1024.0 * 1024.0 * 1024.0);
                        num = (gb >= 10) ? gb.ToString("F1") : gb.ToString("F2");
                        unit = "GB/s";
                    }
                    else if (bytesPerSec >= (1024.0 * 1024.0))
                    {
                        double mb = bytesPerSec / (1024.0 * 1024.0);
                        num = (mb >= 100) ? mb.ToString("F0") : mb.ToString("F1");
                        unit = "MB/s";
                    }
                    else if (bytesPerSec >= 1024.0)
                    {
                        double kb = bytesPerSec / 1024.0;
                        num = (kb >= 100) ? kb.ToString("F0") : kb.ToString("F1");
                        unit = "KB/s";
                    }
                    else
                    {
                        num = bytesPerSec.ToString("F0");
                        unit = "B/s";
                    }
                }
            }
            else // Bits (Padrão de fábrica)
            {
                if (unitMode == "Kbps" || unitMode == "KB/s")
                {
                    double kbps = bitsPerSec / 1000.0;
                    num = (kbps >= 100) ? kbps.ToString("F0") : kbps.ToString("F1");
                    unit = "Kbps";
                }
                else if (unitMode == "Mbps" || unitMode == "MB/s")
                {
                    double mbps = bitsPerSec / 1000000.0;
                    num = (mbps >= 100) ? mbps.ToString("F0") : mbps.ToString("F1");
                    unit = "Mbps";
                }
                else if (unitMode == "Gbps" || unitMode == "GB/s")
                {
                    double gbps = bitsPerSec / 1000000000.0;
                    num = (gbps >= 10) ? gbps.ToString("F1") : gbps.ToString("F2");
                    unit = "Gbps";
                }
                else // Auto
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
                    else if (bitsPerSec >= 1000.0)
                    {
                        double kbps = bitsPerSec / 1000.0;
                        num = (kbps >= 100) ? kbps.ToString("F0") : kbps.ToString("F1");
                        unit = "Kbps";
                    }
                    else
                    {
                        num = bitsPerSec.ToString("F0");
                        unit = "bps";
                    }
                }
            }
        }

        public static string ParseUnicodeString(string input, string fallback)
        {
            if (string.IsNullOrEmpty(input)) return fallback;
            input = input.Trim();

            if (input.StartsWith("\\u", StringComparison.OrdinalIgnoreCase) && input.Length == 6)
            {
                try
                {
                    int code = int.Parse(input.Substring(2), NumberStyles.HexNumber);
                    return char.ConvertFromUtf32(code);
                }
                catch { }
            }
            if (input.StartsWith("U+", StringComparison.OrdinalIgnoreCase) && input.Length >= 6)
            {
                try
                {
                    int code = int.Parse(input.Substring(2), NumberStyles.HexNumber);
                    return char.ConvertFromUtf32(code);
                }
                catch { }
            }
            return input;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            g.Clear(currentBgColor);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (arrowOnRight)
            {
                // ==============================================================
                // LAYOUT: SETAS À DIREITA -> [ 823.6 Kbps  ▲ ] (Padrão de Fábrica)
                // ==============================================================
                RectangleF rUpNum = new RectangleF(2, 1, 46, 17);
                RectangleF rDownNum = new RectangleF(2, 19, 46, 17);

                g.DrawString(strUpNum, fontNum, brushNumUp, rUpNum, sfRight);
                g.DrawString(strDownNum, fontNum, brushNumDown, rDownNum, sfRight);

                RectangleF rUpUnit = new RectangleF(50, 1, 38, 17);
                RectangleF rDownUnit = new RectangleF(50, 19, 38, 17);

                g.DrawString(strUpUnit, fontUnit, brushUnitUp, rUpUnit, sfLeft);
                g.DrawString(strDownUnit, fontUnit, brushUnitDown, rDownUnit, sfLeft);

                DrawArrowIcon(g, 97.0f);
            }
            else
            {
                // ==============================================================
                // LAYOUT: SETAS À ESQUERDA -> [ ▲  823.6 Kbps ]
                // ==============================================================
                DrawArrowIcon(g, 10.0f);

                RectangleF rUpNum = new RectangleF(18, 1, 42, 17);
                RectangleF rDownNum = new RectangleF(18, 19, 42, 17);

                g.DrawString(strUpNum, fontNum, brushNumUp, rUpNum, sfRight);
                g.DrawString(strDownNum, fontNum, brushNumDown, rDownNum, sfRight);

                RectangleF rUpUnit = new RectangleF(62, 1, 40, 17);
                RectangleF rDownUnit = new RectangleF(62, 19, 40, 17);

                g.DrawString(strUpUnit, fontUnit, brushUnitUp, rUpUnit, sfLeft);
                g.DrawString(strDownUnit, fontUnit, brushUnitDown, rDownUnit, sfLeft);
            }
        }

        private void DrawArrowIcon(Graphics g, float centerX)
        {
            if (iconTemplate == "VectorTriangle")
            {
                PointF[] upArrow = new PointF[] {
                    new PointF(centerX, 5.0f),
                    new PointF(centerX - 4.0f, 10.2f),
                    new PointF(centerX + 4.0f, 10.2f)
                };
                g.FillPolygon(brushArrowUp, upArrow);

                PointF[] downArrow = new PointF[] {
                    new PointF(centerX, 31.5f),
                    new PointF(centerX - 4.0f, 26.3f),
                    new PointF(centerX + 4.0f, 26.3f)
                };
                g.FillPolygon(brushArrowDown, downArrow);
            }
            else if (iconTemplate == "VectorChevron")
            {
                g.DrawLines(penArrowUp, new PointF[] {
                    new PointF(centerX - 3.8f, 9.5f),
                    new PointF(centerX, 5.5f),
                    new PointF(centerX + 3.8f, 9.5f)
                });
                g.DrawLines(penArrowDown, new PointF[] {
                    new PointF(centerX - 3.8f, 27.0f),
                    new PointF(centerX, 31.0f),
                    new PointF(centerX + 3.8f, 27.0f)
                });
            }
            else if (iconTemplate == "VectorDualChevron")
            {
                // Chevrons Duplos Velocidade (▲▲ / ▼▼)
                g.DrawLines(penArrowUp, new PointF[] {
                    new PointF(centerX - 3.5f, 6.8f),
                    new PointF(centerX, 3.8f),
                    new PointF(centerX + 3.5f, 6.8f)
                });
                g.DrawLines(penArrowUp, new PointF[] {
                    new PointF(centerX - 3.5f, 10.5f),
                    new PointF(centerX, 7.5f),
                    new PointF(centerX + 3.5f, 10.5f)
                });

                g.DrawLines(penArrowDown, new PointF[] {
                    new PointF(centerX - 3.5f, 26.0f),
                    new PointF(centerX, 29.0f),
                    new PointF(centerX + 3.5f, 26.0f)
                });
                g.DrawLines(penArrowDown, new PointF[] {
                    new PointF(centerX - 3.5f, 29.5f),
                    new PointF(centerX, 32.5f),
                    new PointF(centerX + 3.5f, 29.5f)
                });
            }
            else if (iconTemplate == "StreamTech")
            {
                // Stream Tech Diagonal 45° (↗ / ↘)
                g.DrawLine(penArrowUp, centerX - 3.5f, 10.5f, centerX + 3.0f, 4.5f);
                g.DrawLines(penArrowUp, new PointF[] {
                    new PointF(centerX - 1.0f, 4.5f),
                    new PointF(centerX + 3.0f, 4.5f),
                    new PointF(centerX + 3.0f, 8.5f)
                });

                g.DrawLine(penArrowDown, centerX - 3.5f, 26.5f, centerX + 3.0f, 32.5f);
                g.DrawLines(penArrowDown, new PointF[] {
                    new PointF(centerX + 3.0f, 28.5f),
                    new PointF(centerX + 3.0f, 32.5f),
                    new PointF(centerX - 1.0f, 32.5f)
                });
            }
            else
            {
                string upCh = ParseUnicodeString(customUpChar, "▲");
                string downCh = ParseUnicodeString(customDownChar, "▼");

                RectangleF rUp = new RectangleF(centerX - 8, 1, 16, 17);
                RectangleF rDown = new RectangleF(centerX - 8, 19, 16, 17);

                g.DrawString(upCh, fontUnicode, brushArrowUp, rUp, sfCenter);
                g.DrawString(downCh, fontUnicode, brushArrowDown, rDown, sfCenter);
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

            // 2. Formato e Unidades
            ToolStripMenuItem mnuUnits = new ToolStripMenuItem(isPt ? "Unidade de Medição" : "Speed Unit");
            ConfigureSubmenu(mnuUnits);
            PopulateUnitsMenu(mnuUnits);
            contextMenu.Items.Add(mnuUnits);

            // 3. Posição das Setas / Layout
            ToolStripMenuItem mnuLayout = new ToolStripMenuItem(isPt ? "Posição das Setas" : "Arrow Position");
            ConfigureSubmenu(mnuLayout);

            ToolStripMenuItem itmRight = new ToolStripMenuItem(isPt ? "Setas à Direita (823 Kbps ▲)" : "Arrows on Right (823 Kbps ▲)");
            itmRight.Checked = arrowOnRight;
            itmRight.Click += (s, e) => { arrowOnRight = true; SaveSettings(); BuildContextMenu(); this.Invalidate(); };

            ToolStripMenuItem itmLeft = new ToolStripMenuItem(isPt ? "Setas à Esquerda (▲ 823 Kbps)" : "Arrows on Left (▲ 823 Kbps)");
            itmLeft.Checked = !arrowOnRight;
            itmLeft.Click += (s, e) => { arrowOnRight = false; SaveSettings(); BuildContextMenu(); this.Invalidate(); };

            mnuLayout.DropDownItems.Add(itmRight);
            mnuLayout.DropDownItems.Add(itmLeft);
            contextMenu.Items.Add(mnuLayout);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 4. Travar Posição
            ToolStripMenuItem mnuLock = new ToolStripMenuItem(isPt ? "Travar Posição" : "Lock Position");
            mnuLock.Checked = isLocked;
            mnuLock.Click += (s, e) =>
            {
                isLocked = !isLocked;
                mnuLock.Checked = isLocked;
                SaveSettings();
            };
            contextMenu.Items.Add(mnuLock);

            // 5. Iniciar com o Windows
            ToolStripMenuItem mnuAutoStart = new ToolStripMenuItem(isPt ? "Iniciar com o Windows" : "Start with Windows");
            mnuAutoStart.Checked = IsAutoStartEnabled();
            mnuAutoStart.Click += (s, e) =>
            {
                bool newState = !mnuAutoStart.Checked;
                ToggleAutoStart(newState);
                mnuAutoStart.Checked = newState;
            };
            contextMenu.Items.Add(mnuAutoStart);

            // 6. Redefinir Posição
            ToolStripMenuItem mnuReset = new ToolStripMenuItem(isPt ? "Redefinir Posição" : "Reset Position");
            mnuReset.Click += (s, e) => PositionWidget(true);
            contextMenu.Items.Add(mnuReset);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 7. Sair / Exit
            ToolStripMenuItem mnuExit = new ToolStripMenuItem(isPt ? "Sair" : "Exit");
            mnuExit.Click += (s, e) => Application.Exit();
            contextMenu.Items.Add(mnuExit);
        }

        private void ConfigureSubmenu(ToolStripMenuItem parent)
        {
            parent.DropDown.DropShadowEnabled = true;
            parent.DropDownOpening += (s, e) =>
            {
                Screen scr = Screen.FromControl(this);
                if (contextMenu.Right + parent.DropDown.Width > scr.WorkingArea.Right)
                {
                    parent.DropDownDirection = ToolStripDropDownDirection.Left;
                }
                else
                {
                    parent.DropDownDirection = ToolStripDropDownDirection.Right;
                }
            };
        }

        private void PopulateAdaptersMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();
            bool isPt = currentLanguage == "pt";

            ToolStripMenuItem itmAuto = new ToolStripMenuItem(isPt ? "Automático (Maior Tráfego)" : "Auto (Highest Traffic)");
            itmAuto.Checked = string.Equals(selectedNicId, "Auto", StringComparison.OrdinalIgnoreCase);
            itmAuto.Click += (s, e) =>
            {
                selectedNicId = "Auto";
                cachedNic = null;
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
                    cachedNic = null;
                    lastBytesIn = -1;
                    lastBytesOut = -1;
                    SaveSettings();
                    PopulateAdaptersMenu(parent);
                    UpdateTraffic();
                };
                parent.DropDownItems.Add(itm);
            }
        }

        private void PopulateUnitsMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();
            bool isPt = currentLanguage == "pt";

            // Seletor Bits vs Bytes
            ToolStripMenuItem itmBits = new ToolStripMenuItem(isPt ? "Formato: Bits (Mbps, Kbps) [Padrão]" : "Format: Bits (Mbps, Kbps) [Default]");
            itmBits.Checked = (speedUnitFormat == "Bits");
            itmBits.Click += (s, e) =>
            {
                speedUnitFormat = "Bits";
                SaveSettings();
                PopulateUnitsMenu(parent);
                this.Invalidate();
            };
            parent.DropDownItems.Add(itmBits);

            ToolStripMenuItem itmBytes = new ToolStripMenuItem(isPt ? "Formato: Bytes (MB/s, KB/s)" : "Format: Bytes (MB/s, KB/s)");
            itmBytes.Checked = (speedUnitFormat == "Bytes");
            itmBytes.Click += (s, e) =>
            {
                speedUnitFormat = "Bytes";
                SaveSettings();
                PopulateUnitsMenu(parent);
                this.Invalidate();
            };
            parent.DropDownItems.Add(itmBytes);

            parent.DropDownItems.Add(new ToolStripSeparator());

            string[] modes = (speedUnitFormat == "Bytes") ?
                new string[] { "Auto", "KB/s", "MB/s", "GB/s" } :
                new string[] { "Auto", "Kbps", "Mbps", "Gbps" };

            foreach (var mode in modes)
            {
                string label = mode == "Auto" ? (isPt ? "Escala Automática" : "Auto Scale") : mode;
                ToolStripMenuItem itm = new ToolStripMenuItem(label);
                itm.Checked = (unitMode == mode);
                itm.Click += (s, e) =>
                {
                    unitMode = mode;
                    SaveSettings();
                    PopulateUnitsMenu(parent);
                    this.Invalidate();
                };
                parent.DropDownItems.Add(itm);
            }
        }

        private void OpenSettingsDialog()
        {
            updateTimer.Stop();
            try
            {
                using (SettingsForm sf = new SettingsForm(
                    this,
                    currentLanguage,
                    speedUnitFormat,
                    unitMode,
                    arrowOnRight,
                    isLocked,
                    hideOnScreensaver,
                    followSystemTheme,
                    iconTemplate,
                    customUpChar,
                    customDownChar,
                    fontFamily,
                    fontSize,
                    fontStyle,
                    textColorMode,
                    textColorUnified,
                    textColorUp,
                    textColorDown,
                    unitColorMode,
                    unitColorUnified,
                    unitColorUp,
                    unitColorDown,
                    arrowColorMode,
                    arrowColorUnified,
                    arrowColorUp,
                    arrowColorDown))
                {
                    if (sf.ShowDialog() == DialogResult.OK)
                    {
                        currentLanguage = sf.SelectedLanguage;
                        speedUnitFormat = sf.SelectedSpeedUnitFormat;
                        unitMode = sf.SelectedUnitMode;
                        arrowOnRight = sf.SelectedArrowOnRight;
                        isLocked = sf.SelectedIsLocked;
                        hideOnScreensaver = sf.SelectedHideOnScreensaver;
                        followSystemTheme = sf.SelectedFollowSystemTheme;
                        iconTemplate = sf.SelectedIconTemplate;
                        customUpChar = sf.SelectedCustomUpChar;
                        customDownChar = sf.SelectedCustomDownChar;

                        fontFamily = sf.SelectedFontFamily;
                        fontSize = sf.SelectedFontSize;
                        fontStyle = sf.SelectedFontStyle;

                        textColorMode = sf.SelectedTextColorMode;
                        textColorUnified = sf.SelectedTextColorUnified;
                        textColorUp = sf.SelectedTextColorUp;
                        textColorDown = sf.SelectedTextColorDown;

                        unitColorMode = sf.SelectedUnitColorMode;
                        unitColorUnified = sf.SelectedUnitColorUnified;
                        unitColorUp = sf.SelectedUnitColorUp;
                        unitColorDown = sf.SelectedUnitColorDown;

                        arrowColorMode = sf.SelectedArrowColorMode;
                        arrowColorUnified = sf.SelectedArrowColorUnified;
                        arrowColorUp = sf.SelectedArrowColorUp;
                        arrowColorDown = sf.SelectedArrowColorDown;

                        SaveSettings();
                        SetupFonts();
                        UpdateCachedGdiResources();
                        BuildContextMenu();
                        this.Invalidate();
                    }
                }
            }
            finally
            {
                updateTimer.Start();
                try { GC.Collect(2, GCCollectionMode.Optimized); } catch { }
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
                    SaveSettings();
                }
            }
        }

        private void NetMonitorApp_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Proteção contra transbordamento para o 2º monitor
                Screen scr = Screen.FromControl(this);
                Rectangle wa = scr.WorkingArea;
                Point pt = Cursor.Position;

                if (pt.X + 220 > wa.Right)
                {
                    pt.X = wa.Right - 220;
                }
                if (pt.Y + 320 > wa.Bottom)
                {
                    pt.Y = wa.Bottom - 320;
                }

                contextMenu.Show(pt);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                if (updateTimer != null) { updateTimer.Stop(); updateTimer.Dispose(); }
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }

                if (brushNumUp != null) brushNumUp.Dispose();
                if (brushNumDown != null) brushNumDown.Dispose();
                if (brushUnitUp != null) brushUnitUp.Dispose();
                if (brushUnitDown != null) brushUnitDown.Dispose();
                if (brushArrowUp != null) brushArrowUp.Dispose();
                if (brushArrowDown != null) brushArrowDown.Dispose();
                if (penArrowUp != null) penArrowUp.Dispose();
                if (penArrowDown != null) penArrowDown.Dispose();

                if (fontNum != null) fontNum.Dispose();
                if (fontUnit != null) fontUnit.Dispose();
                if (fontUnicode != null) fontUnicode.Dispose();

                if (sfRight != null) sfRight.Dispose();
                if (sfLeft != null) sfLeft.Dispose();
                if (sfCenter != null) sfCenter.Dispose();
            }
            catch { }
            base.OnFormClosing(e);
        }

        [STAThread]
        public static void Main()
        {
            try
            {
                SetProcessDpiAwareness(2);
            }
            catch
            {
                try { SetProcessDPIAware(); } catch { }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new NetMonitorApp());
        }
    }

    // ==============================================================================
    // RENDERIZADOR NATIVO DO WINDOWS 11 PARA MENUS DE CONTEXTO
    // ==============================================================================
    public class Windows11MenuRenderer : ToolStripProfessionalRenderer
    {
        private bool isDark;

        public Windows11MenuRenderer(bool isDark) : base(new Windows11ColorTable(isDark))
        {
            this.isDark = isDark;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled)
            {
                Rectangle r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
                Color hoverBg = isDark ? Color.FromArgb(52, 52, 55) : Color.FromArgb(232, 232, 236);
                using (SolidBrush b = new SolidBrush(hoverBg))
                using (GraphicsPath path = CreateRoundedRectanglePath(r, 4))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(b, path);
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
                e.TextColor = e.Item.Enabled ? Color.FromArgb(245, 245, 245) : Color.FromArgb(120, 120, 120);
            }
            else
            {
                e.TextColor = e.Item.Enabled ? Color.FromArgb(25, 25, 25) : Color.FromArgb(140, 140, 140);
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

        public static GraphicsPath CreateRoundedRectanglePath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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

    // ==============================================================================
    // PROPOSTA B: JANELA DE CONFIGURAÇÕES COM SIDEBAR LATERAL DO WINDOWS 11 (v1.0.0)
    // ==============================================================================
    public class SettingsForm : Form
    {
        // Propriedades Selecionadas
        public string SelectedLanguage { get; private set; }
        public string SelectedSpeedUnitFormat { get; private set; }
        public string SelectedUnitMode { get; private set; }
        public bool SelectedArrowOnRight { get; private set; }
        public bool SelectedIsLocked { get; private set; }
        public bool SelectedHideOnScreensaver { get; private set; }
        public bool SelectedFollowSystemTheme { get; private set; }
        public string SelectedIconTemplate { get; private set; }
        public string SelectedCustomUpChar { get; private set; }
        public string SelectedCustomDownChar { get; private set; }

        public string SelectedFontFamily { get; private set; }
        public float SelectedFontSize { get; private set; }
        public FontStyle SelectedFontStyle { get; private set; }

        public string SelectedTextColorMode { get; private set; }
        public Color SelectedTextColorUnified { get; private set; }
        public Color SelectedTextColorUp { get; private set; }
        public Color SelectedTextColorDown { get; private set; }

        public string SelectedUnitColorMode { get; private set; }
        public Color SelectedUnitColorUnified { get; private set; }
        public Color SelectedUnitColorUp { get; private set; }
        public Color SelectedUnitColorDown { get; private set; }

        public string SelectedArrowColorMode { get; private set; }
        public Color SelectedArrowColorUnified { get; private set; }
        public Color SelectedArrowColorUp { get; private set; }
        public Color SelectedArrowColorDown { get; private set; }

        // Elementos da Interface
        private Form parentWidget;
        private bool isDarkTheme;

        // Painéis e Navegação Lateral (Sidebar)
        private Panel pnlSidebar;
        private Panel pnlMainContent;
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;

        private Button btnNavGeral;
        private Button btnNavFonte;
        private Button btnNavCores;
        private Button btnNavIcones;

        private Panel pnlPageGeral;
        private Panel pnlPageFonte;
        private Panel pnlPageCores;
        private Panel pnlPageIcones;

        // Controles: Geral
        private ComboBox cbLang;
        private ComboBox cbFormat;
        private ComboBox cbUnits;
        private RadioButton rbRight;
        private RadioButton rbLeft;
        private CheckBox chkLock;
        private CheckBox chkScreensaver;

        // Controles: Tipografia
        private ComboBox cbFontFamily;
        private ComboBox cbFontSize;
        private ComboBox cbFontStyle;

        // Controles: Cores
        private RadioButton rbTextUnified;
        private RadioButton rbTextIndividual;
        private Button btnColorTextBoth;
        private Button btnColorTextUp;
        private Button btnColorTextDown;

        private RadioButton rbUnitInherit;
        private RadioButton rbUnitUnified;
        private RadioButton rbUnitIndividual;
        private Button btnColorUnitBoth;
        private Button btnColorUnitUp;
        private Button btnColorUnitDown;

        private RadioButton rbArrowUnified;
        private RadioButton rbArrowIndividual;
        private Button btnColorArrowBoth;
        private Button btnColorArrowUp;
        private Button btnColorArrowDown;

        // Controles: Ícones
        private ComboBox cbTemplate;
        private TextBox txtUp;
        private TextBox txtDown;
        private Label lblUp;
        private Label lblDown;

        // Live Preview Box
        private Panel pnlLivePreview;

        // Cores Dinâmicas de UI
        private Color clrFormBg;
        private Color clrSidebarBg;
        private Color clrCardBg;
        private Color clrBorder;
        private Color clrTextMain;
        private Color clrTextSub;
        private Color clrInputBg;
        private Color clrAccent = Color.FromArgb(0, 120, 212);

        public SettingsForm(
            Form parent,
            string lang,
            string speedFormat,
            string unit,
            bool arrowRight,
            bool locked,
            bool hideScreensaver,
            bool followSysTheme,
            string tpl,
            string upChar,
            string downChar,
            string fontFam,
            float fontSz,
            FontStyle fontSt,
            string txtColMode,
            Color txtColUni,
            Color txtColUp,
            Color txtColDn,
            string unitColMode,
            Color unitColUni,
            Color unitColUp,
            Color unitColDn,
            string arrColMode,
            Color arrColUni,
            Color arrColUp,
            Color arrColDn)
        {
            this.parentWidget = parent;
            this.SelectedLanguage = lang;
            this.SelectedSpeedUnitFormat = speedFormat;
            this.SelectedUnitMode = unit;
            this.SelectedArrowOnRight = arrowRight;
            this.SelectedIsLocked = locked;
            this.SelectedHideOnScreensaver = hideScreensaver;
            this.SelectedFollowSystemTheme = followSysTheme;
            this.SelectedIconTemplate = tpl;
            this.SelectedCustomUpChar = upChar;
            this.SelectedCustomDownChar = downChar;

            this.SelectedFontFamily = fontFam;
            this.SelectedFontSize = fontSz;
            this.SelectedFontStyle = fontSt;

            this.SelectedTextColorMode = txtColMode;
            this.SelectedTextColorUnified = txtColUni;
            this.SelectedTextColorUp = txtColUp;
            this.SelectedTextColorDown = txtColDn;

            this.SelectedUnitColorMode = unitColMode;
            this.SelectedUnitColorUnified = unitColUni;
            this.SelectedUnitColorUp = unitColUp;
            this.SelectedUnitColorDown = unitColDn;

            this.SelectedArrowColorMode = arrColMode;
            this.SelectedArrowColorUnified = arrColUni;
            this.SelectedArrowColorUp = arrColUp;
            this.SelectedArrowColorDown = arrColDn;

            this.isDarkTheme = NetMonitorApp.IsWindowsDarkTheme();
            SetupThemeColors();
            InitializeUI();
        }

        private void SetupThemeColors()
        {
            if (isDarkTheme)
            {
                clrFormBg = Color.FromArgb(32, 32, 35);
                clrSidebarBg = Color.FromArgb(24, 24, 26);
                clrCardBg = Color.FromArgb(42, 42, 46);
                clrBorder = Color.FromArgb(60, 60, 65);
                clrTextMain = Color.FromArgb(250, 250, 250);
                clrTextSub = Color.FromArgb(180, 180, 185);
                clrInputBg = Color.FromArgb(28, 28, 30);
            }
            else
            {
                clrFormBg = Color.FromArgb(243, 243, 246);
                clrSidebarBg = Color.FromArgb(232, 232, 236);
                clrCardBg = Color.FromArgb(255, 255, 255);
                clrBorder = Color.FromArgb(215, 215, 220);
                clrTextMain = Color.FromArgb(20, 20, 25);
                clrTextSub = Color.FromArgb(90, 90, 95);
                clrInputBg = Color.FromArgb(250, 250, 250);
            }
        }

        private void InitializeUI()
        {
            bool isPt = SelectedLanguage == "pt";

            this.Text = isPt ? "Personalização do NetMonitor v1.0.0" : "NetMonitor v1.0.0 Settings";
            this.Size = new Size(650, 520);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = clrFormBg;
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // Posiciona centralizado e contido no monitor correto (sem transbordar)
            Screen scr = (parentWidget != null) ? Screen.FromControl(parentWidget) : Screen.PrimaryScreen;
            Rectangle wa = scr.WorkingArea;
            int posX = Math.Max(wa.Left, Math.Min(wa.Right - this.Width, wa.Left + (wa.Width - this.Width) / 2));
            int posY = Math.Max(wa.Top, Math.Min(wa.Bottom - this.Height, wa.Top + (wa.Height - this.Height) / 2));
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(posX, posY);

            // ==============================================================
            // 1. SIDEBAR LATERAL (ESQUERDA)
            // ==============================================================
            pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 165,
                BackColor = clrSidebarBg,
                Padding = new Padding(10, 12, 10, 10)
            };
            this.Controls.Add(pnlSidebar);

            Label lblAppLogo = new Label
            {
                Text = "NetMonitor",
                Font = new Font("Segoe UI", 11.0f, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(14, 14)
            };
            pnlSidebar.Controls.Add(lblAppLogo);

            Label lblAppVer = new Label
            {
                Text = "v1.0.0 • Settings",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = clrAccent,
                AutoSize = true,
                Location = new Point(14, 34)
            };
            pnlSidebar.Controls.Add(lblAppVer);

            int btnY = 70;
            btnNavGeral = CreateSidebarButton(isPt ? "⚙️  Geral" : "⚙️  General", btnY, () => SwitchPage(0));
            btnNavFonte = CreateSidebarButton(isPt ? "🔤  Tipografia" : "🔤  Typography", btnY + 40, () => SwitchPage(1));
            btnNavCores = CreateSidebarButton(isPt ? "🎨  Cores" : "🎨  Colors", btnY + 80, () => SwitchPage(2));
            btnNavIcones = CreateSidebarButton(isPt ? "🔼  Ícones & Setas" : "🔼  Icons & Arrows", btnY + 120, () => SwitchPage(3));

            pnlSidebar.Controls.Add(btnNavGeral);
            pnlSidebar.Controls.Add(btnNavFonte);
            pnlSidebar.Controls.Add(btnNavCores);
            pnlSidebar.Controls.Add(btnNavIcones);

            // ==============================================================
            // 2. PAINEL PRINCIPAL (DIREITA)
            // ==============================================================
            pnlMainContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = clrFormBg,
                Padding = new Padding(18, 14, 18, 14)
            };
            this.Controls.Add(pnlMainContent);

            // Cabeçalho dinâmico do painel
            lblHeaderTitle = new Label
            {
                Text = isPt ? "Preferências Gerais" : "General Preferences",
                Font = new Font("Segoe UI", 12.0f, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(18, 14)
            };
            pnlMainContent.Controls.Add(lblHeaderTitle);

            lblHeaderSubtitle = new Label
            {
                Text = isPt ? "Configure idioma, medição, layout e proteção de exibição." : "Configure language, measurement, layout, and display protection.",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = clrTextSub,
                AutoSize = true,
                Location = new Point(19, 36)
            };
            pnlMainContent.Controls.Add(lblHeaderSubtitle);

            // Container de páginas
            int pageY = 62;
            int pageW = pnlMainContent.Width - 36;
            int pageH = 315;

            pnlPageGeral = new Panel { Location = new Point(18, pageY), Size = new Size(445, pageH), BackColor = Color.Transparent };
            pnlPageFonte = new Panel { Location = new Point(18, pageY), Size = new Size(445, pageH), BackColor = Color.Transparent, Visible = false };
            pnlPageCores = new Panel { Location = new Point(18, pageY), Size = new Size(445, pageH), BackColor = Color.Transparent, Visible = false };
            pnlPageIcones = new Panel { Location = new Point(18, pageY), Size = new Size(445, pageH), BackColor = Color.Transparent, Visible = false };

            pnlMainContent.Controls.Add(pnlPageGeral);
            pnlMainContent.Controls.Add(pnlPageFonte);
            pnlMainContent.Controls.Add(pnlPageCores);
            pnlMainContent.Controls.Add(pnlPageIcones);

            // Constrói o conteúdo de cada página
            BuildPageGeral(pnlPageGeral, isPt);
            BuildPageFonte(pnlPageFonte, isPt);
            BuildPageCores(pnlPageCores, isPt);
            BuildPageIcones(pnlPageIcones, isPt);

            // ==============================================================
            // 3. BARRA INFERIOR COM LIVE PREVIEW E AÇÕES
            // ==============================================================
            int bottomY = this.ClientSize.Height - 88;
            Panel pnlBottom = new Panel
            {
                Location = new Point(165, bottomY),
                Size = new Size(this.ClientSize.Width - 165, 88),
                BackColor = clrFormBg
            };
            this.Controls.Add(pnlBottom);
            pnlBottom.BringToFront();

            // Linha divisória
            Panel pnlDiv = new Panel { Location = new Point(18, 0), Size = new Size(pnlBottom.Width - 36, 1), BackColor = clrBorder };
            pnlBottom.Controls.Add(pnlDiv);

            // Live Preview Box
            pnlLivePreview = new Panel
            {
                Location = new Point(18, 12),
                Size = new Size(110, 42),
                BackColor = Color.FromArgb(28, 28, 28)
            };
            pnlLivePreview.Paint += PnlLivePreview_Paint;
            pnlBottom.Controls.Add(pnlLivePreview);

            Label lblPrevTitle = new Label
            {
                Text = isPt ? "PRÉ-VISUALIZAÇÃO AO VIVO" : "LIVE PREVIEW",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = clrAccent,
                Location = new Point(136, 14),
                AutoSize = true
            };
            pnlBottom.Controls.Add(lblPrevTitle);

            Label lblPrevDesc = new Label
            {
                Text = isPt ? "Atualização instantânea com as fontes, cores e setas." : "Instant update with selected fonts, colors, and arrows.",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(136, 30),
                AutoSize = true
            };
            pnlBottom.Controls.Add(lblPrevDesc);

            // Botão Salvar
            Button btnSave = new Button
            {
                Text = isPt ? "Salvar" : "Save",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                Location = new Point(pnlBottom.Width - 180, 14),
                Size = new Size(75, 34),
                BackColor = clrAccent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            pnlBottom.Controls.Add(btnSave);

            // Botão Cancelar
            Button btnCancel = new Button
            {
                Text = isPt ? "Cancelar" : "Cancel",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                Location = new Point(pnlBottom.Width - 95, 14),
                Size = new Size(75, 34),
                BackColor = clrCardBg,
                ForeColor = clrTextMain,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderColor = clrBorder;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            pnlBottom.Controls.Add(btnCancel);

            SwitchPage(0);
        }

        private Button CreateSidebarButton(string text, int top, Action onClick)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(6, top),
                Size = new Size(153, 34),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                ForeColor = clrTextMain,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private void SwitchPage(int index)
        {
            bool isPt = SelectedLanguage == "pt";
            pnlPageGeral.Visible = (index == 0);
            pnlPageFonte.Visible = (index == 1);
            pnlPageCores.Visible = (index == 2);
            pnlPageIcones.Visible = (index == 3);

            Button[] btns = new Button[] { btnNavGeral, btnNavFonte, btnNavCores, btnNavIcones };
            for (int i = 0; i < btns.Length; i++)
            {
                if (i == index)
                {
                    btns[i].BackColor = clrAccent;
                    btns[i].ForeColor = Color.White;
                    btns[i].Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                }
                else
                {
                    btns[i].BackColor = Color.Transparent;
                    btns[i].ForeColor = clrTextMain;
                    btns[i].Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
                }
            }

            if (index == 0)
            {
                lblHeaderTitle.Text = isPt ? "Preferências Gerais" : "General Preferences";
                lblHeaderSubtitle.Text = isPt ? "Configure idioma, medição, layout e proteção de exibição." : "Configure language, measurement, layout, and display protection.";
            }
            else if (index == 1)
            {
                lblHeaderTitle.Text = isPt ? "Tipografia da Barra" : "Taskbar Typography";
                lblHeaderSubtitle.Text = isPt ? "Família, tamanho e estilo calibrados para legibilidade total." : "Calibrated family, size, and style for maximum readability.";
            }
            else if (index == 2)
            {
                lblHeaderTitle.Text = isPt ? "Personalização de Cores" : "Color Customization";
                lblHeaderSubtitle.Text = isPt ? "Defina cores independentes para números, unidades e setas." : "Define independent colors for numbers, units, and arrows.";
            }
            else if (index == 3)
            {
                lblHeaderTitle.Text = isPt ? "Modelos de Ícones & Setas" : "Arrow & Icon Templates";
                lblHeaderSubtitle.Text = isPt ? "Escolha vetores modernos ou defina caracteres personalizados." : "Choose modern vectors or set custom unicode characters.";
            }
        }

        // ==============================================================================
        // PÁGINA 1: GERAL
        // ==============================================================================
        private void BuildPageGeral(Panel pnl, bool isPt)
        {
            Panel card = CreateCard(pnl, 0, 310);

            // 1. Idioma
            Label lblLang = CreateLabel(card, isPt ? "Idioma da Interface:" : "Interface Language:", 16, 16);
            cbLang = CreateComboBox(card, 160, 14, 255);
            cbLang.Items.AddRange(new object[] { "Português (Brasil)", "English (United States)" });
            cbLang.SelectedIndex = SelectedLanguage == "pt" ? 0 : 1;
            cbLang.SelectedIndexChanged += (s, e) =>
            {
                SelectedLanguage = (cbLang.SelectedIndex == 0) ? "pt" : "en";
            };

            // 2. Formato de Medição (Bits vs Bytes) - Padrão: Bits
            Label lblFmt = CreateLabel(card, isPt ? "Formato de Medição:" : "Measurement Format:", 16, 52);
            cbFormat = CreateComboBox(card, 160, 50, 255);
            cbFormat.Items.AddRange(new object[] {
                isPt ? "Bits (Mbps, Kbps, Gbps) [Padrão]" : "Bits (Mbps, Kbps, Gbps) [Default]",
                isPt ? "Bytes (MB/s, KB/s, GB/s)" : "Bytes (MB/s, KB/s, GB/s)"
            });
            cbFormat.SelectedIndex = (SelectedSpeedUnitFormat == "Bytes") ? 1 : 0;
            cbFormat.SelectedIndexChanged += (s, e) =>
            {
                SelectedSpeedUnitFormat = (cbFormat.SelectedIndex == 1) ? "Bytes" : "Bits";
                UpdateUnitComboItems(isPt);
                pnlLivePreview.Invalidate();
            };

            // 3. Escala da Unidade
            Label lblUnit = CreateLabel(card, isPt ? "Escala da Unidade:" : "Unit Scale:", 16, 88);
            cbUnits = CreateComboBox(card, 160, 86, 255);
            UpdateUnitComboItems(isPt);
            cbUnits.SelectedIndexChanged += (s, e) =>
            {
                if (cbUnits.SelectedIndex == 0) SelectedUnitMode = "Auto";
                else SelectedUnitMode = cbUnits.SelectedItem.ToString();
                pnlLivePreview.Invalidate();
            };

            // Linha divisória
            Panel div = new Panel { Location = new Point(16, 126), Size = new Size(card.Width - 32, 1), BackColor = clrBorder };
            card.Controls.Add(div);

            // 4. Posição das Setas
            Label lblArrPos = CreateLabel(card, isPt ? "Posição das Setas:" : "Arrow Position:", 16, 138);

            rbRight = new RadioButton
            {
                Text = isPt ? "À Direita (823 Kbps ▲) [Padrão]" : "On Right (823 Kbps ▲) [Default]",
                Location = new Point(160, 136),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedArrowOnRight
            };
            rbLeft = new RadioButton
            {
                Text = isPt ? "À Esquerda (▲ 823 Kbps)" : "On Left (▲ 823 Kbps)",
                Location = new Point(160, 160),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = !SelectedArrowOnRight
            };
            rbRight.CheckedChanged += (s, e) => { SelectedArrowOnRight = rbRight.Checked; pnlLivePreview.Invalidate(); };
            card.Controls.Add(rbRight);
            card.Controls.Add(rbLeft);

            // Linha divisória
            Panel div2 = new Panel { Location = new Point(16, 194), Size = new Size(card.Width - 32, 1), BackColor = clrBorder };
            card.Controls.Add(div2);

            // 5. Opções de Sistema
            chkLock = new CheckBox
            {
                Text = isPt ? "Travar posição na barra de tarefas" : "Lock position on taskbar",
                Location = new Point(16, 206),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedIsLocked
            };
            card.Controls.Add(chkLock);

            chkScreensaver = new CheckBox
            {
                Text = isPt ? "Ocultar automaticamente no Screensaver e Tela de Bloqueio" : "Auto-hide on Screensaver and Lock Screen",
                Location = new Point(16, 234),
                AutoSize = true,
                ForeColor = clrAccent,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                Checked = SelectedHideOnScreensaver
            };
            card.Controls.Add(chkScreensaver);

            Label lblHintSs = new Label
            {
                Text = isPt ? "Evita que o monitor de rede fique visível por cima do protetor de tela." : "Prevents the network monitor from drawing over the screensaver.",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(34, 256),
                AutoSize = true
            };
            card.Controls.Add(lblHintSs);
        }

        private void UpdateUnitComboItems(bool isPt)
        {
            cbUnits.Items.Clear();
            if (SelectedSpeedUnitFormat == "Bytes")
            {
                cbUnits.Items.AddRange(new object[] {
                    isPt ? "Automático (KB/s, MB/s, GB/s)" : "Auto (KB/s, MB/s, GB/s)",
                    "KB/s", "MB/s", "GB/s"
                });
            }
            else
            {
                cbUnits.Items.AddRange(new object[] {
                    isPt ? "Automático (Kbps, Mbps, Gbps) [Padrão]" : "Auto (Kbps, Mbps, Gbps) [Default]",
                    "Kbps", "Mbps", "Gbps"
                });
            }

            int idx = 0;
            for (int i = 0; i < cbUnits.Items.Count; i++)
            {
                if (cbUnits.Items[i].ToString().StartsWith(SelectedUnitMode, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            cbUnits.SelectedIndex = idx;
        }

        // ==============================================================================
        // PÁGINA 2: TIPOGRAFIA
        // ==============================================================================
        private void BuildPageFonte(Panel pnl, bool isPt)
        {
            Panel card = CreateCard(pnl, 0, 290);

            // 1. Família da Fonte
            Label lblFam = CreateLabel(card, isPt ? "Família da Fonte:" : "Font Family:", 16, 20);
            cbFontFamily = CreateComboBox(card, 160, 18, 255);

            string[] commonFonts = new string[] {
                "Segoe UI Variable Display",
                "Segoe UI",
                "Consolas",
                "Aptos",
                "Arial",
                "Tahoma",
                "Verdana",
                "Cascadia Code"
            };
            foreach (var fn in commonFonts)
            {
                if (NetMonitorApp.IsFontInstalled(fn) || fn == "Segoe UI" || fn == "Segoe UI Variable Display")
                    cbFontFamily.Items.Add(fn);
            }

            int selFontIdx = 0;
            for (int i = 0; i < cbFontFamily.Items.Count; i++)
            {
                if (string.Equals(cbFontFamily.Items[i].ToString(), SelectedFontFamily, StringComparison.OrdinalIgnoreCase))
                {
                    selFontIdx = i;
                    break;
                }
            }
            cbFontFamily.SelectedIndex = selFontIdx;
            cbFontFamily.SelectedIndexChanged += (s, e) =>
            {
                SelectedFontFamily = cbFontFamily.SelectedItem.ToString();
                pnlLivePreview.Invalidate();
            };

            // 2. Tamanho da Fonte
            Label lblSz = CreateLabel(card, isPt ? "Tamanho da Fonte:" : "Font Size:", 16, 62);
            cbFontSize = CreateComboBox(card, 160, 60, 255);
            cbFontSize.Items.AddRange(new object[] {
                "8.0 pt", "8.5 pt", "9.0 pt",
                isPt ? "9.5 pt [Padrão de Fábrica]" : "9.5 pt [Default]",
                "10.0 pt", "10.5 pt", "11.0 pt", "12.0 pt"
            });

            if (Math.Abs(SelectedFontSize - 8.0f) < 0.1f) cbFontSize.SelectedIndex = 0;
            else if (Math.Abs(SelectedFontSize - 8.5f) < 0.1f) cbFontSize.SelectedIndex = 1;
            else if (Math.Abs(SelectedFontSize - 9.0f) < 0.1f) cbFontSize.SelectedIndex = 2;
            else if (Math.Abs(SelectedFontSize - 9.5f) < 0.1f) cbFontSize.SelectedIndex = 3;
            else if (Math.Abs(SelectedFontSize - 10.0f) < 0.1f) cbFontSize.SelectedIndex = 4;
            else if (Math.Abs(SelectedFontSize - 10.5f) < 0.1f) cbFontSize.SelectedIndex = 5;
            else if (Math.Abs(SelectedFontSize - 11.0f) < 0.1f) cbFontSize.SelectedIndex = 6;
            else if (Math.Abs(SelectedFontSize - 12.0f) < 0.1f) cbFontSize.SelectedIndex = 7;
            else cbFontSize.SelectedIndex = 3;

            cbFontSize.SelectedIndexChanged += (s, e) =>
            {
                int idx = cbFontSize.SelectedIndex;
                float[] sizes = new float[] { 8.0f, 8.5f, 9.0f, 9.5f, 10.0f, 10.5f, 11.0f, 12.0f };
                SelectedFontSize = (idx >= 0 && idx < sizes.Length) ? sizes[idx] : 9.5f;
                pnlLivePreview.Invalidate();
            };

            // 3. Estilo da Fonte
            Label lblSt = CreateLabel(card, isPt ? "Estilo da Fonte:" : "Font Style:", 16, 104);
            cbFontStyle = CreateComboBox(card, 160, 102, 255);
            cbFontStyle.Items.AddRange(new object[] {
                isPt ? "Normal (Regular)" : "Regular",
                isPt ? "Negrito (Bold) [Padrão de Fábrica]" : "Bold [Default]",
                isPt ? "Itálico (Italic)" : "Italic"
            });

            if (SelectedFontStyle == FontStyle.Bold) cbFontStyle.SelectedIndex = 1;
            else if (SelectedFontStyle == FontStyle.Italic) cbFontStyle.SelectedIndex = 2;
            else cbFontStyle.SelectedIndex = 0;

            cbFontStyle.SelectedIndexChanged += (s, e) =>
            {
                if (cbFontStyle.SelectedIndex == 1) SelectedFontStyle = FontStyle.Bold;
                else if (cbFontStyle.SelectedIndex == 2) SelectedFontStyle = FontStyle.Italic;
                else SelectedFontStyle = FontStyle.Regular;
                pnlLivePreview.Invalidate();
            };

            // Linha divisória
            Panel div = new Panel { Location = new Point(16, 150), Size = new Size(card.Width - 32, 1), BackColor = clrBorder };
            card.Controls.Add(div);

            // Dica de tipografia
            Label lblTip = new Label
            {
                Text = isPt ?
                    "Dica de Nitidez: A fonte 'Segoe UI Variable Display' a 9.5 pt Negrito oferece contraste e legibilidade ideais na barra de tarefas do Windows 11, adaptando automaticamente o espaçamento das unidades." :
                    "Sharpness Tip: 'Segoe UI Variable Display' at 9.5 pt Bold offers ideal contrast and readability on Windows 11 taskbar.",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(16, 166),
                Size = new Size(card.Width - 32, 80)
            };
            card.Controls.Add(lblTip);
        }

        // ==============================================================================
        // PÁGINA 3: CORES (NÚMEROS, UNIDADES E SETAS INDEPENDENTES)
        // ==============================================================================
        private void BuildPageCores(Panel pnl, bool isPt)
        {
            Panel card = CreateCard(pnl, 0, 310);

            // -------------------------------------------------------------
            // Seção 1: Números de Velocidade
            // -------------------------------------------------------------
            Label lblSecNum = new Label
            {
                Text = isPt ? "1. Números da Velocidade:" : "1. Speed Numbers:",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = clrAccent,
                Location = new Point(14, 12),
                AutoSize = true
            };
            card.Controls.Add(lblSecNum);

            rbTextUnified = new RadioButton
            {
                Text = isPt ? "Unificado" : "Unified",
                Location = new Point(180, 10),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedTextColorMode == "Unified"
            };
            rbTextIndividual = new RadioButton
            {
                Text = isPt ? "Individual" : "Individual",
                Location = new Point(260, 10),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedTextColorMode == "Individual"
            };
            card.Controls.Add(rbTextUnified);
            card.Controls.Add(rbTextIndividual);

            btnColorTextBoth = CreateColorButton(card, isPt ? "Ambos" : "Both", 14, 34, SelectedTextColorUnified, (col) => {
                SelectedTextColorUnified = col;
                btnColorTextBoth.BackColor = col;
                btnColorTextBoth.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
            });

            btnColorTextUp = CreateColorButton(card, "Upload ▲", 152, 34, SelectedTextColorUp, (col) => {
                SelectedTextColorUp = col;
                btnColorTextUp.BackColor = col;
                btnColorTextUp.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
            });

            btnColorTextDown = CreateColorButton(card, "Download ▼", 290, 34, SelectedTextColorDown, (col) => {
                SelectedTextColorDown = col;
                btnColorTextDown.BackColor = col;
                btnColorTextDown.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
            });

            rbTextUnified.CheckedChanged += (s, e) => UpdateColorControlsState();
            rbTextIndividual.CheckedChanged += (s, e) => UpdateColorControlsState();

            // Divisor
            Panel div1 = new Panel { Location = new Point(14, 76), Size = new Size(card.Width - 28, 1), BackColor = clrBorder };
            card.Controls.Add(div1);

            // -------------------------------------------------------------
            // Seção 2: Unidade de Medida (Kbps / Mbps / MB/s)
            // -------------------------------------------------------------
            Label lblSecUnit = new Label
            {
                Text = isPt ? "2. Unidade (Kbps/MB/s):" : "2. Unit (Kbps/MB/s):",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = clrAccent,
                Location = new Point(14, 86),
                AutoSize = true
            };
            card.Controls.Add(lblSecUnit);

            rbUnitInherit = new RadioButton
            {
                Text = isPt ? "Igual Números" : "Match Numbers",
                Location = new Point(160, 84),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedUnitColorMode == "Inherit"
            };
            rbUnitUnified = new RadioButton
            {
                Text = isPt ? "Unificado" : "Unified",
                Location = new Point(270, 84),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedUnitColorMode == "Unified"
            };
            rbUnitIndividual = new RadioButton
            {
                Text = isPt ? "Individual" : "Individual",
                Location = new Point(345, 84),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedUnitColorMode == "Individual"
            };
            card.Controls.Add(rbUnitInherit);
            card.Controls.Add(rbUnitUnified);
            card.Controls.Add(rbUnitIndividual);

            btnColorUnitBoth = CreateColorButton(card, isPt ? "Unidade" : "Unit", 14, 108, SelectedUnitColorUnified, (col) => {
                SelectedUnitColorUnified = col;
                btnColorUnitBoth.BackColor = col;
                btnColorUnitBoth.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
            });

            btnColorUnitUp = CreateColorButton(card, isPt ? "Unidade Up" : "Unit Up", 152, 108, SelectedUnitColorUp, (col) => {
                SelectedUnitColorUp = col;
                btnColorUnitUp.BackColor = col;
                btnColorUnitUp.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
            });

            btnColorUnitDown = CreateColorButton(card, isPt ? "Unidade Down" : "Unit Down", 290, 108, SelectedUnitColorDown, (col) => {
                SelectedUnitColorDown = col;
                btnColorUnitDown.BackColor = col;
                btnColorUnitDown.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
            });

            rbUnitInherit.CheckedChanged += (s, e) => UpdateColorControlsState();
            rbUnitUnified.CheckedChanged += (s, e) => UpdateColorControlsState();
            rbUnitIndividual.CheckedChanged += (s, e) => UpdateColorControlsState();

            // Divisor
            Panel div2 = new Panel { Location = new Point(14, 150), Size = new Size(card.Width - 28, 1), BackColor = clrBorder };
            card.Controls.Add(div2);

            // -------------------------------------------------------------
            // Seção 3: Setas de Tráfego
            // -------------------------------------------------------------
            Label lblSecArr = new Label
            {
                Text = isPt ? "3. Setas de Tráfego:" : "3. Traffic Arrows:",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = clrAccent,
                Location = new Point(14, 160),
                AutoSize = true
            };
            card.Controls.Add(lblSecArr);

            rbArrowUnified = new RadioButton
            {
                Text = isPt ? "Unificado" : "Unified",
                Location = new Point(180, 158),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedArrowColorMode == "Unified"
            };
            rbArrowIndividual = new RadioButton
            {
                Text = isPt ? "Individual" : "Individual",
                Location = new Point(260, 158),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedArrowColorMode == "Individual"
            };
            card.Controls.Add(rbArrowUnified);
            card.Controls.Add(rbArrowIndividual);

            btnColorArrowBoth = CreateColorButton(card, isPt ? "Ambas" : "Both", 14, 182, SelectedArrowColorUnified, (col) => {
                SelectedArrowColorUnified = col;
                btnColorArrowBoth.BackColor = col;
                btnColorArrowBoth.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
            });

            btnColorArrowUp = CreateColorButton(card, "Seta Up ▲", 152, 182, SelectedArrowColorUp, (col) => {
                SelectedArrowColorUp = col;
                btnColorArrowUp.BackColor = col;
                btnColorArrowUp.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
            });

            btnColorArrowDown = CreateColorButton(card, "Seta Down ▼", 290, 182, SelectedArrowColorDown, (col) => {
                SelectedArrowColorDown = col;
                btnColorArrowDown.BackColor = col;
                btnColorArrowDown.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
            });

            rbArrowUnified.CheckedChanged += (s, e) => UpdateColorControlsState();
            rbArrowIndividual.CheckedChanged += (s, e) => UpdateColorControlsState();

            // Dica de paleta
            Label lblColorTip = new Label
            {
                Text = isPt ?
                    "Clique sobre qualquer botão colorido para abrir o seletor visual e ajustar a cor exata." :
                    "Click on any color button to open the visual palette dialog.",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(14, 230),
                AutoSize = true
            };
            card.Controls.Add(lblColorTip);

            UpdateColorControlsState();
        }

        private void UpdateColorControlsState()
        {
            if (rbTextUnified == null || btnColorTextBoth == null || btnColorTextUp == null || btnColorTextDown == null ||
                rbUnitInherit == null || btnColorUnitBoth == null || btnColorUnitUp == null || btnColorUnitDown == null ||
                rbArrowUnified == null || btnColorArrowBoth == null || btnColorArrowUp == null || btnColorArrowDown == null)
            {
                return;
            }

            bool isTextUni = rbTextUnified.Checked;
            btnColorTextBoth.Enabled = isTextUni;
            btnColorTextUp.Enabled = !isTextUni;
            btnColorTextDown.Enabled = !isTextUni;

            bool isUnitInh = rbUnitInherit.Checked;
            bool isUnitUni = rbUnitUnified.Checked;
            btnColorUnitBoth.Enabled = isUnitUni;
            btnColorUnitUp.Enabled = (!isUnitInh && !isUnitUni);
            btnColorUnitDown.Enabled = (!isUnitInh && !isUnitUni);

            bool isArrUni = rbArrowUnified.Checked;
            btnColorArrowBoth.Enabled = isArrUni;
            btnColorArrowUp.Enabled = !isArrUni;
            btnColorArrowDown.Enabled = !isArrUni;

            if (pnlLivePreview != null) pnlLivePreview.Invalidate();
        }

        private Button CreateColorButton(Panel parent, string title, int left, int top, Color initialColor, Action<Color> onColorChanged)
        {
            Button btn = new Button
            {
                Text = string.Format("{0}\n{1}", title, NetMonitorApp.ColorToHex(initialColor)),
                Location = new Point(left, top),
                Size = new Size(130, 36),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                BackColor = initialColor,
                ForeColor = GetContrastTextColor(initialColor),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = clrBorder;
            btn.Click += (s, e) =>
            {
                using (ColorDialog cd = new ColorDialog())
                {
                    cd.Color = btn.BackColor;
                    cd.FullOpen = true;
                    if (cd.ShowDialog() == DialogResult.OK)
                    {
                        btn.Text = string.Format("{0}\n{1}", title, NetMonitorApp.ColorToHex(cd.Color));
                        onColorChanged(cd.Color);
                    }
                }
            };
            parent.Controls.Add(btn);
            return btn;
        }

        private static Color GetContrastTextColor(Color bg)
        {
            double brightness = (bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114);
            return brightness > 140 ? Color.FromArgb(20, 20, 20) : Color.FromArgb(250, 250, 250);
        }

        // ==============================================================================
        // PÁGINA 4: ÍCONES E MODELOS DE SETAS
        // ==============================================================================
        private void BuildPageIcones(Panel pnl, bool isPt)
        {
            Panel card = CreateCard(pnl, 0, 290);

            Label lblTpl = CreateLabel(card, isPt ? "Modelo Visual das Setas:" : "Arrow Visual Model:", 16, 20);
            cbTemplate = CreateComboBox(card, 16, 44, 400);
            cbTemplate.Items.AddRange(new object[] {
                isPt ? "Triângulos Modernos (▲ / ▼) [Vetor Limpo Padrão]" : "Modern Triangles (▲ / ▼) [Clean Vector Default]",
                isPt ? "Chevrons Fluent (⌃ / ⌄) [Vetor Linha]" : "Fluent Chevrons (⌃ / ⌄) [Vector Outline]",
                isPt ? "Chevrons Duplos Velocidade (▲▲ / ▼▼) [Novo]" : "Dual Speed Chevrons (▲▲ / ▼▼) [New]",
                isPt ? "Stream Tech Diagonal (↗ / ↘) [Novo]" : "Stream Tech Diagonal (↗ / ↘) [New]",
                isPt ? "Clássico Unicode (▲ / ▼)" : "Classic Unicode (▲ / ▼)",
                isPt ? "Personalizado (Digite caracteres próprios)" : "Custom (Enter custom characters)"
            });

            if (SelectedIconTemplate == "VectorTriangle") cbTemplate.SelectedIndex = 0;
            else if (SelectedIconTemplate == "VectorChevron") cbTemplate.SelectedIndex = 1;
            else if (SelectedIconTemplate == "VectorDualChevron") cbTemplate.SelectedIndex = 2;
            else if (SelectedIconTemplate == "StreamTech") cbTemplate.SelectedIndex = 3;
            else if (SelectedIconTemplate == "UnicodeClassic") cbTemplate.SelectedIndex = 4;
            else cbTemplate.SelectedIndex = 5;

            // Linha divisória
            Panel div = new Panel { Location = new Point(16, 92), Size = new Size(card.Width - 32, 1), BackColor = clrBorder };
            card.Controls.Add(div);

            // Caracteres Personalizados
            lblUp = CreateLabel(card, isPt ? "Caractere Upload:" : "Upload Character:", 16, 114);
            txtUp = new TextBox
            {
                Location = new Point(160, 112),
                Size = new Size(120, 24),
                BackColor = clrInputBg,
                ForeColor = clrTextMain,
                Text = SelectedCustomUpChar
            };
            card.Controls.Add(lblUp);
            card.Controls.Add(txtUp);

            lblDown = CreateLabel(card, isPt ? "Caractere Download:" : "Download Character:", 16, 150);
            txtDown = new TextBox
            {
                Location = new Point(160, 148),
                Size = new Size(120, 24),
                BackColor = clrInputBg,
                ForeColor = clrTextMain,
                Text = SelectedCustomDownChar
            };
            card.Controls.Add(lblDown);
            card.Controls.Add(txtDown);

            txtUp.TextChanged += (s, e) => { SelectedCustomUpChar = txtUp.Text; pnlLivePreview.Invalidate(); };
            txtDown.TextChanged += (s, e) => { SelectedCustomDownChar = txtDown.Text; pnlLivePreview.Invalidate(); };

            cbTemplate.SelectedIndexChanged += (s, e) =>
            {
                int idx = cbTemplate.SelectedIndex;
                if (idx == 0) SelectedIconTemplate = "VectorTriangle";
                else if (idx == 1) SelectedIconTemplate = "VectorChevron";
                else if (idx == 2) SelectedIconTemplate = "VectorDualChevron";
                else if (idx == 3) SelectedIconTemplate = "StreamTech";
                else if (idx == 4) SelectedIconTemplate = "UnicodeClassic";
                else SelectedIconTemplate = "UnicodeCustom";

                bool isCustom = (idx == 5);
                lblUp.Enabled = isCustom;
                txtUp.Enabled = isCustom;
                lblDown.Enabled = isCustom;
                txtDown.Enabled = isCustom;

                pnlLivePreview.Invalidate();
            };

            bool customInit = (cbTemplate.SelectedIndex == 5);
            lblUp.Enabled = customInit;
            txtUp.Enabled = customInit;
            lblDown.Enabled = customInit;
            txtDown.Enabled = customInit;

            Label lblNote = new Label
            {
                Text = isPt ?
                    "Os modelos vetoriais (Triângulos, Chevrons e Stream Tech) são renderizados matematicamente, garantindo máxima nitidez em qualquer escala de DPI sem pixelização." :
                    "Vector models are rendered mathematically, ensuring maximum sharpness at any DPI scale.",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(16, 190),
                Size = new Size(card.Width - 32, 70)
            };
            card.Controls.Add(lblNote);
        }

        // ==============================================================================
        // LIVE PREVIEW DINÂMICO
        // ==============================================================================
        private void PnlLivePreview_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.FromArgb(28, 28, 28));
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color cNumUp = rbTextIndividual.Checked ? btnColorTextUp.BackColor : btnColorTextBoth.BackColor;
            Color cNumDown = rbTextIndividual.Checked ? btnColorTextDown.BackColor : btnColorTextBoth.BackColor;

            Color cUnitUp;
            Color cUnitDown;
            if (rbUnitIndividual.Checked)
            {
                cUnitUp = btnColorUnitUp.BackColor;
                cUnitDown = btnColorUnitDown.BackColor;
            }
            else if (rbUnitUnified.Checked)
            {
                cUnitUp = btnColorUnitBoth.BackColor;
                cUnitDown = btnColorUnitBoth.BackColor;
            }
            else // Inherit
            {
                cUnitUp = Color.FromArgb((int)(cNumUp.R * 0.85f), (int)(cNumUp.G * 0.85f), (int)(cNumUp.B * 0.85f));
                cUnitDown = Color.FromArgb((int)(cNumDown.R * 0.85f), (int)(cNumDown.G * 0.85f), (int)(cNumDown.B * 0.85f));
            }

            Color cArrUp = rbArrowIndividual.Checked ? btnColorArrowUp.BackColor : btnColorArrowBoth.BackColor;
            Color cArrDown = rbArrowIndividual.Checked ? btnColorArrowDown.BackColor : btnColorArrowBoth.BackColor;

            Font fNum = null;
            Font fUnit = null;
            Font fUni = null;
            try
            {
                fNum = new Font(SelectedFontFamily, SelectedFontSize, SelectedFontStyle);
            }
            catch
            {
                fNum = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            }

            try
            {
                float unitSz = Math.Max(7.0f, SelectedFontSize - 1.5f);
                fUnit = new Font(SelectedFontFamily, unitSz, FontStyle.Regular);
            }
            catch
            {
                fUnit = new Font("Segoe UI", 8.0f, FontStyle.Regular);
            }
            fUni = new Font("Segoe UI Symbol", 8.0f, FontStyle.Regular);

            using (StringFormat sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            using (StringFormat sfL = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (StringFormat sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (Brush bNumUp = new SolidBrush(cNumUp))
            using (Brush bNumDown = new SolidBrush(cNumDown))
            using (Brush bUnitUp = new SolidBrush(cUnitUp))
            using (Brush bUnitDown = new SolidBrush(cUnitDown))
            using (Brush bArrUp = new SolidBrush(cArrUp))
            using (Brush bArrDown = new SolidBrush(cArrDown))
            using (Pen pArrUp = new Pen(cArrUp, 1.4f))
            using (Pen pArrDown = new Pen(cArrDown, 1.4f))
            {
                string unitStr = (SelectedSpeedUnitFormat == "Bytes") ? "MB/s" : "Kbps";

                if (SelectedArrowOnRight)
                {
                    RectangleF rUpNum = new RectangleF(2, 2, 48, 18);
                    RectangleF rDnNum = new RectangleF(2, 21, 48, 18);
                    g.DrawString("823.6", fNum, bNumUp, rUpNum, sfR);
                    g.DrawString("142.0", fNum, bNumDown, rDnNum, sfR);

                    RectangleF rUpU = new RectangleF(52, 2, 40, 18);
                    RectangleF rDnU = new RectangleF(52, 21, 40, 18);
                    g.DrawString(unitStr, fUnit, bUnitUp, rUpU, sfL);
                    g.DrawString(unitStr, fUnit, bUnitDown, rDnU, sfL);

                    DrawPreviewArrow(g, 100.0f, bArrUp, bArrDown, pArrUp, pArrDown, fUni, sfC);
                }
                else
                {
                    DrawPreviewArrow(g, 10.0f, bArrUp, bArrDown, pArrUp, pArrDown, fUni, sfC);

                    RectangleF rUpNum = new RectangleF(20, 2, 44, 18);
                    RectangleF rDnNum = new RectangleF(20, 21, 44, 18);
                    g.DrawString("823.6", fNum, bNumUp, rUpNum, sfR);
                    g.DrawString("142.0", fNum, bNumDown, rDnNum, sfR);

                    RectangleF rUpU = new RectangleF(66, 2, 42, 18);
                    RectangleF rDnU = new RectangleF(66, 21, 42, 18);
                    g.DrawString(unitStr, fUnit, bUnitUp, rUpU, sfL);
                    g.DrawString(unitStr, fUnit, bUnitDown, rDnU, sfL);
                }
            }

            if (fNum != null) fNum.Dispose();
            if (fUnit != null) fUnit.Dispose();
            if (fUni != null) fUni.Dispose();
        }

        private void DrawPreviewArrow(Graphics g, float cx, Brush bUp, Brush bDown, Pen pUp, Pen pDown, Font fUni, StringFormat sfC)
        {
            if (SelectedIconTemplate == "VectorTriangle")
            {
                g.FillPolygon(bUp, new PointF[] {
                    new PointF(cx, 6.0f), new PointF(cx - 3.8f, 11.0f), new PointF(cx + 3.8f, 11.0f)
                });
                g.FillPolygon(bDown, new PointF[] {
                    new PointF(cx, 34.0f), new PointF(cx - 3.8f, 29.0f), new PointF(cx + 3.8f, 29.0f)
                });
            }
            else if (SelectedIconTemplate == "VectorChevron")
            {
                g.DrawLines(pUp, new PointF[] {
                    new PointF(cx - 3.8f, 10.5f), new PointF(cx, 6.5f), new PointF(cx + 3.8f, 10.5f)
                });
                g.DrawLines(pDown, new PointF[] {
                    new PointF(cx - 3.8f, 29.0f), new PointF(cx, 33.0f), new PointF(cx + 3.8f, 29.0f)
                });
            }
            else if (SelectedIconTemplate == "VectorDualChevron")
            {
                g.DrawLines(pUp, new PointF[] { new PointF(cx - 3.5f, 7.5f), new PointF(cx, 4.5f), new PointF(cx + 3.5f, 7.5f) });
                g.DrawLines(pUp, new PointF[] { new PointF(cx - 3.5f, 11.5f), new PointF(cx, 8.5f), new PointF(cx + 3.5f, 11.5f) });
                g.DrawLines(pDown, new PointF[] { new PointF(cx - 3.5f, 27.5f), new PointF(cx, 30.5f), new PointF(cx + 3.5f, 27.5f) });
                g.DrawLines(pDown, new PointF[] { new PointF(cx - 3.5f, 31.0f), new PointF(cx, 34.0f), new PointF(cx + 3.5f, 31.0f) });
            }
            else if (SelectedIconTemplate == "StreamTech")
            {
                g.DrawLine(pUp, cx - 3.5f, 11.5f, cx + 3.0f, 5.5f);
                g.DrawLines(pUp, new PointF[] { new PointF(cx - 1.0f, 5.5f), new PointF(cx + 3.0f, 5.5f), new PointF(cx + 3.0f, 9.5f) });
                g.DrawLine(pDown, cx - 3.5f, 27.5f, cx + 3.0f, 33.5f);
                g.DrawLines(pDown, new PointF[] { new PointF(cx + 3.0f, 29.5f), new PointF(cx + 3.0f, 33.5f), new PointF(cx - 1.0f, 33.5f) });
            }
            else
            {
                string upCh = NetMonitorApp.ParseUnicodeString(SelectedCustomUpChar, "▲");
                string downCh = NetMonitorApp.ParseUnicodeString(SelectedCustomDownChar, "▼");
                g.DrawString(upCh, fUni, bUp, new RectangleF(cx - 8, 2, 16, 18), sfC);
                g.DrawString(downCh, fUni, bDown, new RectangleF(cx - 8, 21, 16, 18), sfC);
            }
        }

        // ==============================================================================
        // HELPERS DE CRIAÇÃO VISUAL
        // ==============================================================================
        private Panel CreateCard(Panel parent, int top, int height)
        {
            Panel card = new Panel
            {
                Location = new Point(0, top),
                Size = new Size(parent.Width, height),
                BackColor = clrCardBg
            };
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(clrBorder, 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                }
            };
            parent.Controls.Add(card);
            return card;
        }

        private Label CreateLabel(Panel parent, string text, int left, int top)
        {
            Label lbl = new Label
            {
                Text = text,
                Location = new Point(left, top),
                AutoSize = true,
                ForeColor = clrTextMain,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular)
            };
            parent.Controls.Add(lbl);
            return lbl;
        }

        private ComboBox CreateComboBox(Panel parent, int left, int top, int width)
        {
            ComboBox cb = new ComboBox
            {
                Location = new Point(left, top),
                Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = clrInputBg,
                ForeColor = clrTextMain,
                FlatStyle = FlatStyle.Flat
            };
            parent.Controls.Add(cb);
            return cb;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;

            SelectedLanguage = (cbLang.SelectedIndex == 0) ? "pt" : "en";
            SelectedSpeedUnitFormat = (cbFormat.SelectedIndex == 1) ? "Bytes" : "Bits";

            if (cbUnits.SelectedIndex == 0) SelectedUnitMode = "Auto";
            else SelectedUnitMode = cbUnits.SelectedItem.ToString();

            SelectedArrowOnRight = rbRight.Checked;
            SelectedIsLocked = chkLock.Checked;
            SelectedHideOnScreensaver = chkScreensaver.Checked;

            SelectedFontFamily = cbFontFamily.SelectedItem != null ? cbFontFamily.SelectedItem.ToString() : "Segoe UI Variable Display";

            float[] sizes = new float[] { 8.0f, 8.5f, 9.0f, 9.5f, 10.0f, 10.5f, 11.0f, 12.0f };
            int sIdx = cbFontSize.SelectedIndex;
            SelectedFontSize = (sIdx >= 0 && sIdx < sizes.Length) ? sizes[sIdx] : 9.5f;

            if (cbFontStyle.SelectedIndex == 1) SelectedFontStyle = FontStyle.Bold;
            else if (cbFontStyle.SelectedIndex == 2) SelectedFontStyle = FontStyle.Italic;
            else SelectedFontStyle = FontStyle.Regular;

            // Cores Números
            SelectedTextColorMode = rbTextIndividual.Checked ? "Individual" : "Unified";
            SelectedTextColorUnified = btnColorTextBoth.BackColor;
            SelectedTextColorUp = btnColorTextUp.BackColor;
            SelectedTextColorDown = btnColorTextDown.BackColor;

            // Cores Unidades
            if (rbUnitIndividual.Checked) SelectedUnitColorMode = "Individual";
            else if (rbUnitUnified.Checked) SelectedUnitColorMode = "Unified";
            else SelectedUnitColorMode = "Inherit";

            SelectedUnitColorUnified = btnColorUnitBoth.BackColor;
            SelectedUnitColorUp = btnColorUnitUp.BackColor;
            SelectedUnitColorDown = btnColorUnitDown.BackColor;

            // Cores Setas
            SelectedArrowColorMode = rbArrowIndividual.Checked ? "Individual" : "Unified";
            SelectedArrowColorUnified = btnColorArrowBoth.BackColor;
            SelectedArrowColorUp = btnColorArrowUp.BackColor;
            SelectedArrowColorDown = btnColorArrowDown.BackColor;

            // Modelos de Ícones
            int tIdx = cbTemplate.SelectedIndex;
            if (tIdx == 0) SelectedIconTemplate = "VectorTriangle";
            else if (tIdx == 1) SelectedIconTemplate = "VectorChevron";
            else if (tIdx == 2) SelectedIconTemplate = "VectorDualChevron";
            else if (tIdx == 3) SelectedIconTemplate = "StreamTech";
            else if (tIdx == 4) SelectedIconTemplate = "UnicodeClassic";
            else SelectedIconTemplate = "UnicodeCustom";

            SelectedCustomUpChar = NetMonitorApp.ParseUnicodeString(txtUp.Text, "▲");
            SelectedCustomDownChar = NetMonitorApp.ParseUnicodeString(txtDown.Text, "▼");

            this.Close();
        }
    }
}
