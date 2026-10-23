// ==============================================================================
// NetMonitor - Monitor de Rede Ultraleve para Barra de Tarefas (Windows 11)
// Autor: Eduardo Noé (eduardonoe)
// Version: 1.0.0
//
// v1.0.0 changelog:
// - Top 5 Aplicativos com maior consumo de rede em tempo real no Mouse Hover (Flyout Fluente Windows 11).
// - Reordenação dinâmica instantânea a cada segundo com identificação individual de subprocessos e abas.
// - Multi-monitor e multi-resolução à prova de vazamento com clamping estrito de coordenadas e tratamento de WM_DISPLAYCHANGE.
// - Atualização em Tempo Real (Live Tuning): todas as alterações refletem instantaneamente no widget.
// - Arquitetura Proposta B: Sidebar lateral nativa Windows 11 com coordenadas absolutas e zero sobreposição.
// - Troca de tema dinâmico em tempo real (Dark / Light) com o formulário de configurações aberto na tela.
// - Auto-ocultação à prova de falhas em descanso de tela / screensaver (Toolhelp32 .scr nativo + SPI).
// - Detecção automática do idioma nativo do sistema operacional (Português / Inglês).
// - Colorização totalmente independente: Números, Unidades (Kbps/Mbps/MB/s) e Setas de Tráfego.
// - Alternância entre medição em Bits (Mbps / Kbps / Gbps) e Bytes (MB/s / KB/s / GB/s), padrão Bits.
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

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
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);

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
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetProcessIoCounters(IntPtr ProcessHandle, out IO_COUNTERS IoCounters);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder text, ref int size);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint state;
            public uint localAddr;
            public byte localPort1, localPort2, localPort3, localPort4;
            public uint remoteAddr;
            public byte remotePort1, remotePort2, remotePort3, remotePort4;
            public int owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCP6ROW_OWNER_PID
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public uint dwLocalScopeId;
            public byte localPort1, localPort2, localPort3, localPort4;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucRemoteAddr;
            public uint dwRemoteScopeId;
            public byte remotePort1, remotePort2, remotePort3, remotePort4;
            public uint state;
            public int owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_UDPROW_OWNER_PID
        {
            public uint localAddr;
            public byte localPort1, localPort2, localPort3, localPort4;
            public int owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_UDP6ROW_OWNER_PID
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public uint dwLocalScopeId;
            public byte localPort1, localPort2, localPort3, localPort4;
            public int owningPid;
        }

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
        private const int WM_DISPLAYCHANGE = 0x007E;
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
        private const uint SWP_HIDEWINDOW = 0x0080;

        public const uint SPI_GETSCREENSAVERRUNNING = 0x0072;
        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private const int AF_INET = 2;
        private const int AF_INET6 = 23;
        private const int TCP_TABLE_OWNER_PID_ALL = 5;
        private const int UDP_TABLE_OWNER_PID = 1;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private const string REG_KEY_APP = @"Software\NetMonitor";
        private const string REG_KEY_RUN = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "NetMonitor";

        // Dimensões do Widget na Barra de Tarefas
        private const int WIDGET_WIDTH = 106;
        private const int WIDGET_HEIGHT = 38;

        // Paleta base do widget
        public static readonly Color BG_COLOR_DARK = Color.FromArgb(28, 28, 28);
        public static readonly Color BG_COLOR_LIGHT = Color.FromArgb(243, 243, 243);
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
        private string textColorMode = "Unified";
        private Color textColorUnified = Color.FromArgb(255, 255, 255);
        private Color textColorUp = Color.FromArgb(255, 255, 255);
        private Color textColorDown = Color.FromArgb(255, 255, 255);

        // Cores das Unidades (Kbps / Mbps / MB/s)
        private string unitColorMode = "Inherit";
        private Color unitColorUnified = Color.FromArgb(190, 190, 190);
        private Color unitColorUp = Color.FromArgb(190, 190, 190);
        private Color unitColorDown = Color.FromArgb(190, 190, 190);

        // Cores das Setas
        private string arrowColorMode = "Unified";
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
        private string speedUnitFormat = "Bits";      // "Bits" ou "Bytes"
        private string unitMode = "Auto";             // "Auto" ou fixa
        private bool arrowOnRight = true;             // Seta à direita por padrão
        private bool isLocked = false;                // Trava de posição
        private bool hideOnScreensaver = true;        // Ocultar em screensaver / lock screen
        private string iconTemplate = "VectorTriangle";
        private string customUpChar = "▲";
        private string customDownChar = "▼";

        // Estado do Screensaver / Sessão
        private bool isSessionLocked = false;
        private bool isScreensaverRunning = false;
        private bool isSettingsDialogOpen = false;

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

        // Hover Flyout (Top 5 Apps por Consumo de Rede em Tempo Real)
        public bool showHoverFlyout = true;
        private NetworkTopProcessesFlyout flyoutForm;
        private Timer hoverCheckTimer;
        private Timer processSampleTimer;
        private bool isHoverSampling = false;
        private Dictionary<int, ulong> lastProcessIo = new Dictionary<int, ulong>();
        private Dictionary<int, string> cachedProcessNames = new Dictionary<int, string>();

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

            this.MouseEnter += OnWidgetMouseEnter;
            this.MouseLeave += OnWidgetMouseLeave;

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

        public static string GetSystemDefaultLanguage()
        {
            try
            {
                string twoLetter = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                return (twoLetter == "pt") ? "pt" : "en";
            }
            catch
            {
                return "pt";
            }
        }

        private static bool IsScreensaverName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.EndsWith(".scr", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("screensaver", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   string.Equals(name, "Bubbles", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Mystify", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Ribbons", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "PhotoScreensaver", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "ssText3d", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "scrnsave", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAnyScreensaverActive()
        {
            try
            {
                // 1. Verificação oficial SPI (quando o Windows ativa automaticamente por timeout de ociosidade)
                bool ss = false;
                if (SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0, ref ss, 0) && ss)
                    return true;

                // 2. Janela clássica de screensaver de tela cheia
                if (FindWindow("WindowsScreenSaverClass", null) != IntPtr.Zero)
                    return true;

                // 3. Verificação da Janela em Primeiro Plano (cobre imediatamente o botão "Visualizar" de desk.cpl)
                IntPtr fg = GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    uint fgPid = 0;
                    GetWindowThreadProcessId(fg, out fgPid);
                    if (fgPid > 0)
                    {
                        try
                        {
                            using (Process p = Process.GetProcessById((int)fgPid))
                            {
                                if (IsScreensaverName(p.ProcessName))
                                {
                                    RECT rc;
                                    if (GetWindowRect(fg, out rc))
                                    {
                                        int w = rc.Right - rc.Left;
                                        int h = rc.Bottom - rc.Top;
                                        // Mini-preview tem ~150x110px. Tela cheia tem >= 640x480px.
                                        if (w >= 400 && h >= 300)
                                            return true;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                // 4. Varredura via Toolhelp32 de processos .scr ativos em tela cheia (sem pai e com tamanho real)
                IntPtr hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
                if (hSnap != IntPtr.Zero && hSnap != (IntPtr)(-1))
                {
                    try
                    {
                        PROCESSENTRY32 pe = new PROCESSENTRY32();
                        pe.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                        if (Process32First(hSnap, ref pe))
                        {
                            do
                            {
                                if (IsScreensaverName(pe.szExeFile))
                                {
                                    try
                                    {
                                        using (Process p = Process.GetProcessById((int)pe.th32ProcessID))
                                        {
                                            IntPtr hMain = p.MainWindowHandle;
                                            if (hMain != IntPtr.Zero)
                                            {
                                                RECT rc;
                                                if (GetWindowRect(hMain, out rc))
                                                {
                                                    int w = rc.Right - rc.Left;
                                                    int h = rc.Bottom - rc.Top;
                                                    // Em mini-preview, GetParent != IntPtr.Zero e tamanho < 200px.
                                                    // Em tela cheia ("Visualizar" ou timer), GetParent == IntPtr.Zero e cobre a tela.
                                                    if (w >= 400 && h >= 300 && GetParent(hMain) == IntPtr.Zero)
                                                    {
                                                        return true;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            } while (Process32Next(hSnap, ref pe));
                        }
                    }
                    finally
                    {
                        CloseHandle(hSnap);
                    }
                }
            }
            catch { }

            return false;
        }

        private void SetupFonts()
        {
            if (fontNum != null) fontNum.Dispose();
            if (fontUnit != null) fontUnit.Dispose();
            if (fontUnicode != null) fontUnicode.Dispose();

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

        private Color EnsureLegibleContrast(Color c, Color bg)
        {
            double lumC = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            double lumBg = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;

            if (lumBg < 128 && lumC < 90)
            {
                return Color.FromArgb(245, 245, 245);
            }
            if (lumBg >= 128 && lumC > 175)
            {
                return Color.FromArgb(25, 25, 25);
            }
            return c;
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

            cNumUp = EnsureLegibleContrast(cNumUp, currentBgColor);
            cNumDown = EnsureLegibleContrast(cNumDown, currentBgColor);
            cUnitUp = EnsureLegibleContrast(cUnitUp, currentBgColor);
            cUnitDown = EnsureLegibleContrast(cUnitDown, currentBgColor);
            cArrUp = EnsureLegibleContrast(cArrUp, currentBgColor);
            cArrDown = EnsureLegibleContrast(cArrDown, currentBgColor);

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
            int tickCount = 0;
            updateTimer = new Timer();
            updateTimer.Interval = 300;
            updateTimer.Tick += (s, e) =>
            {
                // Proteção contra descanso de tela / screensaver
                if (hideOnScreensaver)
                {
                    bool ss = IsAnyScreensaverActive();
                    if (ss || isSessionLocked)
                    {
                        if (!isScreensaverRunning || this.Visible)
                        {
                            isScreensaverRunning = true;
                            this.Visible = false;
                            SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_HIDEWINDOW);
                            if (flyoutForm != null && flyoutForm.Visible)
                            {
                                flyoutForm.Hide();
                            }
                        }
                        return; // Não executa SetWindowPos de exibição nem UpdateTraffic enquanto em descanso!
                    }
                    else
                    {
                        if (isScreensaverRunning || !this.Visible)
                        {
                            isScreensaverRunning = false;
                            this.Visible = true;
                            PositionWidget();
                            this.Invalidate();
                        }
                    }
                }

                tickCount++;
                if (tickCount >= 3)
                {
                    tickCount = 0;
                    UpdateTraffic();

                    bool isDarkNow = IsWindowsDarkTheme();
                    if (isDarkNow != lastThemeIsDark)
                    {
                        ApplySystemTheme();
                    }
                }

                if (this.Visible && !isSettingsDialogOpen && !isScreensaverRunning && !isSessionLocked)
                {
                    SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
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

            if (m.Msg == WM_DISPLAYCHANGE)
            {
                // Multi-monitor: re-posiciona e recalibra limites se a resolução ou disposição de telas mudar
                PositionWidget(false);
                if (flyoutForm != null && flyoutForm.Visible)
                {
                    PositionFlyout();
                }
            }

            if (m.Msg == WM_WINDOWPOSCHANGING)
            {
                if (hideOnScreensaver && isScreensaverRunning)
                {
                    // Não força HWND_TOPMOST se screensaver estiver ativo
                }
                else if (this.Visible && !isScreensaverRunning && !isSessionLocked)
                {
                    WINDOWPOS pos = (WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS));
                    pos.hwndInsertAfter = HWND_TOPMOST;
                    Marshal.StructureToPtr(pos, m.LParam, false);
                }
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
                        else currentLanguage = GetSystemDefaultLanguage();

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

                        object valHover = key.GetValue("ShowHoverFlyout");
                        if (valHover != null) showHoverFlyout = Convert.ToInt32(valHover) == 1;
                        else showHoverFlyout = true;

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
                    else
                    {
                        currentLanguage = GetSystemDefaultLanguage();
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
                        key.SetValue("ShowHoverFlyout", showHoverFlyout ? 1 : 0);
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

        // Método de Live Tuning: reflete alterações na barra de tarefas em tempo real
        public void ApplyLiveTuning(
            string lang,
            string speedFormat,
            string unit,
            bool arrowRight,
            bool locked,
            bool hideScreensaver,
            bool showHover,
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
            this.currentLanguage = lang;
            this.speedUnitFormat = speedFormat;
            this.unitMode = unit;
            this.arrowOnRight = arrowRight;
            this.isLocked = locked;
            this.hideOnScreensaver = hideScreensaver;
            this.showHoverFlyout = showHover;
            this.iconTemplate = tpl;
            this.customUpChar = upChar;
            this.customDownChar = downChar;

            this.fontFamily = fontFam;
            this.fontSize = fontSz;
            this.fontStyle = fontSt;

            this.textColorMode = txtColMode;
            this.textColorUnified = txtColUni;
            this.textColorUp = txtColUp;
            this.textColorDown = txtColDn;

            this.unitColorMode = unitColMode;
            this.unitColorUnified = unitColUni;
            this.unitColorUp = unitColUp;
            this.unitColorDown = unitColDn;

            this.arrowColorMode = arrColMode;
            this.arrowColorUnified = arrColUni;
            this.arrowColorUp = arrColUp;
            this.arrowColorDown = arrColDn;

            SetupFonts();
            UpdateCachedGdiResources();
            if (flyoutForm != null && flyoutForm.Visible)
            {
                flyoutForm.ApplyTheme(IsWindowsDarkTheme(), currentLanguage);
            }
            this.Invalidate();
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

            posX = Math.Max(targetScreen.Bounds.Left, Math.Min(posX, targetScreen.Bounds.Right - WIDGET_WIDTH));
            posY = Math.Max(targetScreen.Bounds.Top, Math.Min(posY, targetScreen.Bounds.Bottom - WIDGET_HEIGHT));

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

                        // Otimização zero CPU: Só invalida se o valor mudou
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
            contextMenu.Opening += (s, e) => { HideFlyout(); };
            contextMenu.Renderer = new Windows11MenuRenderer(isDark);
            contextMenu.ShowCheckMargin = true;
            contextMenu.ShowImageMargin = false;

            // 0. Configurações / Settings
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

            // 4. Idioma da Interface / Language
            ToolStripMenuItem mnuLang = new ToolStripMenuItem(isPt ? "Idioma" : "Language");
            ConfigureSubmenu(mnuLang);

            ToolStripMenuItem itmPt = new ToolStripMenuItem("Português (Brasil)");
            itmPt.Checked = isPt;
            itmPt.Click += (s, e) =>
            {
                currentLanguage = "pt";
                SaveSettings();
                BuildContextMenu();
                this.Invalidate();
            };

            ToolStripMenuItem itmEn = new ToolStripMenuItem("English (United States)");
            itmEn.Checked = !isPt;
            itmEn.Click += (s, e) =>
            {
                currentLanguage = "en";
                SaveSettings();
                BuildContextMenu();
                this.Invalidate();
            };

            mnuLang.DropDownItems.Add(itmPt);
            mnuLang.DropDownItems.Add(itmEn);
            contextMenu.Items.Add(mnuLang);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 5. Travar Posição
            ToolStripMenuItem mnuLock = new ToolStripMenuItem(isPt ? "Travar Posição" : "Lock Position");
            mnuLock.Checked = isLocked;
            mnuLock.Click += (s, e) =>
            {
                isLocked = !isLocked;
                mnuLock.Checked = isLocked;
                SaveSettings();
            };
            contextMenu.Items.Add(mnuLock);

            // 6. Iniciar com o Windows
            ToolStripMenuItem mnuAutoStart = new ToolStripMenuItem(isPt ? "Iniciar com o Windows" : "Start with Windows");
            mnuAutoStart.Checked = IsAutoStartEnabled();
            mnuAutoStart.Click += (s, e) =>
            {
                bool newState = !mnuAutoStart.Checked;
                ToggleAutoStart(newState);
                mnuAutoStart.Checked = newState;
            };
            contextMenu.Items.Add(mnuAutoStart);

            // 7. Redefinir Posição
            ToolStripMenuItem mnuReset = new ToolStripMenuItem(isPt ? "Redefinir Posição" : "Reset Position");
            mnuReset.Click += (s, e) => PositionWidget(true);
            contextMenu.Items.Add(mnuReset);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 8. Desenvolvedor / Developer
            ToolStripMenuItem mnuDev = new ToolStripMenuItem("Noé VFX (noevfx.com)");
            mnuDev.Click += (s, e) =>
            {
                try { Process.Start("https://noevfx.com"); } catch { }
            };
            contextMenu.Items.Add(mnuDev);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 9. Sair / Exit
            ToolStripMenuItem mnuExit = new ToolStripMenuItem(isPt ? "Sair" : "Exit");
            mnuExit.Click += (s, e) =>
            {
                try
                {
                    HideFlyout();
                    if (flyoutForm != null && !flyoutForm.IsDisposed)
                    {
                        flyoutForm.Dispose();
                        flyoutForm = null;
                    }
                    this.Close();
                }
                catch
                {
                    Environment.Exit(0);
                }
            };
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
            // Snapshot do estado original para cancelamento seguro
            string origLang = currentLanguage;
            string origFormat = speedUnitFormat;
            string origUnit = unitMode;
            bool origArrow = arrowOnRight;
            bool origLocked = isLocked;
            bool origHideSs = hideOnScreensaver;
            bool origFollow = followSystemTheme;
            string origTpl = iconTemplate;
            string origUp = customUpChar;
            string origDown = customDownChar;
            string origFontF = fontFamily;
            float origFontS = fontSize;
            FontStyle origFontSt = fontStyle;
            string origTm = textColorMode;
            Color origTu = textColorUnified;
            Color origTup = textColorUp;
            Color origTdn = textColorDown;
            string origUm = unitColorMode;
            Color origUu = unitColorUnified;
            Color origUup = unitColorUp;
            Color origUdn = unitColorDown;
            string origAm = arrowColorMode;
            Color origAu = arrowColorUnified;
            Color origAup = arrowColorUp;
            Color origAdn = arrowColorDown;
            bool origHover = showHoverFlyout;

            isSettingsDialogOpen = true;

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
                    showHoverFlyout,
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
                        SaveSettings();
                        BuildContextMenu();
                    }
                    else
                    {
                        // Se cancelar, restaura o snapshot original instantaneamente
                        ApplyLiveTuning(
                            origLang, origFormat, origUnit, origArrow, origLocked, origHideSs, origHover,
                            origTpl, origUp, origDown, origFontF, origFontS, origFontSt,
                            origTm, origTu, origTup, origTdn,
                            origUm, origUu, origUup, origUdn,
                            origAm, origAu, origAup, origAdn);
                    }
                }
            }
            finally
            {
                isSettingsDialogOpen = false;
                try { GC.Collect(2, GCCollectionMode.Optimized); } catch { }
            }
        }

        private void NetMonitorApp_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                HideFlyout();
                if (!isLocked)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);

                    // Validação multi-monitor pós-arrasto (nunca vaza para o monitor vizinho)
                    Screen scr = Screen.FromControl(this);
                    if (scr != null)
                    {
                        Rectangle b = scr.Bounds;
                        int clX = Math.Max(b.Left, Math.Min(this.Left, b.Right - WIDGET_WIDTH));
                        int clY = Math.Max(b.Top, Math.Min(this.Top, b.Bottom - WIDGET_HEIGHT));
                        if (clX != this.Left || clY != this.Top)
                        {
                            this.Location = new Point(clX, clY);
                        }
                    }
                    SaveSettings();
                }
            }
        }

        private void NetMonitorApp_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                HideFlyout();
                Screen scr = Screen.FromControl(this);
                if (scr == null) scr = Screen.PrimaryScreen;
                Rectangle b = scr.Bounds;

                Size pref = contextMenu.PreferredSize;
                int mnuW = pref.Width > 0 ? pref.Width : (contextMenu.Width > 0 ? contextMenu.Width : 220);
                int mnuH = pref.Height > 0 ? pref.Height : (contextMenu.Height > 0 ? contextMenu.Height : 260);

                // Ancorado diretamente sobre o widget na barra de tarefas
                int x = this.Left;
                if (x + mnuW > b.Right - 4) x = b.Right - mnuW - 4;
                if (x < b.Left + 4) x = b.Left + 4;

                int y = this.Top - mnuH - 2;
                if (y < b.Top + 4)
                {
                    y = this.Bottom + 2;
                }

                contextMenu.Show(x, y);
            }
        }

        private void OnWidgetMouseEnter(object sender, EventArgs e)
        {
            if (!showHoverFlyout || isSettingsDialogOpen || isScreensaverRunning || isSessionLocked) return;
            if (contextMenu != null && contextMenu.Visible) return;
            ShowFlyout();
        }

        private void OnWidgetMouseLeave(object sender, EventArgs e)
        {
            if (hoverCheckTimer != null && !hoverCheckTimer.Enabled)
            {
                hoverCheckTimer.Start();
            }
        }

        private void ShowFlyout()
        {
            if (!showHoverFlyout || isSettingsDialogOpen || isScreensaverRunning || isSessionLocked) return;

            if (flyoutForm == null || flyoutForm.IsDisposed)
            {
                flyoutForm = new NetworkTopProcessesFlyout();
                flyoutForm.MouseEnter += (s, e) => { /* Mantém aberto enquanto cursor estiver no card */ };
                flyoutForm.MouseLeave += (s, e) => { if (hoverCheckTimer != null && !hoverCheckTimer.Enabled) hoverCheckTimer.Start(); };
            }

            PositionFlyout();
            flyoutForm.ApplyTheme(IsWindowsDarkTheme(), currentLanguage);

            if (!flyoutForm.Visible)
            {
                flyoutForm.Show();
                SetWindowPos(flyoutForm.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }

            isHoverSampling = true;
            lastProcessIo.Clear();

            // Amostragem inicial imediata assíncrona
            ThreadPool.QueueUserWorkItem(DoProcessSampleWork);

            if (processSampleTimer == null)
            {
                processSampleTimer = new Timer();
                processSampleTimer.Interval = 1000;
                processSampleTimer.Tick += (s, e) =>
                {
                    if (isHoverSampling && flyoutForm != null && flyoutForm.Visible)
                    {
                        ThreadPool.QueueUserWorkItem(DoProcessSampleWork);
                    }
                    else
                    {
                        processSampleTimer.Stop();
                    }
                };
            }
            processSampleTimer.Start();

            if (hoverCheckTimer == null)
            {
                hoverCheckTimer = new Timer();
                hoverCheckTimer.Interval = 200;
                hoverCheckTimer.Tick += HoverCheckTimer_Tick;
            }
            hoverCheckTimer.Start();
        }

        private void HoverCheckTimer_Tick(object sender, EventArgs e)
        {
            Point cursor = Cursor.Position;
            bool overWidget = this.Visible && this.Bounds.Contains(cursor);
            bool overFlyout = (flyoutForm != null && flyoutForm.Visible && flyoutForm.Bounds.Contains(cursor));

            if (!overWidget && !overFlyout)
            {
                HideFlyout();
            }
        }

        private void HideFlyout()
        {
            isHoverSampling = false;
            if (processSampleTimer != null) processSampleTimer.Stop();
            if (hoverCheckTimer != null) hoverCheckTimer.Stop();

            if (flyoutForm != null && flyoutForm.Visible)
            {
                flyoutForm.Hide();
            }

            lastProcessIo.Clear();
            cachedProcessNames.Clear();
        }

        private void PositionFlyout()
        {
            if (flyoutForm == null) return;
            int flyoutW = flyoutForm.Width;
            int flyoutH = flyoutForm.Height;

            Screen scr = Screen.FromControl(this);
            if (scr == null) scr = Screen.PrimaryScreen;
            Rectangle wa = scr.WorkingArea;

            int desiredX = this.Left + (this.Width - flyoutW) / 2;
            int desiredY = this.Top - flyoutH - 8;

            int clampedX = Math.Max(wa.Left + 6, Math.Min(wa.Right - flyoutW - 6, desiredX));
            int clampedY = desiredY;

            if (clampedY < wa.Top + 6)
            {
                clampedY = this.Bottom + 8;
            }
            if (clampedY + flyoutH > wa.Bottom - 6)
            {
                clampedY = wa.Bottom - flyoutH - 6;
            }

            flyoutForm.Location = new Point(clampedX, clampedY);
        }

        private void DoProcessSampleWork(object state)
        {
            if (!isHoverSampling || flyoutForm == null || !flyoutForm.Visible) return;

            try
            {
                HashSet<int> activePids = GetActiveNetworkPids();
                if (activePids.Count == 0) return;

                // Agrupamento por software/aplicativo (soma tráfego de múltiplos workers e instâncias)
                Dictionary<string, ulong> appTraffic = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> appPrimaryPid = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (int pid in activePids)
                {
                    IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                    if (hProc != IntPtr.Zero)
                    {
                        try
                        {
                            IO_COUNTERS counters;
                            if (GetProcessIoCounters(hProc, out counters))
                            {
                                ulong total = counters.ReadTransferCount + counters.WriteTransferCount;
                                if (lastProcessIo.ContainsKey(pid))
                                {
                                    ulong last = lastProcessIo[pid];
                                    if (total >= last)
                                    {
                                        ulong delta = total - last;
                                        if (delta > 0)
                                        {
                                            string baseName = GetProcessName(pid);
                                            if (baseName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                                baseName = baseName.Substring(0, baseName.Length - 4);

                                            if (!appTraffic.ContainsKey(baseName))
                                            {
                                                appTraffic[baseName] = 0;
                                                appPrimaryPid[baseName] = pid;
                                            }
                                            appTraffic[baseName] += delta;
                                        }
                                    }
                                }
                                lastProcessIo[pid] = total;
                            }
                        }
                        finally
                        {
                            CloseHandle(hProc);
                        }
                    }
                }

                // Limpeza periódica de PIDs inativos de lastProcessIo
                if (lastProcessIo.Count > 100)
                {
                    List<int> toRemove = new List<int>();
                    foreach (var k in lastProcessIo.Keys)
                    {
                        if (!activePids.Contains(k)) toRemove.Add(k);
                    }
                    for (int i = 0; i < toRemove.Count; i++) lastProcessIo.Remove(toRemove[i]);
                }

                List<ProcessTrafficItem> groupedItems = new List<ProcessTrafficItem>();
                foreach (var kvp in appTraffic)
                {
                    groupedItems.Add(new ProcessTrafficItem
                    {
                        Pid = appPrimaryPid[kvp.Key],
                        Name = kvp.Key,
                        DeltaBytes = kvp.Value,
                        SpeedText = FormatSpeedForProcess(kvp.Value)
                    });
                }

                // Ordenação dinâmica em tempo real (maior consumo primeiro)
                groupedItems.Sort((a, b) => b.DeltaBytes.CompareTo(a.DeltaBytes));

                List<ProcessTrafficItem> top5 = new List<ProcessTrafficItem>();
                for (int i = 0; i < groupedItems.Count && i < 5; i++)
                {
                    top5.Add(groupedItems[i]);
                }

                if (flyoutForm != null && flyoutForm.Visible && flyoutForm.IsHandleCreated)
                {
                    flyoutForm.BeginInvoke((MethodInvoker)delegate
                    {
                        if (flyoutForm != null && flyoutForm.Visible)
                        {
                            flyoutForm.UpdateItems(top5);
                        }
                    });
                }
            }
            catch { }
        }

        private static HashSet<int> GetActiveNetworkPids()
        {
            HashSet<int> pids = new HashSet<int>();

            // 1. TCP IPv4
            int size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedTcpTable(buf, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr rowPtr = (IntPtr)((long)buf + 4);
                        int rowSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));
                        for (int i = 0; i < count; i++)
                        {
                            MIB_TCPROW_OWNER_PID row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_TCPROW_OWNER_PID));
                            if (row.owningPid > 4) pids.Add(row.owningPid);
                            rowPtr = (IntPtr)((long)rowPtr + rowSize);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            // 2. TCP IPv6
            size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0);
            if (size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedTcpTable(buf, ref size, false, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr rowPtr = (IntPtr)((long)buf + 4);
                        int rowSize = Marshal.SizeOf(typeof(MIB_TCP6ROW_OWNER_PID));
                        for (int i = 0; i < count; i++)
                        {
                            MIB_TCP6ROW_OWNER_PID row = (MIB_TCP6ROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_TCP6ROW_OWNER_PID));
                            if (row.owningPid > 4) pids.Add(row.owningPid);
                            rowPtr = (IntPtr)((long)rowPtr + rowSize);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            // 3. UDP IPv4
            size = 0;
            GetExtendedUdpTable(IntPtr.Zero, ref size, false, AF_INET, UDP_TABLE_OWNER_PID, 0);
            if (size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedUdpTable(buf, ref size, false, AF_INET, UDP_TABLE_OWNER_PID, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr rowPtr = (IntPtr)((long)buf + 4);
                        int rowSize = Marshal.SizeOf(typeof(MIB_UDPROW_OWNER_PID));
                        for (int i = 0; i < count; i++)
                        {
                            MIB_UDPROW_OWNER_PID row = (MIB_UDPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_UDPROW_OWNER_PID));
                            if (row.owningPid > 4) pids.Add(row.owningPid);
                            rowPtr = (IntPtr)((long)rowPtr + rowSize);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            // 4. UDP IPv6
            size = 0;
            GetExtendedUdpTable(IntPtr.Zero, ref size, false, AF_INET6, UDP_TABLE_OWNER_PID, 0);
            if (size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedUdpTable(buf, ref size, false, AF_INET6, UDP_TABLE_OWNER_PID, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr rowPtr = (IntPtr)((long)buf + 4);
                        int rowSize = Marshal.SizeOf(typeof(MIB_UDP6ROW_OWNER_PID));
                        for (int i = 0; i < count; i++)
                        {
                            MIB_UDP6ROW_OWNER_PID row = (MIB_UDP6ROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_UDP6ROW_OWNER_PID));
                            if (row.owningPid > 4) pids.Add(row.owningPid);
                            rowPtr = (IntPtr)((long)rowPtr + rowSize);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            return pids;
        }

        private string GetProcessName(int pid)
        {
            if (cachedProcessNames.ContainsKey(pid))
                return cachedProcessNames[pid];

            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc != IntPtr.Zero)
            {
                try
                {
                    StringBuilder sb = new StringBuilder(512);
                    int size = sb.Capacity;
                    if (QueryFullProcessImageName(hProc, 0, sb, ref size) != 0)
                    {
                        string fn = System.IO.Path.GetFileName(sb.ToString());
                        if (!string.IsNullOrEmpty(fn))
                        {
                            cachedProcessNames[pid] = fn;
                            return fn;
                        }
                    }
                }
                finally
                {
                    CloseHandle(hProc);
                }
            }

            try
            {
                string n = Process.GetProcessById(pid).ProcessName + ".exe";
                cachedProcessNames[pid] = n;
                return n;
            }
            catch
            {
                string fallback = "PID " + pid;
                cachedProcessNames[pid] = fallback;
                return fallback;
            }
        }

        private string FormatSpeedForProcess(ulong deltaBytes)
        {
            if (speedUnitFormat == "Bytes")
            {
                double bytes = deltaBytes;
                if (bytes >= 1073741824.0) return string.Format("{0:0.0} GB/s", bytes / 1073741824.0);
                if (bytes >= 1048576.0) return string.Format("{0:0.0} MB/s", bytes / 1048576.0);
                if (bytes >= 1024.0) return string.Format("{0:0} KB/s", bytes / 1024.0);
                return string.Format("{0} B/s", bytes);
            }
            else // Bits (Padrão)
            {
                double bits = deltaBytes * 8.0;
                if (bits >= 1000000000.0) return string.Format("{0:0.0} Gbps", bits / 1000000000.0);
                if (bits >= 1000000.0) return string.Format("{0:0.0} Mbps", bits / 1000000.0);
                if (bits >= 1000.0) return string.Format("{0:0} Kbps", bits / 1000.0);
                return string.Format("{0:0} bps", bits);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                if (updateTimer != null) { updateTimer.Stop(); updateTimer.Dispose(); }
                if (hoverCheckTimer != null) { hoverCheckTimer.Stop(); hoverCheckTimer.Dispose(); }
                if (processSampleTimer != null) { processSampleTimer.Stop(); processSampleTimer.Dispose(); }
                if (flyoutForm != null) { flyoutForm.Dispose(); flyoutForm = null; }
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

        private static Mutex singleInstanceMutex = null;

        [STAThread]
        public static void Main()
        {
            bool createdNew = false;
            try
            {
                singleInstanceMutex = new Mutex(true, "NetMonitor_SingleInstance_Mutex_EduardoNoe", out createdNew);
            }
            catch
            {
                createdNew = false;
            }

            if (!createdNew)
            {
                // Instância prévia já ativa: encerra para evitar duplicidade
                return;
            }

            try
            {
                SetProcessDpiAwareness(2);
            }
            catch
            {
                try { SetProcessDPIAware(); } catch { }
            }

            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => { };
                Application.ThreadException += (s, e) => { };
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new NetMonitorApp());
            }
            finally
            {
                if (singleInstanceMutex != null)
                {
                    try { singleInstanceMutex.ReleaseMutex(); } catch { }
                    try { singleInstanceMutex.Close(); } catch { }
                }
            }
        }
    }

    // ==============================================================================
    // CARD FLUTUANTE MODERNO WINDOWS 11: TOP 5 PROGRAMAS POR TRÁFEGO DE REDE
    // ==============================================================================
    public class ProcessTrafficItem
    {
        public int Pid;
        public string Name;
        public ulong DeltaBytes;
        public string SpeedText;
    }

    public class NetworkTopProcessesFlyout : Form
    {
        private List<ProcessTrafficItem> items = new List<ProcessTrafficItem>();
        private bool isDark = true;
        private string lang = "pt";
        private int cornerPref = 2; // DWMWCP_ROUND
        private object syncLock = new object();

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public NetworkTopProcessesFlyout()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(252, 172);
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                DwmSetWindowAttribute(this.Handle, 33, ref cornerPref, sizeof(int));
            }
            catch { }
        }

        public void ApplyTheme(bool dark, string language)
        {
            this.isDark = dark;
            this.lang = language;
            this.BackColor = dark ? Color.FromArgb(30, 30, 30) : Color.FromArgb(255, 255, 255);
            this.Invalidate();
        }

        public void UpdateItems(List<ProcessTrafficItem> newItems)
        {
            lock (syncLock)
            {
                items = newItems ?? new List<ProcessTrafficItem>();
            }
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            Color clrBorder = isDark ? Color.FromArgb(52, 52, 52) : Color.FromArgb(226, 226, 226);
            Color clrHeader = isDark ? Color.FromArgb(150, 150, 150) : Color.FromArgb(110, 110, 110);
            Color clrDiv = isDark ? Color.FromArgb(44, 44, 44) : Color.FromArgb(235, 235, 235);
            Color clrTextMain = isDark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(28, 28, 28);
            Color clrTextMuted = isDark ? Color.FromArgb(120, 120, 120) : Color.FromArgb(145, 145, 145);
            Color clrAccentSpeed = isDark ? Color.FromArgb(96, 205, 255) : Color.FromArgb(0, 103, 192);
            Color clrTrack = isDark ? Color.FromArgb(42, 42, 42) : Color.FromArgb(240, 240, 240);
            Color clrBar = isDark ? Color.FromArgb(0, 120, 212) : Color.FromArgb(0, 120, 212);
            Color clrBarTop = isDark ? Color.FromArgb(0, 188, 249) : Color.FromArgb(0, 90, 180);

            // Borda externa de 1px
            using (Pen pBorder = new Pen(clrBorder, 1.0f))
            {
                g.DrawRectangle(pBorder, 0, 0, w - 1, h - 1);
            }

            // Cabeçalho
            string headerText = (lang == "pt") ? "Top 5 Tráfego de Rede por App" : "Top 5 Network Usage by App";
            using (SolidBrush brDot = new SolidBrush(clrAccentSpeed))
            {
                g.FillEllipse(brDot, 14, 11, 6, 6);
            }

            using (Font fHead = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (SolidBrush brHead = new SolidBrush(clrHeader))
            {
                g.DrawString(headerText, fHead, brHead, 25, 8);
            }

            // Linha divisória
            using (Pen pDiv = new Pen(clrDiv, 1.0f))
            {
                g.DrawLine(pDiv, 14, 26, w - 14, 26);
            }

            List<ProcessTrafficItem> currentItems;
            lock (syncLock)
            {
                currentItems = new List<ProcessTrafficItem>(items);
            }

            if (currentItems.Count == 0)
            {
                string emptyText = (lang == "pt") ? "Aguardando atividade de rede..." : "Waiting for network activity...";
                using (Font fEmpty = new Font("Segoe UI", 8.0f, FontStyle.Italic))
                using (SolidBrush brEmpty = new SolidBrush(clrTextMuted))
                using (StringFormat sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(emptyText, fEmpty, brEmpty, new RectangleF(14, 30, w - 28, h - 36), sfC);
                }
                return;
            }

            ulong maxBytes = 1;
            for (int i = 0; i < currentItems.Count; i++)
            {
                if (currentItems[i].DeltaBytes > maxBytes)
                    maxBytes = currentItems[i].DeltaBytes;
            }

            int startY = 32;
            int rowH = 26;
            using (Font fRank = new Font("Segoe UI", 7.0f, FontStyle.Bold))
            using (Font fName = new Font("Segoe UI", 8.0f, FontStyle.Regular))
            using (Font fSpeed = new Font("Segoe UI", 8.0f, FontStyle.Bold))
            using (SolidBrush brRank = new SolidBrush(clrTextMuted))
            using (SolidBrush brName = new SolidBrush(clrTextMain))
            using (SolidBrush brSpeed = new SolidBrush(clrAccentSpeed))
            using (SolidBrush brTrack = new SolidBrush(clrTrack))
            using (SolidBrush brFillTop = new SolidBrush(clrBarTop))
            using (SolidBrush brFillSub = new SolidBrush(clrBar))
            using (StringFormat sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near })
            {
                for (int i = 0; i < currentItems.Count && i < 5; i++)
                {
                    ProcessTrafficItem item = currentItems[i];
                    int y = startY + (i * rowH);

                    // Ranking: 1, 2, 3, 4, 5
                    g.DrawString((i + 1).ToString(), fRank, brRank, 14, y);

                    // Nome do aplicativo
                    string displayName = item.Name;
                    if (displayName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        displayName = displayName.Substring(0, displayName.Length - 4);
                    if (displayName.Length > 20) displayName = displayName.Substring(0, 18) + "…";
                    g.DrawString(displayName, fName, brName, 28, y - 1);

                    // Velocidade à direita
                    g.DrawString(item.SpeedText, fSpeed, brSpeed, w - 14, y - 1, sfRight);

                    // Mini barra horizontal
                    int barX = 28;
                    int barY = y + 17;
                    int barMaxW = w - 42;
                    int barH = 2;

                    g.FillRectangle(brTrack, barX, barY, barMaxW, barH);

                    float ratio = (maxBytes > 0) ? (float)item.DeltaBytes / (float)maxBytes : 0f;
                    int fillW = Math.Max(3, (int)(ratio * barMaxW));
                    g.FillRectangle(i == 0 ? brFillTop : brFillSub, barX, barY, fillW, barH);
                }
            }
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
        public string SelectedLanguage { get; private set; }
        public string SelectedSpeedUnitFormat { get; private set; }
        public string SelectedUnitMode { get; private set; }
        public bool SelectedArrowOnRight { get; private set; }
        public bool SelectedIsLocked { get; private set; }
        public bool SelectedHideOnScreensaver { get; private set; }
        public bool SelectedShowHoverFlyout { get; private set; }
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
        private NetMonitorApp parentApp;
        private bool isDarkTheme;

        // Painéis com posicionamento absoluto independente (sem sobreposição)
        private Panel pnlSidebar;
        private Panel pnlMainContent;
        private Panel pnlBottom;

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
        private CheckBox chkHoverFlyout;

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

        // Presets de Cores
        private Button btnPresetCiano;
        private Button btnPresetClassico;
        private Button btnPresetPadrao;
        private Button btnPresetMono;
        private int currentPageIndex = 0;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Controles: Ícones
        private ComboBox cbTemplate;
        private TextBox txtUp;
        private TextBox txtDown;
        private Label lblUp;
        private Label lblDown;

        // Live Preview Box
        private Panel pnlLivePreview;
        private Label lblPrevTitle;
        private Label lblPrevDesc;
        private Button btnResetDefaults;
        private Button btnSave;
        private Button btnCancel;

        // Bloco do Desenvolvedor
        private Label lblDevTitle;
        private LinkLabel lnkDev;
        private LinkLabel lnkContact;

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
            NetMonitorApp parent,
            string lang,
            string speedFormat,
            string unit,
            bool arrowRight,
            bool locked,
            bool hideScreensaver,
            bool showHover,
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
            this.parentApp = parent;
            this.SelectedLanguage = lang;
            this.SelectedSpeedUnitFormat = speedFormat;
            this.SelectedUnitMode = unit;
            this.SelectedArrowOnRight = arrowRight;
            this.SelectedIsLocked = locked;
            this.SelectedHideOnScreensaver = hideScreensaver;
            this.SelectedShowHoverFlyout = showHover;
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

            try
            {
                SystemEvents.UserPreferenceChanged += OnThemeChanged;
            }
            catch { }
        }

        private void OnThemeChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            try
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        ApplyDynamicTheme(NetMonitorApp.IsWindowsDarkTheme());
                    });
                }
                else
                {
                    ApplyDynamicTheme(NetMonitorApp.IsWindowsDarkTheme());
                }
            }
            catch { }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00040000; // WS_EX_APPWINDOW: Garante presença explícita no alternador ALT+TAB do Windows
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateTitleBarTheme(isDarkTheme);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SETTINGCHANGE = 0x001A;
            const int WM_THEMECHANGED = 0x031A;
            if (m.Msg == WM_SETTINGCHANGE || m.Msg == WM_THEMECHANGED)
            {
                ApplyDynamicTheme(NetMonitorApp.IsWindowsDarkTheme());
            }
            base.WndProc(ref m);
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

        private void UpdateTitleBarTheme(bool darkNow)
        {
            try
            {
                int val = darkNow ? 1 : 0;
                DwmSetWindowAttribute(this.Handle, 20, ref val, sizeof(int));
            }
            catch { }
        }

        private void ApplyThemeRecursively(Control root, bool darkNow)
        {
            if (root == null) return;
            foreach (Control c in root.Controls)
            {
                Label lbl = c as Label;
                if (lbl != null)
                {
                    if (lbl == lblHeaderTitle)
                    {
                        lbl.ForeColor = clrTextMain;
                    }
                    else if (lbl == lblHeaderSubtitle)
                    {
                        lbl.ForeColor = clrTextSub;
                    }
                    else if (lbl == lblPrevTitle || lbl.Font.Bold)
                    {
                        lbl.ForeColor = clrAccent;
                    }
                    else if (lbl.Font.Size < 8.5f)
                    {
                        lbl.ForeColor = clrTextSub;
                    }
                    else if ((lbl == lblUp || lbl == lblDown) && cbTemplate != null && cbTemplate.SelectedIndex != 5)
                    {
                        lbl.ForeColor = clrTextSub;
                    }
                    else if (lbl == lblDevTitle)
                    {
                        lbl.ForeColor = clrTextSub;
                    }
                    else
                    {
                        lbl.ForeColor = clrTextMain;
                    }
                }
                else
                {
                    RadioButton rb = c as RadioButton;
                    if (rb != null)
                    {
                        rb.ForeColor = clrTextMain;
                    }
                    else
                    {
                        CheckBox cb = c as CheckBox;
                        if (cb != null)
                        {
                            cb.ForeColor = cb.Font.Bold ? clrAccent : clrTextMain;
                        }
                        else
                        {
                            ComboBox cmb = c as ComboBox;
                            if (cmb != null)
                            {
                                cmb.BackColor = clrInputBg;
                                cmb.ForeColor = clrTextMain;
                            }
                            else
                            {
                                TextBox tb = c as TextBox;
                                if (tb != null)
                                {
                                    tb.BackColor = clrInputBg;
                                    tb.ForeColor = clrTextMain;
                                }
                                else
                                {
                                    Panel p = c as Panel;
                                    if (p != null)
                                    {
                                        if (p.Height == 1)
                                        {
                                            p.BackColor = clrBorder;
                                        }
                                        else if (p == pnlPageGeral || p == pnlPageFonte || p == pnlPageCores || p == pnlPageIcones)
                                        {
                                            p.BackColor = clrCardBg;
                                        }
                                        else if (p == pnlLivePreview)
                                        {
                                            p.BackColor = darkNow ? NetMonitorApp.BG_COLOR_DARK : NetMonitorApp.BG_COLOR_LIGHT;
                                        }
                                    }
                                    else
                                    {
                                        Button btn = c as Button;
                                        if (btn != null)
                                        {
                                            string tagStr = btn.Tag as string;
                                            if (tagStr == "ColorSwatch")
                                            {
                                                btn.FlatAppearance.BorderColor = clrBorder;
                                            }
                                            else if (tagStr == "PresetBtn")
                                            {
                                                btn.BackColor = clrCardBg;
                                                btn.ForeColor = clrTextMain;
                                                btn.FlatAppearance.BorderColor = clrBorder;
                                            }
                                            else if (btn == btnCancel || btn == btnResetDefaults)
                                            {
                                                btn.BackColor = clrCardBg;
                                                btn.ForeColor = clrTextMain;
                                                btn.FlatAppearance.BorderColor = clrBorder;
                                            }
                                            else if (btn == btnSave)
                                            {
                                                btn.BackColor = clrAccent;
                                                btn.ForeColor = Color.White;
                                            }
                                        }
                                        else
                                        {
                                            LinkLabel ll = c as LinkLabel;
                                            if (ll != null)
                                            {
                                                if (ll == lnkDev) ll.LinkColor = clrAccent;
                                                else ll.LinkColor = clrTextSub;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (c.HasChildren)
                {
                    ApplyThemeRecursively(c, darkNow);
                }
            }
        }

        public void ApplyDynamicTheme(bool darkNow)
        {
            isDarkTheme = darkNow;
            SetupThemeColors();
            UpdateTitleBarTheme(darkNow);

            this.BackColor = clrFormBg;
            if (pnlSidebar != null) pnlSidebar.BackColor = clrSidebarBg;
            if (pnlMainContent != null) pnlMainContent.BackColor = clrFormBg;
            if (pnlBottom != null) pnlBottom.BackColor = clrFormBg;

            ApplyThemeRecursively(this, darkNow);
            SwitchPage(currentPageIndex);

            this.Invalidate(true);
        }

        // Envia as alterações ao vivo para o widget da barra de tarefas
        private void TriggerLiveTuning()
        {
            if (parentApp == null) return;
            parentApp.ApplyLiveTuning(
                SelectedLanguage,
                SelectedSpeedUnitFormat,
                SelectedUnitMode,
                SelectedArrowOnRight,
                SelectedIsLocked,
                SelectedHideOnScreensaver,
                SelectedShowHoverFlyout,
                SelectedIconTemplate,
                SelectedCustomUpChar,
                SelectedCustomDownChar,
                SelectedFontFamily,
                SelectedFontSize,
                SelectedFontStyle,
                SelectedTextColorMode,
                SelectedTextColorUnified,
                SelectedTextColorUp,
                SelectedTextColorDown,
                SelectedUnitColorMode,
                SelectedUnitColorUnified,
                SelectedUnitColorUp,
                SelectedUnitColorDown,
                SelectedArrowColorMode,
                SelectedArrowColorUnified,
                SelectedArrowColorUp,
                SelectedArrowColorDown
            );
        }

        private void InitializeUI()
        {
            bool isPt = SelectedLanguage == "pt";

            this.Text = isPt ? "Personalização do NetMonitor v1.0.0" : "NetMonitor v1.0.0 Settings";
            this.ClientSize = new Size(740, 540);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = true;
            try
            {
                if (parentApp != null && parentApp.Icon != null)
                {
                    this.Icon = parentApp.Icon;
                }
                else
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch { }
            this.BackColor = clrFormBg;
            this.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            // Posiciona centralizado e contido no monitor ativo (sem transbordar)
            Screen scr = (parentApp != null) ? Screen.FromControl(parentApp) : Screen.PrimaryScreen;
            Rectangle wa = scr.WorkingArea;
            int posX = Math.Max(wa.Left, Math.Min(wa.Right - this.Width, wa.Left + (wa.Width - this.Width) / 2));
            int posY = Math.Max(wa.Top, Math.Min(wa.Bottom - this.Height, wa.Top + (wa.Height - this.Height) / 2));
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(posX, posY);

            // ==============================================================
            // 1. SIDEBAR LATERAL ESQUERDA (180px de largura, sem sobreposição)
            // ==============================================================
            pnlSidebar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(180, 540),
                BackColor = clrSidebarBg,
                Padding = new Padding(12, 14, 12, 12)
            };
            this.Controls.Add(pnlSidebar);

            Label lblAppLogo = new Label
            {
                Text = "NetMonitor",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(16, 16)
            };
            pnlSidebar.Controls.Add(lblAppLogo);

            Label lblAppVer = new Label
            {
                Text = "v1.0.0 • Settings",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = clrAccent,
                AutoSize = true,
                Location = new Point(16, 38)
            };
            pnlSidebar.Controls.Add(lblAppVer);

            int btnY = 78;
            btnNavGeral = CreateSidebarButton(isPt ? "⚙️  Geral" : "⚙️  General", btnY, () => SwitchPage(0));
            btnNavFonte = CreateSidebarButton(isPt ? "🔤  Tipografia" : "🔤  Typography", btnY + 44, () => SwitchPage(1));
            btnNavCores = CreateSidebarButton(isPt ? "🎨  Cores" : "🎨  Colors", btnY + 88, () => SwitchPage(2));
            btnNavIcones = CreateSidebarButton(isPt ? "🔼  Ícones & Setas" : "🔼  Icons & Arrows", btnY + 132, () => SwitchPage(3));

            pnlSidebar.Controls.Add(btnNavGeral);
            pnlSidebar.Controls.Add(btnNavFonte);
            pnlSidebar.Controls.Add(btnNavCores);
            pnlSidebar.Controls.Add(btnNavIcones);

            // Bloco do Desenvolvedor (Noé VFX)
            Panel pnlDevDiv = new Panel
            {
                Location = new Point(14, 452),
                Size = new Size(152, 1),
                BackColor = clrBorder
            };
            pnlSidebar.Controls.Add(pnlDevDiv);

            lblDevTitle = new Label
            {
                Text = isPt ? "desenvolvido por" : "developed by",
                Font = new Font("Segoe UI", 7.0f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(14, 460),
                AutoSize = true,
                UseMnemonic = false
            };
            pnlSidebar.Controls.Add(lblDevTitle);

            lnkDev = new LinkLabel
            {
                Text = "Noé VFX | noevfx.com",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                LinkColor = clrAccent,
                ActiveLinkColor = clrAccent,
                VisitedLinkColor = clrAccent,
                Location = new Point(14, 477),
                AutoSize = true,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            lnkDev.LinkClicked += (s, e) => {
                try { Process.Start("https://noevfx.com"); } catch { }
            };
            pnlSidebar.Controls.Add(lnkDev);

            lnkContact = new LinkLabel
            {
                Text = "contato@noevfx.com",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                LinkColor = clrTextSub,
                ActiveLinkColor = clrAccent,
                VisitedLinkColor = clrTextSub,
                Location = new Point(14, 497),
                AutoSize = true,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            lnkContact.LinkClicked += (s, e) => {
                try { Process.Start("mailto:contato@noevfx.com"); } catch { }
            };
            pnlSidebar.Controls.Add(lnkContact);

            // ==============================================================
            // 2. PAINEL PRINCIPAL DIREITO (560px de largura, inicia em X=180)
            // ==============================================================
            pnlMainContent = new Panel
            {
                Location = new Point(180, 0),
                Size = new Size(560, 455),
                BackColor = clrFormBg
            };
            this.Controls.Add(pnlMainContent);

            lblHeaderTitle = new Label
            {
                Text = isPt ? "Preferências Gerais" : "General Preferences",
                Font = new Font("Segoe UI", 13.0f, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(24, 16),
                UseMnemonic = false
            };
            pnlMainContent.Controls.Add(lblHeaderTitle);

            lblHeaderSubtitle = new Label
            {
                Text = isPt ? "Configure idioma, medição, layout e proteção de exibição." : "Configure language, measurement, layout, and display protection.",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = clrTextSub,
                AutoSize = true,
                Location = new Point(25, 42)
            };
            pnlMainContent.Controls.Add(lblHeaderSubtitle);

            // Container de cartões
            int cardW = 512;
            int cardH = 368;
            int cardX = 24;
            int cardY = 72;

            pnlPageGeral = CreateCard(pnlMainContent, cardX, cardY, cardW, cardH);
            pnlPageFonte = CreateCard(pnlMainContent, cardX, cardY, cardW, cardH);
            pnlPageCores = CreateCard(pnlMainContent, cardX, cardY, cardW, cardH);
            pnlPageIcones = CreateCard(pnlMainContent, cardX, cardY, cardW, cardH);

            pnlPageFonte.Visible = false;
            pnlPageCores.Visible = false;
            pnlPageIcones.Visible = false;

            BuildPageGeral(pnlPageGeral, isPt);
            BuildPageFonte(pnlPageFonte, isPt);
            BuildPageCores(pnlPageCores, isPt);
            BuildPageIcones(pnlPageIcones, isPt);

            // ==============================================================
            // 3. BARRA INFERIOR (Inicia em X=180, Y=455, Height=85)
            // ==============================================================
            pnlBottom = new Panel
            {
                Location = new Point(180, 455),
                Size = new Size(560, 85),
                BackColor = clrFormBg
            };
            this.Controls.Add(pnlBottom);

            Panel pnlDiv = new Panel
            {
                Location = new Point(24, 0),
                Size = new Size(cardW, 1),
                BackColor = clrBorder
            };
            pnlBottom.Controls.Add(pnlDiv);

            pnlLivePreview = new Panel
            {
                Location = new Point(18, 18),
                Size = new Size(100, 42),
                BackColor = isDarkTheme ? NetMonitorApp.BG_COLOR_DARK : NetMonitorApp.BG_COLOR_LIGHT
            };
            pnlLivePreview.Paint += PnlLivePreview_Paint;
            pnlBottom.Controls.Add(pnlLivePreview);

            lblPrevTitle = new Label
            {
                Text = isPt ? "PRÉVIA AO VIVO" : "LIVE PREVIEW",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = clrAccent,
                Location = new Point(124, 20),
                Size = new Size(110, 16),
                AutoSize = false,
                UseMnemonic = false
            };
            pnlBottom.Controls.Add(lblPrevTitle);

            lblPrevDesc = new Label
            {
                Text = isPt ? "Tempo real" : "Real-time",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(124, 38),
                Size = new Size(110, 18),
                AutoSize = false,
                UseMnemonic = false
            };
            pnlBottom.Controls.Add(lblPrevDesc);

            btnResetDefaults = new Button
            {
                Text = isPt ? "Redefinir Padrões" : "Reset Defaults",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                Location = new Point(238, 20),
                Size = new Size(108, 36),
                BackColor = clrCardBg,
                ForeColor = clrTextMain,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnResetDefaults.FlatAppearance.BorderColor = clrBorder;
            btnResetDefaults.Click += (s, e) => ResetToDefaults(SelectedLanguage == "pt");
            pnlBottom.Controls.Add(btnResetDefaults);

            btnSave = new Button
            {
                Text = isPt ? "Salvar" : "Save",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                Location = new Point(354, 20),
                Size = new Size(88, 36),
                BackColor = clrAccent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            pnlBottom.Controls.Add(btnSave);

            btnCancel = new Button
            {
                Text = isPt ? "Cancelar" : "Cancel",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                Location = new Point(450, 20),
                Size = new Size(88, 36),
                BackColor = clrCardBg,
                ForeColor = clrTextMain,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseMnemonic = false
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
                Location = new Point(8, top),
                Size = new Size(164, 38),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                ForeColor = clrTextMain,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private void SwitchPage(int index)
        {
            currentPageIndex = index;
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
        private void BuildPageGeral(Panel card, bool isPt)
        {
            // 1. Idioma
            CreateLabel(card, isPt ? "Idioma da Interface:" : "Interface Language:", 20, 20);
            cbLang = CreateComboBox(card, 180, 18, 310);
            cbLang.Items.AddRange(new object[] { "Português (Brasil)", "English (United States)" });
            cbLang.SelectedIndex = SelectedLanguage == "pt" ? 0 : 1;
            cbLang.SelectedIndexChanged += (s, e) =>
            {
                SelectedLanguage = (cbLang.SelectedIndex == 0) ? "pt" : "en";
                UpdateLanguageTexts();
                TriggerLiveTuning();
            };

            // 2. Formato de Medição (Bits vs Bytes)
            CreateLabel(card, isPt ? "Formato de Medição:" : "Measurement Format:", 20, 58);
            cbFormat = CreateComboBox(card, 180, 56, 310);
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
                TriggerLiveTuning();
            };

            // 3. Escala da Unidade
            CreateLabel(card, isPt ? "Escala da Unidade:" : "Unit Scale:", 20, 96);
            cbUnits = CreateComboBox(card, 180, 94, 310);
            UpdateUnitComboItems(isPt);
            cbUnits.SelectedIndexChanged += (s, e) =>
            {
                if (cbUnits.SelectedIndex == 0) SelectedUnitMode = "Auto";
                else SelectedUnitMode = cbUnits.SelectedItem.ToString();
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            };

            // Divisor 1
            Panel div = new Panel { Location = new Point(20, 136), Size = new Size(card.Width - 40, 1), BackColor = clrBorder };
            card.Controls.Add(div);

            // 4. Posição das Setas
            CreateLabel(card, isPt ? "Posição das Setas:" : "Arrow Position:", 20, 150);

            rbRight = new RadioButton
            {
                Text = isPt ? "À Direita (823 Kbps ▲) [Padrão]" : "On Right (823 Kbps ▲) [Default]",
                Location = new Point(180, 148),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedArrowOnRight
            };
            rbLeft = new RadioButton
            {
                Text = isPt ? "À Esquerda (▲ 823 Kbps)" : "On Left (▲ 823 Kbps)",
                Location = new Point(180, 174),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = !SelectedArrowOnRight
            };
            rbRight.CheckedChanged += (s, e) =>
            {
                SelectedArrowOnRight = rbRight.Checked;
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            };
            card.Controls.Add(rbRight);
            card.Controls.Add(rbLeft);

            // Divisor 2
            Panel div2 = new Panel { Location = new Point(20, 210), Size = new Size(card.Width - 40, 1), BackColor = clrBorder };
            card.Controls.Add(div2);

            // 5. Opções de Sistema
            chkLock = new CheckBox
            {
                Text = isPt ? "Travar posição na barra de tarefas" : "Lock position on taskbar",
                Location = new Point(20, 224),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedIsLocked
            };
            chkLock.CheckedChanged += (s, e) => { SelectedIsLocked = chkLock.Checked; TriggerLiveTuning(); };
            card.Controls.Add(chkLock);

            chkScreensaver = new CheckBox
            {
                Text = isPt ? "Ocultar automaticamente no Screensaver e Tela de Bloqueio" : "Auto-hide on Screensaver and Lock Screen",
                Location = new Point(20, 254),
                AutoSize = true,
                ForeColor = clrAccent,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                Checked = SelectedHideOnScreensaver
            };
            chkScreensaver.CheckedChanged += (s, e) => { SelectedHideOnScreensaver = chkScreensaver.Checked; TriggerLiveTuning(); };
            card.Controls.Add(chkScreensaver);

            Label lblHintSs = new Label
            {
                Text = isPt ? "Evita que o monitor de rede fique visível por cima do protetor de tela." : "Prevents the network monitor from drawing over the screensaver.",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(42, 278),
                AutoSize = true
            };
            card.Controls.Add(lblHintSs);

            chkHoverFlyout = new CheckBox
            {
                Text = isPt ? "Exibir Top 5 programas ao passar o mouse (Hover)" : "Show Top 5 apps on mouse hover (Flyout)",
                Location = new Point(20, 304),
                AutoSize = true,
                ForeColor = clrAccent,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                Checked = SelectedShowHoverFlyout
            };
            chkHoverFlyout.CheckedChanged += (s, e) => { SelectedShowHoverFlyout = chkHoverFlyout.Checked; TriggerLiveTuning(); };
            card.Controls.Add(chkHoverFlyout);

            Label lblHintHover = new Label
            {
                Text = isPt ? "Card moderno e dinâmico com os 5 processos que mais consomem banda ao vivo." : "Dynamic modern card showing top 5 bandwidth consuming apps live.",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(42, 328),
                AutoSize = true
            };
            card.Controls.Add(lblHintHover);
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

        private void UpdateLanguageTexts()
        {
            bool isPt = (SelectedLanguage == "pt");
            this.Text = isPt ? "Personalização do NetMonitor v1.0.0" : "NetMonitor v1.0.0 Settings";
            btnNavGeral.Text = isPt ? "⚙️  Geral" : "⚙️  General";
            btnNavFonte.Text = isPt ? "🔤  Tipografia" : "🔤  Typography";
            btnNavCores.Text = isPt ? "🎨  Cores" : "🎨  Colors";
            btnNavIcones.Text = isPt ? "🔼  Ícones & Setas" : "🔼  Icons & Arrows";
            btnSave.Text = isPt ? "Salvar" : "Save";
            btnCancel.Text = isPt ? "Cancelar" : "Cancel";
            if (btnResetDefaults != null) btnResetDefaults.Text = isPt ? "Redefinir Padrões" : "Reset Defaults";
            if (lblDevTitle != null) lblDevTitle.Text = isPt ? "desenvolvido por" : "developed by";
            lblPrevTitle.Text = isPt ? "PRÉVIA AO VIVO" : "LIVE PREVIEW";
            lblPrevDesc.Text = isPt ? "Tempo real" : "Real-time";
            if (btnPresetCiano != null) btnPresetCiano.Text = isPt ? "Ciano & Verde" : "Cyan & Green";
            if (btnPresetClassico != null) btnPresetClassico.Text = isPt ? "Clássico Rede" : "Classic Net";
            if (btnPresetPadrao != null) btnPresetPadrao.Text = isPt ? "Padrão Win11" : "Win11 Default";
            if (btnPresetMono != null) btnPresetMono.Text = isPt ? "Monocromático" : "Monochrome";
            SwitchPage(pnlPageGeral.Visible ? 0 : pnlPageFonte.Visible ? 1 : pnlPageCores.Visible ? 2 : 3);
        }

        private void ResetToDefaults(bool isPt)
        {
            // 1. Geral
            SelectedLanguage = "pt";
            if (cbLang != null) cbLang.SelectedIndex = 0;

            SelectedSpeedUnitFormat = "Bits";
            if (cbFormat != null) cbFormat.SelectedIndex = 0;

            SelectedUnitMode = "Auto";
            if (cbUnits != null) cbUnits.SelectedIndex = 0;

            SelectedArrowOnRight = true;
            if (rbRight != null) rbRight.Checked = true;
            if (rbLeft != null) rbLeft.Checked = false;

            SelectedIsLocked = false;
            if (chkLock != null) chkLock.Checked = false;

            SelectedHideOnScreensaver = true;
            if (chkScreensaver != null) chkScreensaver.Checked = true;

            SelectedShowHoverFlyout = true;
            if (chkHoverFlyout != null) chkHoverFlyout.Checked = true;

            // 2. Tipografia
            SelectedFontFamily = "Segoe UI Variable Display";
            if (cbFontFamily != null)
            {
                int fIdx = 0;
                for (int i = 0; i < cbFontFamily.Items.Count; i++)
                {
                    if (string.Equals(cbFontFamily.Items[i].ToString(), "Segoe UI Variable Display", StringComparison.OrdinalIgnoreCase))
                    {
                        fIdx = i;
                        break;
                    }
                }
                cbFontFamily.SelectedIndex = fIdx;
            }

            SelectedFontSize = 9.5f;
            if (cbFontSize != null) cbFontSize.SelectedIndex = 3;

            SelectedFontStyle = FontStyle.Bold;
            if (cbFontStyle != null) cbFontStyle.SelectedIndex = 1;

            // 3. Cores
            SelectedTextColorMode = "Unified";
            SelectedTextColorUnified = Color.FromArgb(255, 255, 255);
            SelectedTextColorUp = Color.FromArgb(255, 255, 255);
            SelectedTextColorDown = Color.FromArgb(255, 255, 255);
            UpdateColorButtonDisplay(btnColorTextBoth, isPt ? "Ambos" : "Both", SelectedTextColorUnified);
            UpdateColorButtonDisplay(btnColorTextUp, "Upload ▲", SelectedTextColorUp);
            UpdateColorButtonDisplay(btnColorTextDown, "Download ▼", SelectedTextColorDown);
            if (rbTextUnified != null) rbTextUnified.Checked = true;
            if (rbTextIndividual != null) rbTextIndividual.Checked = false;

            SelectedUnitColorMode = "Inherit";
            SelectedUnitColorUnified = Color.FromArgb(190, 190, 190);
            SelectedUnitColorUp = Color.FromArgb(190, 190, 190);
            SelectedUnitColorDown = Color.FromArgb(190, 190, 190);
            UpdateColorButtonDisplay(btnColorUnitBoth, isPt ? "Unidade" : "Unit", SelectedUnitColorUnified);
            UpdateColorButtonDisplay(btnColorUnitUp, isPt ? "Unidade Up" : "Unit Up", SelectedUnitColorUp);
            UpdateColorButtonDisplay(btnColorUnitDown, isPt ? "Unidade Down" : "Unit Down", SelectedUnitColorDown);
            if (rbUnitInherit != null) rbUnitInherit.Checked = true;
            if (rbUnitUnified != null) rbUnitUnified.Checked = false;
            if (rbUnitIndividual != null) rbUnitIndividual.Checked = false;

            SelectedArrowColorMode = "Unified";
            SelectedArrowColorUnified = Color.FromArgb(235, 235, 235);
            SelectedArrowColorUp = Color.FromArgb(235, 235, 235);
            SelectedArrowColorDown = Color.FromArgb(235, 235, 235);
            UpdateColorButtonDisplay(btnColorArrowBoth, isPt ? "Ambas" : "Both", SelectedArrowColorUnified);
            UpdateColorButtonDisplay(btnColorArrowUp, "Seta Up ▲", SelectedArrowColorUp);
            UpdateColorButtonDisplay(btnColorArrowDown, "Seta Down ▼", SelectedArrowColorDown);
            if (rbArrowUnified != null) rbArrowUnified.Checked = true;
            if (rbArrowIndividual != null) rbArrowIndividual.Checked = false;

            UpdateColorControlsState();

            // 4. Ícones & Setas
            SelectedIconTemplate = "VectorTriangle";
            if (cbTemplate != null) cbTemplate.SelectedIndex = 0;

            SelectedCustomUpChar = "▲";
            SelectedCustomDownChar = "▼";
            if (txtUp != null) txtUp.Text = "▲";
            if (txtDown != null) txtDown.Text = "▼";
            if (lblUp != null) { lblUp.Enabled = true; lblUp.ForeColor = clrTextSub; }
            if (txtUp != null) txtUp.Enabled = false;
            if (lblDown != null) { lblDown.Enabled = true; lblDown.ForeColor = clrTextSub; }
            if (txtDown != null) txtDown.Enabled = false;

            UpdateLanguageTexts();
            if (pnlLivePreview != null) pnlLivePreview.Invalidate();
            TriggerLiveTuning();
        }

        // ==============================================================================
        // PÁGINA 2: TIPOGRAFIA
        // ==============================================================================
        private void BuildPageFonte(Panel card, bool isPt)
        {
            // 1. Família da Fonte
            CreateLabel(card, isPt ? "Família da Fonte:" : "Font Family:", 20, 24);
            cbFontFamily = CreateComboBox(card, 180, 22, 310);

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
                TriggerLiveTuning();
            };

            // 2. Tamanho da Fonte
            CreateLabel(card, isPt ? "Tamanho da Fonte:" : "Font Size:", 20, 72);
            cbFontSize = CreateComboBox(card, 180, 70, 310);
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
                TriggerLiveTuning();
            };

            // 3. Estilo da Fonte
            CreateLabel(card, isPt ? "Estilo da Fonte:" : "Font Style:", 20, 120);
            cbFontStyle = CreateComboBox(card, 180, 118, 310);
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
                TriggerLiveTuning();
            };

            // Divisor
            Panel div = new Panel { Location = new Point(20, 170), Size = new Size(card.Width - 40, 1), BackColor = clrBorder };
            card.Controls.Add(div);

            Label lblTip = new Label
            {
                Text = isPt ?
                    "Dica de Nitidez: A fonte 'Segoe UI Variable Display' a 9.5 pt Negrito oferece contraste e legibilidade ideais na barra de tarefas do Windows 11, adaptando automaticamente o espaçamento das unidades." :
                    "Sharpness Tip: 'Segoe UI Variable Display' at 9.5 pt Bold offers ideal contrast and readability on Windows 11 taskbar.",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(20, 188),
                Size = new Size(card.Width - 40, 90)
            };
            card.Controls.Add(lblTip);
        }

        // ==============================================================================
        // PÁGINA 3: CORES
        // ==============================================================================
        private void BuildPageCores(Panel card, bool isPt)
        {
            // -------------------------------------------------------------
            // Seção 1: Números da Velocidade
            // -------------------------------------------------------------
            Label lblSecNum = new Label
            {
                Text = isPt ? "1. Números da Velocidade:" : "1. Speed Numbers:",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = clrAccent,
                Location = new Point(18, 14),
                AutoSize = true
            };
            card.Controls.Add(lblSecNum);

            Panel pnlGrpText = new Panel { Location = new Point(205, 8), Size = new Size(295, 26), BackColor = Color.Transparent };
            rbTextUnified = new RadioButton
            {
                Text = isPt ? "Unificado" : "Unified",
                Location = new Point(5, 4),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedTextColorMode == "Unified"
            };
            rbTextIndividual = new RadioButton
            {
                Text = isPt ? "Individual" : "Individual",
                Location = new Point(105, 4),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedTextColorMode == "Individual"
            };
            pnlGrpText.Controls.Add(rbTextUnified);
            pnlGrpText.Controls.Add(rbTextIndividual);
            card.Controls.Add(pnlGrpText);

            btnColorTextBoth = CreateColorButton(card, isPt ? "Ambos" : "Both", 18, 38, SelectedTextColorUnified, (col) => {
                SelectedTextColorUnified = col;
                btnColorTextBoth.BackColor = col;
                btnColorTextBoth.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            });

            btnColorTextUp = CreateColorButton(card, "Upload ▲", 180, 38, SelectedTextColorUp, (col) => {
                SelectedTextColorUp = col;
                btnColorTextUp.BackColor = col;
                btnColorTextUp.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            });

            btnColorTextDown = CreateColorButton(card, "Download ▼", 342, 38, SelectedTextColorDown, (col) => {
                SelectedTextColorDown = col;
                btnColorTextDown.BackColor = col;
                btnColorTextDown.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            });

            rbTextUnified.CheckedChanged += (s, e) => {
                SelectedTextColorMode = rbTextIndividual.Checked ? "Individual" : "Unified";
                UpdateColorControlsState();
                TriggerLiveTuning();
            };
            rbTextIndividual.CheckedChanged += (s, e) => {
                SelectedTextColorMode = rbTextIndividual.Checked ? "Individual" : "Unified";
                UpdateColorControlsState();
                TriggerLiveTuning();
            };

            // Divisor 1
            Panel div1 = new Panel { Location = new Point(18, 88), Size = new Size(card.Width - 36, 1), BackColor = clrBorder };
            card.Controls.Add(div1);

            // -------------------------------------------------------------
            // Seção 2: Unidade de Medição (Kbps / MB/s)
            // -------------------------------------------------------------
            Label lblSecUnit = new Label
            {
                Text = isPt ? "2. Unidade (Kbps/MB/s):" : "2. Unit (Kbps/MB/s):",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = clrAccent,
                Location = new Point(18, 100),
                AutoSize = true
            };
            card.Controls.Add(lblSecUnit);

            Panel pnlGrpUnit = new Panel { Location = new Point(185, 94), Size = new Size(315, 26), BackColor = Color.Transparent };
            rbUnitInherit = new RadioButton
            {
                Text = isPt ? "Igual Números" : "Match Numbers",
                Location = new Point(5, 4),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedUnitColorMode == "Inherit"
            };
            rbUnitUnified = new RadioButton
            {
                Text = isPt ? "Unificado" : "Unified",
                Location = new Point(125, 4),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedUnitColorMode == "Unified"
            };
            rbUnitIndividual = new RadioButton
            {
                Text = isPt ? "Individual" : "Individual",
                Location = new Point(215, 4),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedUnitColorMode == "Individual"
            };
            pnlGrpUnit.Controls.Add(rbUnitInherit);
            pnlGrpUnit.Controls.Add(rbUnitUnified);
            pnlGrpUnit.Controls.Add(rbUnitIndividual);
            card.Controls.Add(pnlGrpUnit);

            btnColorUnitBoth = CreateColorButton(card, isPt ? "Unidade" : "Unit", 18, 124, SelectedUnitColorUnified, (col) => {
                SelectedUnitColorUnified = col;
                btnColorUnitBoth.BackColor = col;
                btnColorUnitBoth.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            });

            btnColorUnitUp = CreateColorButton(card, isPt ? "Unidade Up" : "Unit Up", 180, 124, SelectedUnitColorUp, (col) => {
                SelectedUnitColorUp = col;
                btnColorUnitUp.BackColor = col;
                btnColorUnitUp.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            });

            btnColorUnitDown = CreateColorButton(card, isPt ? "Unidade Down" : "Unit Down", 342, 124, SelectedUnitColorDown, (col) => {
                SelectedUnitColorDown = col;
                btnColorUnitDown.BackColor = col;
                btnColorUnitDown.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            });

            rbUnitInherit.CheckedChanged += (s, e) => {
                if (rbUnitIndividual.Checked) SelectedUnitColorMode = "Individual";
                else if (rbUnitUnified.Checked) SelectedUnitColorMode = "Unified";
                else SelectedUnitColorMode = "Inherit";
                UpdateColorControlsState();
                TriggerLiveTuning();
            };
            rbUnitUnified.CheckedChanged += (s, e) => {
                if (rbUnitIndividual.Checked) SelectedUnitColorMode = "Individual";
                else if (rbUnitUnified.Checked) SelectedUnitColorMode = "Unified";
                else SelectedUnitColorMode = "Inherit";
                UpdateColorControlsState();
                TriggerLiveTuning();
            };
            rbUnitIndividual.CheckedChanged += (s, e) => {
                if (rbUnitIndividual.Checked) SelectedUnitColorMode = "Individual";
                else if (rbUnitUnified.Checked) SelectedUnitColorMode = "Unified";
                else SelectedUnitColorMode = "Inherit";
                UpdateColorControlsState();
                TriggerLiveTuning();
            };

            // Divisor 2
            Panel div2 = new Panel { Location = new Point(18, 174), Size = new Size(card.Width - 36, 1), BackColor = clrBorder };
            card.Controls.Add(div2);

            // -------------------------------------------------------------
            // Seção 3: Setas de Tráfego
            // -------------------------------------------------------------
            Label lblSecArr = new Label
            {
                Text = isPt ? "3. Setas de Tráfego:" : "3. Traffic Arrows:",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = clrAccent,
                Location = new Point(18, 186),
                AutoSize = true
            };
            card.Controls.Add(lblSecArr);

            Panel pnlGrpArrow = new Panel { Location = new Point(205, 180), Size = new Size(295, 26), BackColor = Color.Transparent };
            rbArrowUnified = new RadioButton
            {
                Text = isPt ? "Unificado" : "Unified",
                Location = new Point(5, 4),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedArrowColorMode == "Unified"
            };
            rbArrowIndividual = new RadioButton
            {
                Text = isPt ? "Individual" : "Individual",
                Location = new Point(105, 4),
                AutoSize = true,
                ForeColor = clrTextMain,
                Checked = SelectedArrowColorMode == "Individual"
            };
            pnlGrpArrow.Controls.Add(rbArrowUnified);
            pnlGrpArrow.Controls.Add(rbArrowIndividual);
            card.Controls.Add(pnlGrpArrow);

            btnColorArrowBoth = CreateColorButton(card, isPt ? "Ambas" : "Both", 18, 210, SelectedArrowColorUnified, (col) => {
                SelectedArrowColorUnified = col;
                btnColorArrowBoth.BackColor = col;
                btnColorArrowBoth.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            });

            btnColorArrowUp = CreateColorButton(card, "Seta Up ▲", 180, 210, SelectedArrowColorUp, (col) => {
                SelectedArrowColorUp = col;
                btnColorArrowUp.BackColor = col;
                btnColorArrowUp.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            });

            btnColorArrowDown = CreateColorButton(card, "Seta Down ▼", 342, 210, SelectedArrowColorDown, (col) => {
                SelectedArrowColorDown = col;
                btnColorArrowDown.BackColor = col;
                btnColorArrowDown.ForeColor = GetContrastTextColor(col);
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            });

            rbArrowUnified.CheckedChanged += (s, e) => {
                SelectedArrowColorMode = rbArrowIndividual.Checked ? "Individual" : "Unified";
                UpdateColorControlsState();
                TriggerLiveTuning();
            };
            rbArrowIndividual.CheckedChanged += (s, e) => {
                SelectedArrowColorMode = rbArrowIndividual.Checked ? "Individual" : "Unified";
                UpdateColorControlsState();
                TriggerLiveTuning();
            };

            // Divisor 3
            Panel div3 = new Panel { Location = new Point(18, 256), Size = new Size(card.Width - 36, 1), BackColor = clrBorder };
            card.Controls.Add(div3);

            // -------------------------------------------------------------
            // Seção 4: Presets Rápidos
            // -------------------------------------------------------------
            Label lblSecPresets = new Label
            {
                Text = isPt ? "4. Presets Rápidos:" : "4. Quick Presets:",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = clrAccent,
                Location = new Point(18, 266),
                AutoSize = true
            };
            card.Controls.Add(lblSecPresets);

            btnPresetCiano = new Button
            {
                Text = isPt ? "Ciano & Verde" : "Cyan & Green",
                Location = new Point(18, 290),
                Size = new Size(112, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                BackColor = clrCardBg,
                ForeColor = clrTextMain,
                Cursor = Cursors.Hand,
                Tag = "PresetBtn"
            };
            btnPresetCiano.FlatAppearance.BorderColor = clrBorder;
            btnPresetCiano.Click += (s, e) => ApplyPreset("CianoVerde", isPt);
            card.Controls.Add(btnPresetCiano);

            btnPresetClassico = new Button
            {
                Text = isPt ? "Clássico Rede" : "Classic Net",
                Location = new Point(138, 290),
                Size = new Size(112, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                BackColor = clrCardBg,
                ForeColor = clrTextMain,
                Cursor = Cursors.Hand,
                Tag = "PresetBtn"
            };
            btnPresetClassico.FlatAppearance.BorderColor = clrBorder;
            btnPresetClassico.Click += (s, e) => ApplyPreset("ClassicoRede", isPt);
            card.Controls.Add(btnPresetClassico);

            btnPresetPadrao = new Button
            {
                Text = isPt ? "Padrão Win11" : "Win11 Default",
                Location = new Point(258, 290),
                Size = new Size(112, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                BackColor = clrCardBg,
                ForeColor = clrTextMain,
                Cursor = Cursors.Hand,
                Tag = "PresetBtn"
            };
            btnPresetPadrao.FlatAppearance.BorderColor = clrBorder;
            btnPresetPadrao.Click += (s, e) => ApplyPreset("PadraoWin11", isPt);
            card.Controls.Add(btnPresetPadrao);

            btnPresetMono = new Button
            {
                Text = isPt ? "Monocromático" : "Monochrome",
                Location = new Point(378, 290),
                Size = new Size(112, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                BackColor = clrCardBg,
                ForeColor = clrTextMain,
                Cursor = Cursors.Hand,
                Tag = "PresetBtn"
            };
            btnPresetMono.FlatAppearance.BorderColor = clrBorder;
            btnPresetMono.Click += (s, e) => ApplyPreset("Monocromatico", isPt);
            card.Controls.Add(btnPresetMono);

            Label lblColorTip = new Label
            {
                Text = isPt ?
                    "Clique em um preset acima para aplicar instantaneamente, ou personalize nos botões." :
                    "Click a preset above to apply instantly, or customize using the buttons above.",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(18, 332),
                AutoSize = true
            };
            card.Controls.Add(lblColorTip);

            UpdateColorControlsState();
        }

        private void UpdateColorButtonDisplay(Button btn, string title, Color col)
        {
            if (btn == null) return;
            btn.BackColor = col;
            btn.ForeColor = GetContrastTextColor(col);
            btn.Text = string.Format("{0}\n{1}", title, NetMonitorApp.ColorToHex(col));
        }

        private void ApplyPreset(string presetName, bool isPt)
        {
            if (presetName == "CianoVerde")
            {
                rbTextIndividual.Checked = true;
                SelectedTextColorMode = "Individual";
                SelectedTextColorUp = ColorTranslator.FromHtml("#00E5FF");
                SelectedTextColorDown = ColorTranslator.FromHtml("#00E676");
                UpdateColorButtonDisplay(btnColorTextUp, "Upload ▲", SelectedTextColorUp);
                UpdateColorButtonDisplay(btnColorTextDown, "Download ▼", SelectedTextColorDown);

                rbArrowIndividual.Checked = true;
                SelectedArrowColorMode = "Individual";
                SelectedArrowColorUp = SelectedTextColorUp;
                SelectedArrowColorDown = SelectedTextColorDown;
                UpdateColorButtonDisplay(btnColorArrowUp, "Seta Up ▲", SelectedArrowColorUp);
                UpdateColorButtonDisplay(btnColorArrowDown, "Seta Down ▼", SelectedArrowColorDown);

                rbUnitInherit.Checked = true;
                SelectedUnitColorMode = "Inherit";
            }
            else if (presetName == "ClassicoRede")
            {
                rbTextIndividual.Checked = true;
                SelectedTextColorMode = "Individual";
                SelectedTextColorUp = ColorTranslator.FromHtml("#00E676");
                SelectedTextColorDown = ColorTranslator.FromHtml("#FF5252");
                UpdateColorButtonDisplay(btnColorTextUp, "Upload ▲", SelectedTextColorUp);
                UpdateColorButtonDisplay(btnColorTextDown, "Download ▼", SelectedTextColorDown);

                rbArrowIndividual.Checked = true;
                SelectedArrowColorMode = "Individual";
                SelectedArrowColorUp = SelectedTextColorUp;
                SelectedArrowColorDown = SelectedTextColorDown;
                UpdateColorButtonDisplay(btnColorArrowUp, "Seta Up ▲", SelectedArrowColorUp);
                UpdateColorButtonDisplay(btnColorArrowDown, "Seta Down ▼", SelectedArrowColorDown);

                rbUnitInherit.Checked = true;
                SelectedUnitColorMode = "Inherit";
            }
            else if (presetName == "PadraoWin11")
            {
                rbTextUnified.Checked = true;
                SelectedTextColorMode = "Unified";
                SelectedTextColorUnified = Color.FromArgb(255, 255, 255);
                UpdateColorButtonDisplay(btnColorTextBoth, isPt ? "Ambos" : "Both", SelectedTextColorUnified);

                rbArrowUnified.Checked = true;
                SelectedArrowColorMode = "Unified";
                SelectedArrowColorUnified = Color.FromArgb(235, 235, 235);
                UpdateColorButtonDisplay(btnColorArrowBoth, isPt ? "Ambas" : "Both", SelectedArrowColorUnified);

                rbUnitInherit.Checked = true;
                SelectedUnitColorMode = "Inherit";
            }
            else if (presetName == "Monocromatico")
            {
                rbTextIndividual.Checked = true;
                SelectedTextColorMode = "Individual";
                SelectedTextColorUp = Color.FromArgb(255, 255, 255);
                SelectedTextColorDown = Color.FromArgb(176, 176, 176);
                UpdateColorButtonDisplay(btnColorTextUp, "Upload ▲", SelectedTextColorUp);
                UpdateColorButtonDisplay(btnColorTextDown, "Download ▼", SelectedTextColorDown);

                rbArrowIndividual.Checked = true;
                SelectedArrowColorMode = "Individual";
                SelectedArrowColorUp = SelectedTextColorUp;
                SelectedArrowColorDown = SelectedTextColorDown;
                UpdateColorButtonDisplay(btnColorArrowUp, "Seta Up ▲", SelectedArrowColorUp);
                UpdateColorButtonDisplay(btnColorArrowDown, "Seta Down ▼", SelectedArrowColorDown);

                rbUnitInherit.Checked = true;
                SelectedUnitColorMode = "Inherit";
            }

            UpdateColorControlsState();
            if (pnlLivePreview != null) pnlLivePreview.Invalidate();
            TriggerLiveTuning();
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
                Size = new Size(150, 36),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                BackColor = initialColor,
                ForeColor = GetContrastTextColor(initialColor),
                Cursor = Cursors.Hand,
                Tag = "ColorSwatch"
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
        private void BuildPageIcones(Panel card, bool isPt)
        {
            CreateLabel(card, isPt ? "Modelo Visual das Setas:" : "Arrow Visual Model:", 20, 20);
            cbTemplate = CreateComboBox(card, 20, 46, 472);
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

            // Divisor
            Panel div = new Panel { Location = new Point(20, 96), Size = new Size(card.Width - 40, 1), BackColor = clrBorder };
            card.Controls.Add(div);

            // Caracteres Personalizados
            lblUp = CreateLabel(card, isPt ? "Caractere Upload:" : "Upload Character:", 20, 118);
            txtUp = new TextBox
            {
                Location = new Point(180, 116),
                Size = new Size(140, 24),
                BackColor = clrInputBg,
                ForeColor = clrTextMain,
                Text = SelectedCustomUpChar
            };
            card.Controls.Add(lblUp);
            card.Controls.Add(txtUp);

            lblDown = CreateLabel(card, isPt ? "Caractere Download:" : "Download Character:", 20, 158);
            txtDown = new TextBox
            {
                Location = new Point(180, 156),
                Size = new Size(140, 24),
                BackColor = clrInputBg,
                ForeColor = clrTextMain,
                Text = SelectedCustomDownChar
            };
            card.Controls.Add(lblDown);
            card.Controls.Add(txtDown);

            txtUp.TextChanged += (s, e) => {
                SelectedCustomUpChar = txtUp.Text;
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            };
            txtDown.TextChanged += (s, e) => {
                SelectedCustomDownChar = txtDown.Text;
                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            };

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
                lblUp.Enabled = true;
                lblUp.ForeColor = isCustom ? clrTextMain : clrTextSub;
                txtUp.Enabled = isCustom;
                lblDown.Enabled = true;
                lblDown.ForeColor = isCustom ? clrTextMain : clrTextSub;
                txtDown.Enabled = isCustom;

                pnlLivePreview.Invalidate();
                TriggerLiveTuning();
            };

            bool customInit = (cbTemplate.SelectedIndex == 5);
            lblUp.Enabled = true;
            lblUp.ForeColor = customInit ? clrTextMain : clrTextSub;
            txtUp.Enabled = customInit;
            lblDown.Enabled = true;
            lblDown.ForeColor = customInit ? clrTextMain : clrTextSub;
            txtDown.Enabled = customInit;

            Label lblNote = new Label
            {
                Text = isPt ?
                    "Os modelos vetoriais (Triângulos, Chevrons e Stream Tech) são renderizados matematicamente, garantindo máxima nitidez em qualquer escala de DPI sem pixelização." :
                    "Vector models are rendered mathematically, ensuring maximum sharpness at any DPI scale.",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = clrTextSub,
                Location = new Point(20, 220),
                Size = new Size(card.Width - 40, 75)
            };
            card.Controls.Add(lblNote);
        }

        // ==============================================================================
        // LIVE PREVIEW DINÂMICO
        // ==============================================================================
        private void PnlLivePreview_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color previewBg = isDarkTheme ? NetMonitorApp.BG_COLOR_DARK : NetMonitorApp.BG_COLOR_LIGHT;
            g.Clear(previewBg);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Borda do preview
            using (Pen p = new Pen(clrBorder, 1))
            {
                g.DrawRectangle(p, 0, 0, pnlLivePreview.Width - 1, pnlLivePreview.Height - 1);
            }

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
                if (isDarkTheme)
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
                    RectangleF rUpNum = new RectangleF(2, 2, 52, 18);
                    RectangleF rDnNum = new RectangleF(2, 21, 52, 18);
                    g.DrawString("823.6", fNum, bNumUp, rUpNum, sfR);
                    g.DrawString("142.0", fNum, bNumDown, rDnNum, sfR);

                    RectangleF rUpU = new RectangleF(56, 2, 42, 18);
                    RectangleF rDnU = new RectangleF(56, 21, 42, 18);
                    g.DrawString(unitStr, fUnit, bUnitUp, rUpU, sfL);
                    g.DrawString(unitStr, fUnit, bUnitDown, rDnU, sfL);

                    DrawPreviewArrow(g, 106.0f, bArrUp, bArrDown, pArrUp, pArrDown, fUni, sfC);
                }
                else
                {
                    DrawPreviewArrow(g, 10.0f, bArrUp, bArrDown, pArrUp, pArrDown, fUni, sfC);

                    RectangleF rUpNum = new RectangleF(20, 2, 48, 18);
                    RectangleF rDnNum = new RectangleF(20, 21, 48, 18);
                    g.DrawString("823.6", fNum, bNumUp, rUpNum, sfR);
                    g.DrawString("142.0", fNum, bNumDown, rDnNum, sfR);

                    RectangleF rUpU = new RectangleF(70, 2, 44, 18);
                    RectangleF rDnU = new RectangleF(70, 21, 44, 18);
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
        private Panel CreateCard(Panel parent, int left, int top, int width, int height)
        {
            Panel card = new Panel
            {
                Location = new Point(left, top),
                Size = new Size(width, height),
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
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                UseMnemonic = false
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                SystemEvents.UserPreferenceChanged -= OnThemeChanged;
            }
            catch { }
            base.OnFormClosing(e);
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
            SelectedShowHoverFlyout = chkHoverFlyout.Checked;
            parentApp.showHoverFlyout = SelectedShowHoverFlyout;

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
