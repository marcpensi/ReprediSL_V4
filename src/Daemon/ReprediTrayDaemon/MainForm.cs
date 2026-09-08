using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
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

        // Botones de acción principales
        private Button btnAck = null!;
        private Button btnAutoToggle = null!;
        private Button btnViewErrors = null!;
        private Button btnExport = null!;
        private Button btnPrint = null!;
        private Button btnClear = null!;
        private Button btnSizeToggle = null!;
        private Button btnSync = null!;

        // Etiquetas de estado
        private Label lblPendientes = null!;
        private Label lblErrorStatus = null!;
        private Label lblRutaTarget = null!;

        private DbSyncService syncService = null!;
        private System.Windows.Forms.Timer timerHealth = null!;
        private bool forceClose = false;

        private bool autoAcceptMode = false;
        private string currentFontSize = "Grande"; // "Pequeno", "Mediano", "Grande"
        private string settingsFilePath = string.Empty;

        public MainForm()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));
            if (!File.Exists(Path.Combine(projectRoot, "AGENTS.md")))
            {
                projectRoot = @"D:\programacio\repredi\ReprediSL_V4";
            }

            settingsFilePath = Path.Combine(projectRoot, "src", "Access", "daemon_ui_settings.json");

            InitializeComponentCustom();

            syncService = new DbSyncService(projectRoot);
            syncService.OnLogMessage += SyncService_OnLogMessage;
            syncService.OnErrorThresholdExceeded += SyncService_OnErrorThresholdExceeded;

            CargarConfiguracionUI();
            ApplyFontSize(currentFontSize);
            UpdateStatusLabels();

            syncService.AppendLog("Demonio de bandeja ReprediSL V4 C# NATIVO iniciado.", DbSyncService.LogLevel.Success);
            syncService.AppendLog($"PsGest Target: {syncService.MdbPath}", DbSyncService.LogLevel.Info);

            timerHealth = new System.Windows.Forms.Timer { Interval = 2000 };
            timerHealth.Tick += TimerHealth_Tick;
            timerHealth.Start();
        }

        private void InitializeComponentCustom()
        {
            this.Text = "ReprediSL V4 - Demonio de Pedidos (Alertas de Error en ROJO & Log de Incidencias)";
            this.Size = new Size(1180, 740);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 19, 22);
            this.Icon = SystemIcons.Application;

            // Panel Superior
            pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                BackColor = Color.FromArgb(28, 30, 36)
            };

            // 1. Boton Confirmar Pedidos
            btnAck = new Button
            {
                Text = " CONFIRMAR PEDIDOS",
                Size = new Size(200, 38),
                Location = new Point(12, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, 140, 75),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnAck.FlatAppearance.BorderSize = 0;
            btnAck.Click += (s, e) => MessageBox.Show("No hay pedidos pendientes de confirmacion.", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 2. Boton Conmutar Auto-Aceptar
            btnAutoToggle = new Button
            {
                Text = "Modo: CONFIRMACION MANUAL",
                Size = new Size(230, 38),
                Location = new Point(220, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 60, 80),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnAutoToggle.FlatAppearance.BorderSize = 0;
            btnAutoToggle.Click += (s, e) => ToggleAutoAcceptMode();

            // 3. Boton Ver Errores
            btnViewErrors = new Button
            {
                Text = "🚨 VER ERRORES (0)",
                Size = new Size(185, 38),
                Location = new Point(460, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 70, 80),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnViewErrors.FlatAppearance.BorderSize = 0;
            btnViewErrors.Click += (s, e) => MostrarVentanaErrores();

            // 4. Boton Exportar Log
            btnExport = new Button
            {
                Text = "💾 Exportar Log",
                Size = new Size(125, 38),
                Location = new Point(655, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 90, 140),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += (s, e) => ExportarRegistro();

            // 5. Boton Imprimir Log
            btnPrint = new Button
            {
                Text = "🖨️ Imprimir",
                Size = new Size(110, 38),
                Location = new Point(788, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(100, 70, 130),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnPrint.FlatAppearance.BorderSize = 0;
            btnPrint.Click += (s, e) => ImprimirRegistro();

            // 6. Boton Limpiar Log
            btnClear = new Button
            {
                Text = "🗑️ Limpiar",
                Size = new Size(110, 38),
                Location = new Point(906, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(140, 50, 50),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) => LimpiarRegistroConConfirmacion();

            // 7. Boton Selector de Tamaño de Fuente
            btnSizeToggle = new Button
            {
                Text = "Fuente: GRANDE",
                Size = new Size(145, 38),
                Location = new Point(1024, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 90, 110),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnSizeToggle.FlatAppearance.BorderSize = 0;
            btnSizeToggle.Click += (s, e) => CycleFontSize();

            // 8. Boton Sincronizar Ahora
            btnSync = new Button
            {
                Text = "⚡ Sincronizar PostgreSQL",
                Size = new Size(180, 32),
                Location = new Point(12, 54),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnSync.FlatAppearance.BorderSize = 0;
            btnSync.Click += async (s, e) => await DoManualSyncAsync();

            // Etiquetas de estado
            lblPendientes = new Label
            {
                Text = "Sin pedidos pendientes",
                Location = new Point(200, 58),
                AutoSize = true,
                ForeColor = Color.FromArgb(220, 220, 220)
            };

            lblErrorStatus = new Label
            {
                Text = "Incidencias: 0 errores | 0 advertencias (Sin errores)",
                Location = new Point(460, 58),
                AutoSize = true,
                ForeColor = Color.FromArgb(53, 189, 105)
            };

            lblRutaTarget = new Label
            {
                Text = "Destino ERP PsGest: ",
                Location = new Point(12, 90),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 180, 240)
            };

            pnlTop.Controls.Add(btnAck);
            pnlTop.Controls.Add(btnAutoToggle);
            pnlTop.Controls.Add(btnViewErrors);
            pnlTop.Controls.Add(btnExport);
            pnlTop.Controls.Add(btnPrint);
            pnlTop.Controls.Add(btnClear);
            pnlTop.Controls.Add(btnSizeToggle);
            pnlTop.Controls.Add(btnSync);
            pnlTop.Controls.Add(lblPendientes);
            pnlTop.Controls.Add(lblErrorStatus);
            pnlTop.Controls.Add(lblRutaTarget);

            // Consola Log RichTextBox (Estilo Oscuro Emerald)
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(12, 13, 15),
                ForeColor = Color.FromArgb(53, 189, 105),
                Font = new Font("Consolas", 12F, FontStyle.Bold),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10)
            };

            this.Controls.Add(txtLog);
            this.Controls.Add(pnlTop);

            // Menu Tray Icon
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("🖥️ Abrir Monitoreo Visual", null, (s, e) => ShowForm());
            trayMenu.Items.Add("⚡ Sincronizar PostgreSQL Ahora", null, async (s, e) => await DoManualSyncAsync());
            trayMenu.Items.Add("🚨 Ver Registro de Errores", null, (s, e) => MostrarVentanaErrores());
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("❌ Salir del Demonio", null, (s, e) => ExitApplication());

            // System Tray Icon
            trayIcon = new NotifyIcon
            {
                Text = "ReprediSL V4 - Demonio de Pedidos",
                Icon = SystemIcons.Information,
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => ShowForm();
        }

        private void CycleFontSize()
        {
            if (currentFontSize == "Grande") ApplyFontSize("Pequeno");
            else if (currentFontSize == "Pequeno") ApplyFontSize("Mediano");
            else ApplyFontSize("Grande");
        }

        private void ApplyFontSize(string size)
        {
            currentFontSize = size;
            GuardarConfiguracionUI(size);

            float logFontSize = 12f;
            float btnFontSize = 10f;
            float statusFontSize = 10.5f;
            float targetFontSize = 12.5f;
            int formWidth = 1180;
            int formHeight = 740;

            switch (size)
            {
                case "Pequeno":
                    logFontSize = 9.5f;
                    btnFontSize = 8.5f;
                    statusFontSize = 9f;
                    targetFontSize = 11f;
                    formWidth = 940;
                    formHeight = 580;
                    btnSizeToggle.Text = "Fuente: PEQUEÑO";
                    break;
                case "Mediano":
                    logFontSize = 11f;
                    btnFontSize = 9.5f;
                    statusFontSize = 10f;
                    targetFontSize = 12f;
                    formWidth = 1060;
                    formHeight = 660;
                    btnSizeToggle.Text = "Fuente: MEDIANO";
                    break;
                default: // Grande
                    logFontSize = 12.5f;
                    btnFontSize = 10f;
                    statusFontSize = 10.5f;
                    targetFontSize = 12.5f;
                    formWidth = 1180;
                    formHeight = 740;
                    btnSizeToggle.Text = "Fuente: GRANDE";
                    break;
            }

            this.Size = new Size(formWidth, formHeight);
            txtLog.Font = new Font("Consolas", logFontSize, FontStyle.Bold);

            Font btnFont = new Font("Segoe UI", btnFontSize, FontStyle.Bold);
            btnAck.Font = btnFont;
            btnAutoToggle.Font = btnFont;
            btnViewErrors.Font = btnFont;
            btnExport.Font = btnFont;
            btnPrint.Font = btnFont;
            btnClear.Font = btnFont;
            btnSizeToggle.Font = btnFont;
            btnSync.Font = new Font("Segoe UI", btnFontSize - 0.5f, FontStyle.Bold);

            lblPendientes.Font = new Font("Segoe UI", statusFontSize, FontStyle.Bold);
            lblErrorStatus.Font = new Font("Segoe UI", statusFontSize, FontStyle.Bold);
            lblRutaTarget.Font = new Font("Segoe UI", targetFontSize, FontStyle.Bold);
        }

        private void CargarConfiguracionUI()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("TamanoFuente", out var elem))
                    {
                        currentFontSize = elem.GetString() ?? "Grande";
                    }
                }
            }
            catch { }
        }

        private void GuardarConfiguracionUI(string size)
        {
            try
            {
                var data = new { TamanoFuente = size };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsFilePath, json);
            }
            catch { }
        }

        private void ToggleAutoAcceptMode()
        {
            autoAcceptMode = !autoAcceptMode;
            if (autoAcceptMode)
            {
                btnAutoToggle.Text = "Modo: AUTO-ACEPTAR";
                btnAutoToggle.BackColor = Color.FromArgb(38, 140, 75);
            }
            else
            {
                btnAutoToggle.Text = "Modo: CONFIRMACION MANUAL";
                btnAutoToggle.BackColor = Color.FromArgb(50, 60, 80);
            }
        }

        private void UpdateStatusLabels()
        {
            lblPendientes.Text = "Sin pedidos pendientes";
            lblPendientes.ForeColor = Color.FromArgb(220, 220, 220);

            if (syncService.ErrorCount > 0)
            {
                lblErrorStatus.Text = $"Incidencias: {syncService.ErrorCount} errores/alertas acumuladas";
                lblErrorStatus.ForeColor = Color.FromArgb(255, 100, 100);
                btnViewErrors.Text = $"🚨 VER ERRORES ({syncService.ErrorCount})";
                btnViewErrors.BackColor = Color.FromArgb(180, 40, 40);
            }
            else
            {
                lblErrorStatus.Text = "Incidencias: 0 errores | 0 advertencias (Sin errores)";
                lblErrorStatus.ForeColor = Color.FromArgb(53, 189, 105);
                btnViewErrors.Text = "🚨 VER ERRORES (0)";
                btnViewErrors.BackColor = Color.FromArgb(70, 70, 80);
            }

            lblRutaTarget.Text = "Destino ERP PsGest: " + syncService.MdbPath;
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
                _ => Color.FromArgb(100, 180, 240) // Azul acero brillante para tildes e info
            };

            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = logColor;
            txtLog.AppendText(message + Environment.NewLine);
            txtLog.SelectionColor = txtLog.ForeColor;
            txtLog.ScrollToCaret();

            UpdateStatusLabels();

            if (level == DbSyncService.LogLevel.Error)
            {
                trayIcon.ShowBalloonTip(3000, "ReprediSL V4 - Error", message, ToolTipIcon.Error);
            }
        }

        private void SyncService_OnErrorThresholdExceeded(int count)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SyncService_OnErrorThresholdExceeded(count)));
                return;
            }

            UpdateStatusLabels();

            var dialogResult = MessageBox.Show(
                $"🚨 Se han detectado {count} incidencias consecutivas en la sincronizacion.\n\n" +
                "¿Deseas DETENER la sincronizacion actual?\n\n" +
                "[Sí] Detener Proceso\n" +
                "[No] Silenciar alertas visuales y continuar\n" +
                "[Cancelar] Ignorar por ahora",
                "Alerta de Incidencias Elevadas - ReprediSL V4",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning
            );

            if (dialogResult == DialogResult.Yes)
            {
                syncService.StopCurrentSync();
            }
            else if (dialogResult == DialogResult.No)
            {
                syncService.SilenceAlerts = true;
            }
        }

        private void TimerHealth_Tick(object? sender, EventArgs e)
        {
            syncService.CheckLogFilesForNewLines();
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
                btnSync.Text = "⚡ Sincronizar PostgreSQL";
            }
        }

        private void ExportarRegistro()
        {
            if (string.IsNullOrWhiteSpace(txtLog.Text))
            {
                MessageBox.Show("El registro esta vacio. No hay datos para exportar.", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Exportar Registro de Sincronizacion y Pedidos",
                Filter = "Archivos de texto (*.txt)|*.txt|Archivos de Log (*.log)|*.log|Todos los archivos (*.*)|*.*",
                FileName = $"Registro_ReprediSL_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, txtLog.Text, System.Text.Encoding.UTF8);
                    MessageBox.Show($"Registro exportado exitosamente en:\n{sfd.FileName}", "Exportacion Completa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exportando el archivo: {ex.Message}", "Error de Exportacion", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImprimirRegistro()
        {
            if (string.IsNullOrWhiteSpace(txtLog.Text))
            {
                MessageBox.Show("El registro esta vacio. No hay datos para imprimir.", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using var pd = new System.Drawing.Printing.PrintDocument();
                string textToPrint = txtLog.Text;
                using var printFont = new Font("Consolas", 9F);

                pd.PrintPage += (sender, ev) =>
                {
                    ev.Graphics?.DrawString(textToPrint, printFont, Brushes.Black, 40, 40);
                    ev.HasMorePages = false;
                };

                using var printDialog = new PrintDialog { Document = pd };
                if (printDialog.ShowDialog() == DialogResult.OK)
                {
                    pd.Print();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al imprimir: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LimpiarRegistroConConfirmacion()
        {
            if (string.IsNullOrWhiteSpace(txtLog.Text))
            {
                MessageBox.Show("El registro ya esta vacio.", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var resp = MessageBox.Show(
                "¿Deseas guardar una copia de seguridad del registro antes de limpiarlo?",
                "Limpiar Registro - ReprediSL V4",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question
            );

            if (resp == DialogResult.Cancel) return;

            if (resp == DialogResult.Yes)
            {
                ExportarRegistro();
            }

            txtLog.Clear();
            try { File.WriteAllText(syncService.ProgressLogPath, string.Empty); } catch { }
            MessageBox.Show("El registro ha sido limpiado correctamente.", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MostrarVentanaErrores()
        {
            using var errForm = new Form
            {
                Text = "ReprediSL V4 - Log Especial de Errores e Incidencias (sync_errors.log)",
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(28, 15, 15),
                Size = new Size(1050, 650)
            };

            var errTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 55,
                BackColor = Color.FromArgb(45, 20, 20)
            };

            var lblSummary = new Label
            {
                Text = $"Resumen de Incidencias: {syncService.ErrorCount} Registradas",
                Location = new Point(14, 14),
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 120, 120),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };

            var btnClearErr = new Button
            {
                Text = "🗑️ Limpiar Log de Errores",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(160, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Size = new Size(200, 34),
                Location = new Point(errForm.ClientSize.Width - 220, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            var txtErr = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(18, 10, 10),
                ForeColor = Color.FromArgb(255, 180, 180),
                Font = txtLog.Font,
                BorderStyle = BorderStyle.None
            };

            if (File.Exists(syncService.ErrorLogPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(syncService.ErrorLogPath);
                    if (lines.Length > 0)
                    {
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            txtErr.SelectionStart = txtErr.TextLength;
                            txtErr.SelectionLength = 0;
                            txtErr.SelectionColor = line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("Fallo", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(255, 90, 90) : Color.FromArgb(255, 200, 80);
                            txtErr.AppendText(line + Environment.NewLine);
                        }
                    }
                    else
                    {
                        txtErr.AppendText("Sin errores ni advertencias registradas." + Environment.NewLine);
                    }
                }
                catch (Exception ex)
                {
                    txtErr.AppendText($"Error leyendo log: {ex.Message}");
                }
            }

            btnClearErr.Click += (s, e) =>
            {
                try { File.WriteAllText(syncService.ErrorLogPath, string.Empty); } catch { }
                txtErr.Clear();
                txtErr.AppendText("Log especial de errores limpiado correctamente." + Environment.NewLine);
                syncService.ResetErrorCounter();
                UpdateStatusLabels();
            };

            errTop.Controls.Add(lblSummary);
            errTop.Controls.Add(btnClearErr);
            errForm.Controls.Add(txtErr);
            errForm.Controls.Add(errTop);
            errForm.ShowDialog(this);
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
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!forceClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                trayIcon.ShowBalloonTip(2000, "ReprediSL V4", "El demonio sigue ejecutandose en segundo plano en la barra de tareas.", ToolTipIcon.Info);
            }
            else
            {
                base.OnFormClosing(e);
            }
        }
    }
}
