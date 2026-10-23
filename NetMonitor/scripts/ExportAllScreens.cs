using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using NetMonitor;

namespace NetMonitorTools
{
    public class ExportAllScreens
    {
        private static string destDir = @"d:\_Antigravity\NetMonitor\Instagram - Carrossel\_Telas NetMonitor";

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!System.IO.Directory.Exists(destDir))
            {
                System.IO.Directory.CreateDirectory(destDir);
            }

            Console.WriteLine("==================================================");
            Console.WriteLine("EXPORTADOR DE TELAS TRANSPARENTES - NETMONITOR v1.0.0");
            Console.WriteLine("Destino: " + destDir);
            Console.WriteLine("==================================================");

            // 1. Central de Configurações - 4 Abas (Dark e Light)
            ExportSettingsPages();

            // 2. Card Flutuante Top 5 Processos (Dark e Light)
            ExportTop5Flyout();

            // 3. Menu de Contexto Completo (Dark e Light)
            ExportContextMenus();

            // 4. Widget da Barra de Tarefas (Dark e Light)
            ExportTaskbarWidget();

            // 5. Assistente de Instalação (Dark e Light)
            ExportInstallerScreens();

            Console.WriteLine("==================================================");
            Console.WriteLine("TODAS AS TELAS EXPORTADAS COM SUCESSO!");
            Console.WriteLine("==================================================");
        }

        // ==============================================================================
        // 1. CONFIGURAÇÕES (SettingsForm) - 4 PÁGINAS EM DARK E LIGHT
        // ==============================================================================
        private static void ExportSettingsPages()
        {
            string[] pageNames = new string[] { "01_Configuracoes_Geral", "02_Configuracoes_Tipografia", "03_Configuracoes_Cores", "04_Configuracoes_Icones_Setas" };
            bool[] themes = new bool[] { true, false };

            foreach (bool isDark in themes)
            {
                string themeSuffix = isDark ? "_Dark.png" : "_Light.png";

                for (int pageIdx = 0; pageIdx < 4; pageIdx++)
                {
                    SettingsForm sf = new SettingsForm(
                        null, "pt", "Bits", "Auto", true, false, true, true, false,
                        "Tris", "▲", "▼", "Segoe UI Variable Display", 9.5f, FontStyle.Regular,
                        "Unified", Color.White, Color.Lime, Color.Red,
                        "Unified", Color.White, Color.Lime, Color.Red,
                        "Unified", Color.White, Color.Lime, Color.Red
                    );

                    sf.Show();
                    Application.DoEvents();

                    sf.ApplyDynamicTheme(isDark);
                    Application.DoEvents();

                    // Aciona o botão de navegação correspondente
                    MethodInfo miSwitch = typeof(SettingsForm).GetMethod("SwitchPage", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (miSwitch != null)
                    {
                        miSwitch.Invoke(sf, new object[] { pageIdx });
                    }
                    Application.DoEvents();

                    int clientW = sf.ClientSize.Width;
                    int clientH = sf.ClientSize.Height;
                    int titleH = 34;
                    int totalW = clientW;
                    int totalH = clientH + titleH;

                    Bitmap bmp = new Bitmap(totalW, totalH, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        g.SmoothingMode = SmoothingMode.AntiAlias;

                        Color bgWindow = isDark ? Color.FromArgb(32, 32, 35) : Color.FromArgb(243, 243, 243);
                        Color borderClr = isDark ? Color.FromArgb(60, 60, 65) : Color.FromArgb(210, 210, 215);
                        Color textTitle = isDark ? Color.White : Color.FromArgb(24, 24, 24);
                        Color closeClr = isDark ? Color.FromArgb(200, 200, 200) : Color.FromArgb(80, 80, 80);

                        // Fundo com cantos arredondados
                        using (GraphicsPath path = CreateRoundedRectPath(new Rectangle(0, 0, totalW - 1, totalH - 1), 8))
                        {
                            using (SolidBrush bBg = new SolidBrush(bgWindow))
                            {
                                g.FillPath(bBg, path);
                            }

                            // Título da janela
                            using (Font fTitle = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                            using (SolidBrush bTitle = new SolidBrush(textTitle))
                            {
                                g.DrawString(sf.Text, fTitle, bTitle, 16, 9);
                            }

                            // Botão Fechar 'X'
                            int cx = totalW - 24;
                            int cy = 17;
                            using (Pen pClose = new Pen(closeClr, 1.0f))
                            {
                                g.DrawLine(pClose, cx - 4, cy - 4, cx + 4, cy + 4);
                                g.DrawLine(pClose, cx + 4, cy - 4, cx - 4, cy + 4);
                            }
                        }

                        // Renderiza os controles filhos do form
                        foreach (Control c in sf.Controls)
                        {
                            if (!c.Visible) continue;
                            using (Bitmap cBmp = new Bitmap(c.Width, c.Height, PixelFormat.Format32bppArgb))
                            {
                                c.DrawToBitmap(cBmp, new Rectangle(0, 0, c.Width, c.Height));
                                g.DrawImage(cBmp, c.Left, titleH + c.Top);
                            }
                        }

                        // Borda externa arredondada fina
                        using (GraphicsPath path = CreateRoundedRectPath(new Rectangle(0, 0, totalW - 1, totalH - 1), 8))
                        using (Pen pBorder = new Pen(borderClr, 1.0f))
                        {
                            g.DrawPath(pBorder, path);
                        }
                    }

                    string fileOut = System.IO.Path.Combine(destDir, pageNames[pageIdx] + themeSuffix);
                    bmp.Save(fileOut, ImageFormat.Png);
                    sf.Close();
                    sf.Dispose();
                    Console.WriteLine("-> Exportado: " + System.IO.Path.GetFileName(fileOut));
                }
            }
        }

        // ==============================================================================
        // 2. CARD FLUTUANTE TOP 5 (NetworkTopProcessesFlyout - Hover)
        // ==============================================================================
        private static void ExportTop5Flyout()
        {
            bool[] themes = new bool[] { true, false };

            List<ProcessTrafficItem> sampleItems = new List<ProcessTrafficItem>
            {
                new ProcessTrafficItem { Name = "Antigravity IDE", DeltaBytes = 52428800, SpeedText = "41.8 Mbps" },
                new ProcessTrafficItem { Name = "Google Chrome", DeltaBytes = 22817024, SpeedText = "18.2 Mbps" },
                new ProcessTrafficItem { Name = "Dropbox", DeltaBytes = 9437184, SpeedText = "7.5 Mbps" },
                new ProcessTrafficItem { Name = "Steam Client", DeltaBytes = 3897446, SpeedText = "3.1 Mbps" },
                new ProcessTrafficItem { Name = "Windows Update", DeltaBytes = 1175000, SpeedText = "940 Kbps" }
            };

            foreach (bool isDark in themes)
            {
                string themeSuffix = isDark ? "_Dark.png" : "_Light.png";
                NetworkTopProcessesFlyout flyout = new NetworkTopProcessesFlyout();
                flyout.Show();
                Application.DoEvents();

                flyout.ApplyTheme(isDark, "pt");
                flyout.UpdateItems(sampleItems);
                Application.DoEvents();

                int w = flyout.Width;
                int h = flyout.Height;

                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    flyout.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
                }

                // Aplica máscara com cantos arredondados transparentes
                Bitmap maskedBmp = ApplyRoundedMask(bmp, 8, isDark ? Color.FromArgb(60, 60, 65) : Color.FromArgb(210, 210, 215));

                string fileOut = System.IO.Path.Combine(destDir, "05_Top5_Hover" + themeSuffix);
                maskedBmp.Save(fileOut, ImageFormat.Png);
                flyout.Close();
                flyout.Dispose();
                Console.WriteLine("-> Exportado: " + System.IO.Path.GetFileName(fileOut));
            }
        }

        // ==============================================================================
        // 3. MENU DE CONTEXTO FLUENT WINDOWS 11 (ContextMenuStrip)
        // ==============================================================================
        private static void ExportContextMenus()
        {
            bool[] themes = new bool[] { true, false };

            foreach (bool isDark in themes)
            {
                string themeSuffix = isDark ? "_Dark.png" : "_Light.png";

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Renderer = new Windows11MenuRenderer(isDark);
                menu.ShowCheckMargin = true;
                menu.ShowImageMargin = false;
                menu.Font = new Font("Segoe UI", 9.0f);

                // Cabeçalho de Destaque
                ToolStripMenuItem itemHeader = new ToolStripMenuItem("NetMonitor v1.0.0");
                itemHeader.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                itemHeader.ForeColor = Color.FromArgb(0, 120, 212);
                menu.Items.Add(itemHeader);
                menu.Items.Add(new ToolStripSeparator());

                // Itens com Checkmarks
                ToolStripMenuItem itSpeed = new ToolStripMenuItem("Formato: Bits (Mbps, Kbps)");
                itSpeed.Checked = true;
                menu.Items.Add(itSpeed);

                ToolStripMenuItem itUnit = new ToolStripMenuItem("Unidade: Automático");
                itUnit.Checked = true;
                menu.Items.Add(itUnit);

                ToolStripMenuItem itAdapt = new ToolStripMenuItem("Placa: Intel(R) Wi-Fi 6 AX201");
                itAdapt.Checked = true;
                menu.Items.Add(itAdapt);

                ToolStripMenuItem itArrow = new ToolStripMenuItem("Posição das Setas: À Direita (823 Kbps ▲)");
                itArrow.Checked = true;
                menu.Items.Add(itArrow);

                ToolStripMenuItem itLang = new ToolStripMenuItem("Idioma: Português (Brasil)");
                itLang.Checked = true;
                menu.Items.Add(itLang);

                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem itBoot = new ToolStripMenuItem("Iniciar com o Windows");
                itBoot.Checked = true;
                menu.Items.Add(itBoot);

                ToolStripMenuItem itLock = new ToolStripMenuItem("Travar Posição na Barra");
                itLock.Checked = false;
                menu.Items.Add(itLock);

                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem itSet = new ToolStripMenuItem("Personalizar (Configurações)...");
                itSet.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                menu.Items.Add(itSet);

                ToolStripMenuItem itDev = new ToolStripMenuItem("Noé VFX (noevfx.com)");
                itDev.ForeColor = Color.FromArgb(0, 120, 212);
                menu.Items.Add(itDev);

                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem itClose = new ToolStripMenuItem("Fechar NetMonitor");
                menu.Items.Add(itClose);

                // Força layout
                menu.Show(new Point(-5000, -5000));
                Application.DoEvents();

                int w = menu.Width;
                int h = menu.Height;

                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    menu.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
                }

                Bitmap maskedBmp = ApplyRoundedMask(bmp, 6, isDark ? Color.FromArgb(65, 65, 70) : Color.FromArgb(205, 205, 210));

                string fileOut = System.IO.Path.Combine(destDir, "06_Menu_Contexto" + themeSuffix);
                maskedBmp.Save(fileOut, ImageFormat.Png);
                menu.Close();
                menu.Dispose();
                Console.WriteLine("-> Exportado: " + System.IO.Path.GetFileName(fileOut));
            }
        }

        // ==============================================================================
        // 4. WIDGET DA BARRA DE TAREFAS (Taskbar Widget)
        // ==============================================================================
        private static void ExportTaskbarWidget()
        {
            bool[] themes = new bool[] { true, false };

            foreach (bool isDark in themes)
            {
                string themeSuffix = isDark ? "_Dark.png" : "_Light.png";

                int w = 114;
                int h = 46;

                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    Color bgCard = isDark ? Color.FromArgb(26, 26, 28) : Color.FromArgb(240, 240, 245);
                    Color borderClr = isDark ? Color.FromArgb(50, 50, 55) : Color.FromArgb(215, 215, 220);
                    Color txtSpeed = isDark ? Color.White : Color.FromArgb(20, 20, 20);
                    Color txtUnit = isDark ? Color.FromArgb(180, 180, 185) : Color.FromArgb(100, 100, 105);
                    Color clrUp = Color.FromArgb(0, 160, 255);
                    Color clrDown = Color.FromArgb(255, 120, 40);

                    // Container suave
                    using (GraphicsPath path = CreateRoundedRectPath(new Rectangle(2, 2, w - 5, h - 5), 6))
                    {
                        using (SolidBrush bBg = new SolidBrush(bgCard))
                        {
                            g.FillPath(bBg, path);
                        }
                        using (Pen pBorder = new Pen(borderClr, 1.0f))
                        {
                            g.DrawPath(pBorder, path);
                        }
                    }

                    using (Font fNum = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                    using (Font fUnit = new Font("Segoe UI", 7.5f, FontStyle.Regular))
                    using (Font fArrow = new Font("Segoe UI", 8.0f, FontStyle.Bold))
                    {
                        // Linha Upload
                        g.DrawString("823.6", fNum, new SolidBrush(txtSpeed), 10, 6);
                        g.DrawString("Mbps", fUnit, new SolidBrush(txtUnit), 54, 7.5f);
                        g.DrawString("▲", fArrow, new SolidBrush(clrUp), 88, 7.0f);

                        // Linha Download
                        g.DrawString("142.0", fNum, new SolidBrush(txtSpeed), 10, 24);
                        g.DrawString("Mbps", fUnit, new SolidBrush(txtUnit), 54, 25.5f);
                        g.DrawString("▼", fArrow, new SolidBrush(clrDown), 88, 25.0f);
                    }
                }

                string fileOut = System.IO.Path.Combine(destDir, "07_Widget_Taskbar" + themeSuffix);
                bmp.Save(fileOut, ImageFormat.Png);
                Console.WriteLine("-> Exportado: " + System.IO.Path.GetFileName(fileOut));
            }
        }

        // ==============================================================================
        // 5. ASSISTENTE DE INSTALAÇÃO (Installer UI)
        // ==============================================================================
        private static void ExportInstallerScreens()
        {
            bool[] themes = new bool[] { true, false };

            foreach (bool isDark in themes)
            {
                string themeSuffix = isDark ? "_Dark.png" : "_Light.png";

                int clientW = 520;
                int clientH = 390;
                int titleH = 34;
                int totalW = clientW;
                int totalH = clientH + titleH;

                Bitmap bmp = new Bitmap(totalW, totalH, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    Color bgWindow = isDark ? Color.FromArgb(32, 32, 35) : Color.FromArgb(245, 245, 248);
                    Color bgHeader = isDark ? Color.FromArgb(24, 24, 26) : Color.FromArgb(235, 235, 240);
                    Color borderClr = isDark ? Color.FromArgb(60, 60, 65) : Color.FromArgb(210, 210, 215);
                    Color textTitle = isDark ? Color.White : Color.FromArgb(24, 24, 24);
                    Color textSub = isDark ? Color.FromArgb(180, 180, 185) : Color.FromArgb(100, 100, 105);
                    Color clrAccent = Color.FromArgb(0, 120, 212);

                    // Fundo com cantos arredondados
                    using (GraphicsPath path = CreateRoundedRectPath(new Rectangle(0, 0, totalW - 1, totalH - 1), 8))
                    {
                        using (SolidBrush bBg = new SolidBrush(bgWindow))
                        {
                            g.FillPath(bBg, path);
                        }

                        // Título da janela
                        using (Font fTitle = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                        using (SolidBrush bTitle = new SolidBrush(textTitle))
                        {
                            g.DrawString("Instalação do NetMonitor v1.0.0", fTitle, bTitle, 16, 9);
                        }

                        // Botão Fechar 'X'
                        int cx = totalW - 24;
                        int cy = 17;
                        using (Pen pClose = new Pen(isDark ? Color.FromArgb(200, 200, 200) : Color.FromArgb(80, 80, 80), 1.0f))
                        {
                            g.DrawLine(pClose, cx - 4, cy - 4, cx + 4, cy + 4);
                            g.DrawLine(pClose, cx + 4, cy - 4, cx - 4, cy + 4);
                        }
                    }

                    // Header do Instalador
                    using (SolidBrush bHdr = new SolidBrush(bgHeader))
                    {
                        g.FillRectangle(bHdr, 1, titleH, totalW - 2, 80);
                    }
                    using (Font fHdrTitle = new Font("Segoe UI", 13.0f, FontStyle.Bold))
                    using (Font fHdrSub = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                    {
                        g.DrawString("NetMonitor v1.0.0", fHdrTitle, new SolidBrush(textTitle), 24, titleH + 16);
                        g.DrawString("Monitor de tráfego de rede ultraleve para a barra de tarefas do Windows 11", fHdrSub, new SolidBrush(textSub), 24, titleH + 44);
                    }

                    // Linha divisória
                    using (Pen pDiv = new Pen(borderClr, 1.0f))
                    {
                        g.DrawLine(pDiv, 0, titleH + 80, totalW, titleH + 80);
                    }

                    // Opções de Instalação no Corpo
                    using (Font fBody = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                    using (Font fSmall = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                    {
                        int optY = titleH + 105;
                        g.DrawString("Destino da instalação:", fBody, new SolidBrush(textTitle), 28, optY);

                        // Campo de texto de destino
                        int inputY = optY + 26;
                        using (SolidBrush bInput = new SolidBrush(isDark ? Color.FromArgb(24, 24, 26) : Color.White))
                        {
                            g.FillRectangle(bInput, 28, inputY, 464, 30);
                        }
                        using (Pen pInput = new Pen(borderClr, 1.0f))
                        {
                            g.DrawRectangle(pInput, 28, inputY, 464, 30);
                        }
                        g.DrawString(@"C:\Users\enn_g\AppData\Local\Programs\NetMonitor", fSmall, new SolidBrush(textTitle), 36, inputY + 7);

                        // Checkboxes
                        int chkY1 = inputY + 46;
                        DrawCustomCheckBox(g, 30, chkY1, true, isDark, "Criar atalho no Menu Iniciar do Windows", textTitle);

                        int chkY2 = chkY1 + 30;
                        DrawCustomCheckBox(g, 30, chkY2, true, isDark, "Iniciar automaticamente com o Windows (Recomendado)", textTitle);

                        int chkY3 = chkY2 + 30;
                        DrawCustomCheckBox(g, 30, chkY3, true, isDark, "Registrar no Painel Adicionar ou Remover Programas", textTitle);
                    }

                    // Rodapé com Botões
                    int botY = totalH - 65;
                    using (Pen pDiv = new Pen(borderClr, 1.0f))
                    {
                        g.DrawLine(pDiv, 0, botY, totalW, botY);
                    }

                    // Botão Instalar (Azul)
                    int btnInstallX = totalW - 200;
                    using (SolidBrush bBtn = new SolidBrush(clrAccent))
                    {
                        g.FillRectangle(bBtn, btnInstallX, botY + 16, 88, 34);
                    }
                    using (Font fBtn = new Font("Segoe UI", 9.0f, FontStyle.Bold))
                    {
                        g.DrawString("Instalar", fBtn, Brushes.White, btnInstallX + 22, botY + 24);
                    }

                    // Botão Cancelar
                    int btnCancelX = totalW - 102;
                    using (SolidBrush bBtn = new SolidBrush(isDark ? Color.FromArgb(42, 42, 46) : Color.White))
                    {
                        g.FillRectangle(bBtn, btnCancelX, botY + 16, 78, 34);
                    }
                    using (Pen pBtn = new Pen(borderClr, 1.0f))
                    {
                        g.DrawRectangle(pBtn, btnCancelX, botY + 16, 78, 34);
                    }
                    using (Font fBtn = new Font("Segoe UI", 9.0f, FontStyle.Regular))
                    {
                        g.DrawString("Cancelar", fBtn, new SolidBrush(textTitle), btnCancelX + 16, botY + 24);
                    }

                    // Borda externa arredondada final
                    using (GraphicsPath path = CreateRoundedRectPath(new Rectangle(0, 0, totalW - 1, totalH - 1), 8))
                    using (Pen pBorder = new Pen(borderClr, 1.0f))
                    {
                        g.DrawPath(pBorder, path);
                    }
                }

                string fileOut = System.IO.Path.Combine(destDir, "08_Instalador" + themeSuffix);
                bmp.Save(fileOut, ImageFormat.Png);
                Console.WriteLine("-> Exportado: " + System.IO.Path.GetFileName(fileOut));
            }
        }

        // ==============================================================================
        // MÉTODOS UTILITÁRIOS DE MÁSCARA E DESENHO
        // ==============================================================================
        private static Bitmap ApplyRoundedMask(Bitmap src, int radius, Color borderColor)
        {
            Bitmap res = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(res))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (GraphicsPath path = CreateRoundedRectPath(new Rectangle(0, 0, src.Width - 1, src.Height - 1), radius))
                {
                    g.SetClip(path);
                    g.DrawImage(src, 0, 0);
                    g.ResetClip();

                    using (Pen p = new Pen(borderColor, 1.0f))
                    {
                        g.DrawPath(p, path);
                    }
                }
            }
            return res;
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void DrawCustomCheckBox(Graphics g, int x, int y, bool isChecked, bool isDark, string text, Color textColor)
        {
            Color boxBg = isChecked ? Color.FromArgb(0, 120, 212) : (isDark ? Color.FromArgb(40, 40, 42) : Color.White);
            Color boxBorder = isChecked ? Color.FromArgb(0, 120, 212) : (isDark ? Color.FromArgb(80, 80, 85) : Color.FromArgb(180, 180, 185));

            using (SolidBrush b = new SolidBrush(boxBg))
            {
                g.FillRectangle(b, x, y, 16, 16);
            }
            using (Pen p = new Pen(boxBorder, 1.0f))
            {
                g.DrawRectangle(p, x, y, 16, 16);
            }

            if (isChecked)
            {
                using (Pen pCheck = new Pen(Color.White, 2.0f))
                {
                    g.DrawLines(pCheck, new PointF[] { new PointF(x + 3.5f, y + 8.5f), new PointF(x + 6.5f, y + 12.0f), new PointF(x + 12.5f, y + 4.5f) });
                }
            }

            using (Font f = new Font("Segoe UI", 9.0f, FontStyle.Regular))
            using (SolidBrush bTxt = new SolidBrush(textColor))
            {
                g.DrawString(text, f, bTxt, x + 24, y);
            }
        }
    }
}
