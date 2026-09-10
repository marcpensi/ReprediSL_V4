using System;
using System.Threading;
using System.Windows.Forms;

namespace ReprediTrayDaemon
{
    internal static class Program
    {
        private static Mutex? mutex = null;

        [STAThread]
        private static void Main(string[] args)
        {
            const string appName = @"Local\ReprediSL_V4_Daemon_Mutex";
            bool createdNew;

            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show(
                    "El Centro de Control de ReprediSL V4 ya se encuentra en ejecución en la barra de tareas (junto al reloj). Haz doble clic en el icono para abrirlo.",
                    "ReprediSL V4",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool autoStart = false;
            if (args != null)
            {
                foreach (var arg in args)
                {
                    if (arg.Equals("--start-all", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("-start", StringComparison.OrdinalIgnoreCase))
                    {
                        autoStart = true;
                    }
                }
            }

            Application.Run(new MainForm(autoStart));

            GC.KeepAlive(mutex);
        }
    }
}
