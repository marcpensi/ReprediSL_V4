using System;
using System.Threading;
using System.Windows.Forms;

namespace ReprediTrayDaemon
{
    internal static class ProgramCopia
    {
        private static Mutex? mutex = null;

        [STAThread]
        private static void Main()
        {
            const string appName = @"Local\ReprediSL_V4_Daemon_Mutex";
            bool createdNew;

            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show(
                    "El Demonio de Sincronizacion ya se encuentra en ejecucion en la barra de tareas (junto al reloj).",
                    "ReprediSL V4",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());

            GC.KeepAlive(mutex);
        }
    }
}
