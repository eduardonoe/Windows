// ==============================================================================
// NetMonitor Uninstaller (Bilingual: Portuguese & English)
// Autor: Eduardo Noé (eduardonoe)
// Version: 1.0.5
// ==============================================================================

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NetMonitor
{
    public class Uninstaller
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool isQuiet = false;
            string lang = "pt";

            // Detect language from registry or OS
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\NetMonitor"))
                {
                    if (key != null)
                    {
                        object l = key.GetValue("Language");
                        if (l != null) lang = l.ToString();
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(lang))
            {
                lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase) ? "pt" : "en";
            }

            foreach (string arg in args)
            {
                if (arg.Equals("/quiet", StringComparison.OrdinalIgnoreCase) || arg.Equals("/q", StringComparison.OrdinalIgnoreCase)) isQuiet = true;
                if (arg.StartsWith("/lang:", StringComparison.OrdinalIgnoreCase)) lang = arg.Substring(6);
            }

            bool isPt = lang.Equals("pt", StringComparison.OrdinalIgnoreCase);

            string title = isPt ? "Desinstalação do NetMonitor" : "NetMonitor Uninstaller";
            string prompt = isPt
                ? "Deseja realmente remover o NetMonitor do seu computador?"
                : "Are you sure you want to completely remove NetMonitor from your computer?";
            string doneMsg = isPt
                ? "O NetMonitor foi desinstalado com sucesso do seu computador."
                : "NetMonitor has been successfully uninstalled from your computer.";

            if (!isQuiet)
            {
                DialogResult dr = MessageBox.Show(prompt, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr != DialogResult.Yes) return;
            }

            // 1. Fechar processos ativos do NetMonitor
            try
            {
                Process[] procs = Process.GetProcessesByName("NetMonitor");
                foreach (Process p in procs)
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }
            }
            catch { }

            // 2. Remover do Run (Boot)
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null) key.DeleteValue("NetMonitor", false);
                }
            }
            catch { }

            // 3. Remover do Painel de Controle / Configurações do Windows
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\NetMonitor", false);
            }
            catch { }

            // 4. Remover Configurações do App
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\NetMonitor", false);
            }
            catch { }

            // 5. Remover Atalhos
            try
            {
                string startLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "NetMonitor.lnk");
                if (File.Exists(startLnk)) File.Delete(startLnk);

                string deskLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NetMonitor.lnk");
                if (File.Exists(deskLnk)) File.Delete(deskLnk);
            }
            catch { }

            // 6. Remover a pasta de instalação (auto-deleção assíncrona)
            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = string.Format("/c timeout /t 1 /nobreak >nul & rmdir /s /q \"{0}\"", appDir),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch { }

            if (!isQuiet)
            {
                MessageBox.Show(doneMsg, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
