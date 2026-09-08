using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ReprediTrayDaemon.Services;

namespace ReprediTrayDaemon
{
    public class MainForm : Form
    {
        private NotifyIcon trayIcon = null!;
        private ContextMenuStrip trayMenu = null!;
        private RichTextBox txtLog = null!;
        private Panel pnlTop = null!;
        private Label lblStatus = null!;
        private Button btnSync = null!;
        private Button btnErrors = null!;
        private Button btnClear = null!;
        private Button btnHide = null!;

        private DbSyncService syncService = null!;
        private System.Windows.Forms.Timer timerHealth = null!;
        private bool forceClose = false;

        public MainForm()
        {
            InitializeComponentCustom();

            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));
            if (!File.Exists(Path.Combine(projectRoot, "AGENTS.md")))
            {
                projectRoot = @"D:\programacio\repredi\ReprediSL_V4";
            }

            syncService = new DbSyncService(projectRoot);
            syncService.OnLogMessage += SyncService_OnLogMessage;
            syncService.OnErrorThresholdExceeded += SyncService_OnErrorThresholdExceeded;

            lblStatus.Text = $"PsGest Target: {syncService.MdbPath}";

            syncService.AppendLog("Demonio de bandeja ReprediSL V4 C# NATIVO iniciado.", DbSyncService.LogLevel.Success);
            syncService.AppendLog($"PsGest Target: {syncService.MdbPath}", DbSyncService.LogLevel.Info);

            timerHealth = new System.Windows.Forms.Timer { Interval = 2000 };
            timerHealth.Tick += TimerHealth_Tick;
            timerHealth.Start();
        }

        private void InitializeComponentCustom()
        {
            this.Text = "Demonio de Sincronizacion - ReprediSL V4 (Nativo .NET)";
            this.Size = new Size(850, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Application;

            // Panel Superior
            pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.FromArgb(240, 243, 246),
                Padding = new Padding(10)
            };

            lblStatus = new Label
            {
                Text = "Conectando a postgres ...",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                AutoSize = true,
                Location = new Point(12, 12)
            };

            btnSync = new Button
            {
                Text = "⚡ Sincronizar Ahora",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(160, 32),
                Location = new Point(12, 35),
                Cursor = Cursors.Hand
            };
            btnSync.FlatAppearance.BorderSize = 0;
            btnSync.Click += async (s, e) => await DoManualSyncAsync();

            btnErrors = new Button
            {
                Text = "🚨 Ver Errores",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(130, 32),
                Location = new Point(180, 35),
                Cursor = Cursors.Hand
            };
            btnErrors.FlatAppearance.BorderSize = 0;
            btnErrors.Click += (s, e) => OpenLogFile(syncService.ErrorLogPath);

            btnClear = new Button
            {
                Text = "🧹 Limpiar Consola",
                Font = new Font("Segoe UI", 9F),
                Size = new Size(130, 32),
                Location = new Point(320, 35),
                Cursor = Cursors.Hand
            };
            btnClear.Click += (s, e) => txtLog.Clear();

            btnHide = new Button
            {
                Text = "📌 Ocultar a Bandeja",
                Font = new Font("Segoe UI", 9F),
                Size = new Size(140, 32),
                Location = new Point(460, 35),
                Cursor = Cursors.Hand
            };
            btnHide.Click += (s, e) => this.Hide();

            pnlTop.Controls.Add(lblStatus);
            pnlTop.Controls.Add(btnSync);
            pnlTop.Controls.Add(btnErrors);
            pnlTop.Controls.Add(btnClear);
            pnlTop.Controls.Add(btnHide);

            // Consola Log RichTextBox
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.FromArgb(230, 230, 230),
                Font = new Font("Consolas", 10F),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10)
            };

            this.Controls.Add(txtLog);
            this.Controls.Add(pnlTop);

            // Menu Tray Icon
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("🖥️ Abrir Monitoreo", null, (s, e) => ShowForm());
            trayMenu.Items.Add("⚡ Sincronizar PostgreSQL Ahora", null, async (s, e) => await DoManualSyncAsync());
            trayMenu.Items.Add("🚨 Ver Registro de Errores", null, (s, e) => OpenLogFile(syncService.ErrorLogPath));
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("❌ Salir del Demonio", null, (s, e) => ExitApplication());

            // System Tray Icon
            trayIcon = new NotifyIcon
            {
                Text = "ReprediSL V4 Daemon",
                Icon = SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            trayIcon.DoubleClick += (s, e) => ShowForm();
        }

        private void SyncService_OnLogMessage(string message, DbSyncService.LogLevel level)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SyncService_OnLogMessage(message, level)));
                return;
            }

            Color logColor = level switch
            {
                DbSyncService.LogLevel.Error => Color.Crimson,
                DbSyncService.LogLevel.Warning => Color.DarkOrange,
                DbSyncService.LogLevel.Success => Color.LimeGreen,
                _ => Color.LightSteelBlue
            };

            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = logColor;
            txtLog.AppendText(message + Environment.NewLine);
            txtLog.SelectionColor = txtLog.ForeColor;
            txtLog.ScrollToCaret();

            if (level == DbSyncService.LogLevel.Error)
            {
                trayIcon.ShowBalloonTip(3000, "ReprediSL V4 - Error", message, ToolTipIcon.Error);
            }
        }

        private async Task DoManualSyncAsync()
        {
            btnSync.Enabled = false;
            btnSync.Text = "⏳ Sincronizando...";
            try
            {
                await syncService.RunExportAsync();
            }
            finally
            {
                btnSync.Enabled = true;
                btnSync.Text = "⚡ Sincronizar Ahora";
            }
        }

        private bool isThresholdPromptActive = false;

        private void SyncService_OnErrorThresholdExceeded(int count)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SyncService_OnErrorThresholdExceeded(count)));
                return;
            }

            if (isThresholdPromptActive) return;
            isThresholdPromptActive = true;

            using (var promptForm = new Form())
            {
                promptForm.Text = "🚨 ALERTA DE ERRORES ELEVADOS (>3) - ReprediSL V4";
                promptForm.Size = new Size(530, 230);
                promptForm.StartPosition = FormStartPosition.CenterScreen;
                promptForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                promptForm.MaximizeBox = false;
                promptForm.MinimizeBox = false;
                promptForm.Icon = SystemIcons.Warning;

                var lbl = new Label
                {
                    Text = $"Se han acumulado {count} errores durante la sincronización.\n" +
                           $"Todos los errores han sido guardados en 'sync_errors.log'.\n\n" +
                           $"¿Qué acción desea realizar?",
                    Font = new Font("Segoe UI", 9.5F),
                    Location = new Point(20, 20),
                    Size = new Size(470, 75)
                };

                var btnStop = new Button
                {
                    Text = "🛑 Cancelar Proceso",
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    BackColor = Color.FromArgb(220, 53, 69),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Location = new Point(20, 115),
                    Size = new Size(150, 42),
                    Cursor = Cursors.Hand,
                    DialogResult = DialogResult.Abort
                };
                btnStop.FlatAppearance.BorderSize = 0;

                var btnSilence = new Button
                {
                    Text = "🔕 Continuar en Silencio",
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    BackColor = Color.FromArgb(108, 117, 125),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Location = new Point(180, 115),
                    Size = new Size(180, 42),
                    Cursor = Cursors.Hand,
                    DialogResult = DialogResult.Ignore
                };
                btnSilence.FlatAppearance.BorderSize = 0;

                var btnContinue = new Button
                {
                    Text = "▶️ Continuar",
                    Font = new Font("Segoe UI", 9F),
                    Location = new Point(370, 115),
                    Size = new Size(120, 42),
                    Cursor = Cursors.Hand,
                    DialogResult = DialogResult.OK
                };

                promptForm.Controls.Add(lbl);
                promptForm.Controls.Add(btnStop);
                promptForm.Controls.Add(btnSilence);
                promptForm.Controls.Add(btnContinue);

                DialogResult result = promptForm.ShowDialog(this);
                isThresholdPromptActive = false;

                if (result == DialogResult.Abort)
                {
                    syncService.StopCurrentSync();
                }
                else if (result == DialogResult.Ignore)
                {
                    syncService.SilenceAlerts = true;
                    syncService.AppendLog("[OPCION] Se han silenciado las alertas emergentes para esta sesion. Los errores continúan guardandose en 'sync_errors.log'.", DbSyncService.LogLevel.Info);
                }
            }
        }

        private void TimerHealth_Tick(object? sender, EventArgs e)
        {
            syncService.CheckLogFilesForNewLines();
        }

        private void OpenLogFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show($"El archivo de log no existe todavia: {path}", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo log: {ex.Message}", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowForm()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
        }

        private void ExitApplication()
        {
            forceClose = true;
            trayIcon.Visible = false;
            trayIcon.Dispose();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!forceClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                trayIcon.ShowBalloonTip(2000, "ReprediSL V4", "El demonio continua ejecutandose en segundo plano en la barra de tareas.", ToolTipIcon.Info);
            }
            else
            {
                base.OnFormClosing(e);
            }
        }
    }
}
