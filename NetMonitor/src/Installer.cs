// ==============================================================================
// NetMonitor Installer (Bilingual: Portuguese & English)
// Autor: Eduardo Noé (eduardonoe)
// Version: 1.0.5
//
// v1.0.3 changelog:
// - Adiciona subpasta \NetMonitor automaticamente ao selecionar qualquer diretório.
// - Suporte a instalação em C:\Program Files com auto-elevação de Administrador (UAC).
// ==============================================================================

using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NetMonitor
{
    public class InstallerForm : Form
    {
        private ComboBox cbLanguage;
        private Label lblTitle;
        private Label lblDesc;
        private Label lblDest;
        private TextBox txtDest;
        private Button btnBrowse;
        private CheckBox chkBoot;
        private CheckBox chkStartMenu;
        private CheckBox chkDesktop;
        private Button btnInstall;
        private Button btnCancel;
        private Label lblStatus;
        private ProgressBar progressBar;

        private bool isPt = true;

        public InstallerForm(string initialDest = null, string initialLang = null)
        {
            if (!string.IsNullOrEmpty(initialLang))
            {
                isPt = initialLang.Equals("pt", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                isPt = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase);
            }

            InitializeUI(initialDest);
            UpdateLanguage();
        }

        private void InitializeUI(string initialDest)
        {
            this.Text = "NetMonitor Setup";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(520, 390);
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = Color.FromArgb(24, 24, 24)
            };
            this.Controls.Add(pnlHeader);

            lblTitle = new Label
            {
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White
            };
            pnlHeader.Controls.Add(lblTitle);

            lblDesc = new Label
            {
                Location = new Point(20, 45),
                AutoSize = true,
                ForeColor = Color.FromArgb(180, 180, 180)
            };
            pnlHeader.Controls.Add(lblDesc);

            cbLanguage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(360, 20),
                Width = 140,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cbLanguage.Items.Add("Português (Brasil)");
            cbLanguage.Items.Add("English");
            cbLanguage.SelectedIndex = isPt ? 0 : 1;
            cbLanguage.SelectedIndexChanged += (s, e) =>
            {
                isPt = (cbLanguage.SelectedIndex == 0);
                UpdateLanguage();
            };
            pnlHeader.Controls.Add(cbLanguage);

            lblDest = new Label
            {
                Location = new Point(22, 105),
                AutoSize = true,
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            this.Controls.Add(lblDest);

            string defaultPath = !string.IsNullOrEmpty(initialDest) 
                ? initialDest 
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\NetMonitor");

            txtDest = new TextBox
            {
                Location = new Point(25, 130),
                Width = 370,
                Text = defaultPath,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(txtDest);

            btnBrowse = new Button
            {
                Location = new Point(405, 128),
                Width = 90,
                Height = 26,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            btnBrowse.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = txtDest.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        string selected = fbd.SelectedPath.Trim().TrimEnd('\\');
                        // Garante que o nome da pasta NetMonitor seja sempre adicionado
                        if (!selected.EndsWith("NetMonitor", StringComparison.OrdinalIgnoreCase))
                        {
                            selected = Path.Combine(selected, "NetMonitor");
                        }
                        txtDest.Text = selected;
                    }
                }
            };
            this.Controls.Add(btnBrowse);

            chkBoot = new CheckBox
            {
                Location = new Point(25, 175),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.FromArgb(230, 230, 230)
            };
            this.Controls.Add(chkBoot);

            chkStartMenu = new CheckBox
            {
                Location = new Point(25, 205),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.FromArgb(230, 230, 230)
            };
            this.Controls.Add(chkStartMenu);

            chkDesktop = new CheckBox
            {
                Location = new Point(25, 235),
                AutoSize = true,
                Checked = false,
                ForeColor = Color.FromArgb(230, 230, 230)
            };
            this.Controls.Add(chkDesktop);

            progressBar = new ProgressBar
            {
                Location = new Point(25, 280),
                Width = 470,
                Height = 16,
                Visible = false
            };
            this.Controls.Add(progressBar);

            lblStatus = new Label
            {
                Location = new Point(25, 302),
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 160, 160)
            };
            this.Controls.Add(lblStatus);

            btnCancel = new Button
            {
                Location = new Point(405, 340),
                Width = 90,
                Height = 32,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(btnCancel);

            btnInstall = new Button
            {
                Location = new Point(295, 340),
                Width = 100,
                Height = 32,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Click += BtnInstall_Click;
            this.Controls.Add(btnInstall);
        }

        private void UpdateLanguage()
        {
            if (isPt)
            {
                this.Text = "Instalação do NetMonitor v1.0.5";
                lblTitle.Text = "Instalação do NetMonitor";
                lblDesc.Text = "Monitor de rede ultraleve para a barra de tarefas do Windows 11";
                lblDest.Text = "Pasta de destino:";
                btnBrowse.Text = "Procurar...";
                chkBoot.Text = "Iniciar automaticamente com o Windows (Boot)";
                chkStartMenu.Text = "Criar atalho no Menu Iniciar";
                chkDesktop.Text = "Criar atalho na Área de Trabalho";
                btnInstall.Text = "Instalar";
                btnCancel.Text = "Cancelar";
                lblStatus.Text = "Pronto para iniciar a instalação.";
            }
            else
            {
                this.Text = "NetMonitor Setup v1.0.5";
                lblTitle.Text = "NetMonitor Setup";
                lblDesc.Text = "Ultralight network monitor for Windows 11 taskbar";
                lblDest.Text = "Destination folder:";
                btnBrowse.Text = "Browse...";
                chkBoot.Text = "Start automatically with Windows (Boot)";
                chkStartMenu.Text = "Create Start Menu shortcut";
                chkDesktop.Text = "Create Desktop shortcut";
                btnInstall.Text = "Install";
                btnCancel.Text = "Cancel";
                lblStatus.Text = "Ready to begin installation.";
            }
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            string installDir = txtDest.Text.Trim().TrimEnd('\\');
            if (string.IsNullOrEmpty(installDir))
            {
                installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\NetMonitor");
            }

            // Garante que o diretório finalize com \NetMonitor
            if (!installDir.EndsWith("NetMonitor", StringComparison.OrdinalIgnoreCase))
            {
                installDir = Path.Combine(installDir, "NetMonitor");
                txtDest.Text = installDir;
            }

            // Verifica se a pasta requer elevação de Administrador (ex: Program Files)
            bool needsAdmin = installDir.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase) ||
                              installDir.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), StringComparison.OrdinalIgnoreCase) ||
                              installDir.IndexOf("Program Files", StringComparison.OrdinalIgnoreCase) >= 0;

            if (needsAdmin && !IsAdministrator())
            {
                // Reinicia o instalador solicitando UAC (permissão de Administrador)
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        Arguments = string.Format("\"{0}\" {1}", installDir, isPt ? "pt" : "en"),
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    this.Close();
                    return;
                }
                catch
                {
                    string msg = isPt 
                        ? "A pasta escolhida exige permissões de Administrador para ser criada." 
                        : "The chosen folder requires Administrator permissions to be created.";
                    MessageBox.Show(msg, isPt ? "Permissão Necessária" : "Permission Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            btnInstall.Enabled = false;
            btnBrowse.Enabled = false;
            txtDest.Enabled = false;
            cbLanguage.Enabled = false;
            progressBar.Visible = true;
            progressBar.Value = 10;

            lblStatus.Text = isPt ? "Fechando instâncias anteriores..." : "Closing previous instances...";
            Application.DoEvents();

            try
            {
                Process[] procs = Process.GetProcessesByName("NetMonitor");
                foreach (Process p in procs)
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }
            }
            catch { }

            try
            {
                if (!Directory.Exists(installDir))
                {
                    Directory.CreateDirectory(installDir);
                }

                progressBar.Value = 35;
                lblStatus.Text = isPt ? "Extraindo arquivos do aplicativo..." : "Extracting application files...";
                Application.DoEvents();

                string exePath = Path.Combine(installDir, "NetMonitor.exe");
                string uninstallerPath = Path.Combine(installDir, "uninstall.exe");

                ExtractResource("NetMonitor.exe", exePath);
                progressBar.Value = 60;

                ExtractResource("uninstall.exe", uninstallerPath);
                progressBar.Value = 75;

                lblStatus.Text = isPt ? "Registrando no Windows..." : "Registering with Windows...";
                Application.DoEvents();

                using (RegistryKey appKey = Registry.CurrentUser.CreateSubKey(@"Software\NetMonitor"))
                {
                    if (appKey != null)
                    {
                        appKey.SetValue("Language", isPt ? "pt" : "en");
                    }
                }

                if (chkBoot.Checked)
                {
                    using (RegistryKey runKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        if (runKey != null) runKey.SetValue("NetMonitor", "\"" + exePath + "\"");
                    }
                }

                if (chkStartMenu.Checked)
                {
                    string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs));
                    string shortcutPath = Path.Combine(startMenuDir, "NetMonitor.lnk");
                    CreateShortcut(shortcutPath, exePath, installDir, isPt ? "NetMonitor - Monitor de Rede" : "NetMonitor - Network Monitor");
                }

                if (chkDesktop.Checked)
                {
                    string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string shortcutPath = Path.Combine(desktopDir, "NetMonitor.lnk");
                    CreateShortcut(shortcutPath, exePath, installDir, isPt ? "NetMonitor - Monitor de Rede" : "NetMonitor - Network Monitor");
                }

                using (RegistryKey uninstKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\NetMonitor"))
                {
                    if (uninstKey != null)
                    {
                        uninstKey.SetValue("DisplayName", "NetMonitor");
                        uninstKey.SetValue("DisplayVersion", "1.0.5");
                        uninstKey.SetValue("Publisher", "Eduardo Noé");
                        uninstKey.SetValue("InstallLocation", installDir);
                        uninstKey.SetValue("DisplayIcon", exePath + ",0");
                        uninstKey.SetValue("UninstallString", "\"" + uninstallerPath + "\"");
                        uninstKey.SetValue("QuietUninstallString", "\"" + uninstallerPath + "\" /quiet");
                        uninstKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        uninstKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }

                progressBar.Value = 95;
                lblStatus.Text = isPt ? "Iniciando NetMonitor..." : "Starting NetMonitor...";
                Application.DoEvents();

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = installDir
                    });
                }
                catch { }

                progressBar.Value = 100;
                lblStatus.Text = isPt ? "Instalação concluída com sucesso!" : "Installation completed successfully!";

                string successTitle = isPt ? "Instalação Concluída" : "Setup Complete";
                string successMsg = isPt
                    ? "NetMonitor v1.0.5 foi instalado com sucesso!\nO aplicativo já está rodando na sua barra de tarefas."
                    : "NetMonitor v1.0.5 has been successfully installed!\nThe application is now running in your taskbar.";

                MessageBox.Show(successMsg, successTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                string errTitle = isPt ? "Erro na Instalação" : "Installation Error";
                MessageBox.Show(ex.Message, errTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnInstall.Enabled = true;
                btnBrowse.Enabled = true;
                txtDest.Enabled = true;
                cbLanguage.Enabled = true;
            }
        }

        private void ExtractResource(string resourceName, string outputPath)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string matchedName = null;
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase))
                {
                    matchedName = name;
                    break;
                }
            }

            if (matchedName == null)
            {
                string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, resourceName);
                if (File.Exists(localPath))
                {
                    File.Copy(localPath, outputPath, true);
                    return;
                }
                throw new FileNotFoundException("Recurso não encontrado: " + resourceName);
            }

            using (Stream s = asm.GetManifestResourceStream(matchedName))
            {
                if (s == null) throw new Exception("Falha ao abrir stream do recurso: " + resourceName);
                using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    s.CopyTo(fs);
                }
            }
        }

        private void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = workingDir;
                shortcut.Description = description;
                shortcut.Save();
            }
            catch { }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string initialDest = (args.Length > 0) ? args[0] : null;
            string initialLang = (args.Length > 1) ? args[1] : null;

            Application.Run(new InstallerForm(initialDest, initialLang));
        }
    }
}
