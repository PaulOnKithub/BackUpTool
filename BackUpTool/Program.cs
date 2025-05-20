using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BackUpTool
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 
        [STAThread]
        static void Main()
        {
            if (!IsAdministrator())
            {
                // Restart as admin
                var processInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = Application.ExecutablePath,
                    Verb = "runas" // Causes UAC prompt
                };

                try
                {
                    Process.Start(processInfo);
                }
                catch
                {
                    MessageBox.Show("This application needs to be run as Administrator.", "Elevation Required",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                }

                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BackUpForm());
        }

        static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
