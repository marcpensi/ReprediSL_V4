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
        private Panel pnlHeader = null!;
        private FlowLayoutPanel flowButtons = null!;
        private TableLayoutPanel pnlStatusInfo = null!;

        // Botones de acción principales
        private Button btnAck = null!;
        private Button btnAutoToggle = null!;
        private Button btnViewErrors = null!;
        private Button btnExport = null!;
        private Button btnPrint = null!;
        private Button btnClear = null!;
        private Button btnSizeToggle = null!;
        private Button btnThemeToggle = null!;
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
        private string currentTheme = "Oscuro";     // "Oscuro", "Claro"
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
            ApplyTheme(currentTheme);
            UpdateStatusLabels();

            syncService.AppendLog("Demonio de bandeja ReprediSL V4 C# NATIVO iniciado.", DbSyncService.LogLevel.Success);
            syncService.AppendLog($"PsGest Target: {syncService.MdbPath}", DbSyncService.LogLevel.Info);

            timerHealth = new System.Windows.Forms.Timer { Interval = 2000 };
            timerHealth.Tick += TimerHealth_Tick;
            timerHealth.Start();
        }

        private void InitializeComponentCustom()
        {
            this.Text = "ReprediSL V4 - Demonio de Pedidos & Sincronización";
            this.MinimumSize = new Size(980, 620);
            this.Size = new Size(1200, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Application;

            // Panel Header Superior
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 10, 12, 10)
            };

            // FlowLayoutPanel para botones responsivos
            flowButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 0, 4),
                WrapContents = true
            };

            // 1. Boton Confirmar Pedidos
            btnAck = CreateModernButton("🟢 CONFIRMAR PEDIDOS", Color.FromArgb(16, 185, 129), (s, e) =>
                MessageBox.Show("No hay pedidos pendientes de confirmación.", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information), 195);

            // 2. Boton Conmutar Auto-Aceptar
            btnAutoToggle = CreateModernButton("⚙️ CONFIRMACIÓN MANUAL", Color.FromArgb(99, 102, 241), (s, e) => ToggleAutoAcceptMode(), 225);

            // 3. Boton Ver Errores
            btnViewErrors = CreateModernButton("🚨 VER ERRORES (0)", Color.FromArgb(71, 85, 105), (s, e) => MostrarVentanaErrores(), 175);

            // 4. Boton Exportar Log
            btnExport = CreateModernButton("💾 Exportar Log", Color.FromArgb(14, 165, 233), (s, e) => ExportarRegistro(), 135);

            // 5. Boton Imprimir Log
            btnPrint = CreateModernButton("🖨️ Imprimir", Color.FromArgb(139, 92, 246), (s, e) => ImprimirRegistro(), 120);

            // 6. Boton Limpiar Log
            btnClear = CreateModernButton("🗑️ Limpiar", Color.FromArgb(225, 29, 72), (s, e) => LimpiarRegistroConConfirmacion(), 115);

            // 7. Boton Selector de Tamaño de Fuente
            btnSizeToggle = CreateModernButton("🔤 Fuente: GRANDE", Color.FromArgb(51, 65, 85), (s, e) => CycleFontSize(), 160);

            // 8. Boton Conmutar Tema (Claro / Oscuro)
            btnThemeToggle = CreateModernButton("🌗 Tema: OSCURO", Color.FromArgb(71, 85, 105), (s, e) => ToggleTheme(), 145);

            // 9. Boton Sincronizar Ahora
            btnSync = CreateModernButton("⚡ Sincronizar PostgreSQL", Color.FromArgb(6, 182, 212), async (s, e) => await DoManualSyncAsync(), 200);

            flowButtons.Controls.Add(btnAck);
            flowButtons.Controls.Add(btnAutoToggle);
            flowButtons.Controls.Add(btnViewErrors);
            flowButtons.Controls.Add(btnExport);
            flowButtons.Controls.Add(btnPrint);
            flowButtons.Controls.Add(btnClear);
            flowButtons.Controls.Add(btnSizeToggle);
            flowButtons.Controls.Add(btnThemeToggle);
            flowButtons.Controls.Add(btnSync);

            // TableLayoutPanel para etiquetas de estado responsivas
            pnlStatusInfo = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 6, 4, 4)
            };
            pnlStatusInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlStatusInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            lblPendientes = new Label
            {
                Text = "Sin pedidos pendientes",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 4)
            };

            lblErrorStatus = new Label
            {
                Text = "Incidencias: 0 errores | 0 advertencias (Sin errores)",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 4)
            };

            lblRutaTarget = new Label
            {
                Text = "Destino ERP PsGest: ",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 2)
            };
            pnlStatusInfo.SetColumnSpan(lblRutaTarget, 2);

            pnlStatusInfo.Controls.Add(lblPendientes, 0, 0);
            pnlStatusInfo.Controls.Add(lblErrorStatus, 1, 0);
            pnlStatusInfo.Controls.Add(lblRutaTarget, 0, 1);

            pnlHeader.Controls.Add(pnlStatusInfo);
            pnlHeader.Controls.Add(flowButtons);

            // Consola Log RichTextBox (Estilo Terminal)
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = GetConsolasFont(12.5f),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(12)
            };

            this.Controls.Add(txtLog);
            this.Controls.Add(pnlHeader);

            // ContextMenuStrip para la Bandeja de Sistema (Boton Derecho en el Reloj)
            trayMenu = new ContextMenuStrip
            {
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular)
            };

            var itemTitle = new ToolStripMenuItem("🖥️ Demonio ReprediSL V4") { Enabled = false, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
            trayMenu.Items.Add(itemTitle);
            trayMenu.Items.Add("-");

            trayMenu.Items.Add("👁️ Ver Registro / Log en Vivo", null, (s, e) => ShowForm());
            trayMenu.Items.Add("⚡ Ejecutar Sincronización Ahora", null, async (s, e) => await DoManualSyncAsync());
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("🟢 Gestionar / Aceptar Pedidos Pendientes", null, (s, e) => MessageBox.Show("No hay pedidos pendientes de confirmación.", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information));
            trayMenu.Items.Add("⚙️ Alternar Modo Auto-Aceptar", null, (s, e) => ToggleAutoAcceptMode());
            trayMenu.Items.Add("🚨 Ver Log Especial de Errores (sync_errors.log)", null, (s, e) => MostrarVentanaErrores());
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("💾 Exportar Registro (Guardar como...)", null, (s, e) => ExportarRegistro());
            trayMenu.Items.Add("🖨️ Imprimir Registro", null, (s, e) => ImprimirRegistro());
            trayMenu.Items.Add("🗑️ Limpiar Registro (Con Copia de Seg.)", null, (s, e) => LimpiarRegistroConConfirmacion());

            // Submenu Tamaño de Letra e Interfaz y Tema
            var itemSizeMenu = new ToolStripMenuItem("🔤 Tamaño de Letra & Tema");
            itemSizeMenu.DropDownItems.Add("Pequeño (100%)", null, (s, e) => ApplyFontSize("Pequeno"));
            itemSizeMenu.DropDownItems.Add("Mediano (125%)", null, (s, e) => ApplyFontSize("Mediano"));
            itemSizeMenu.DropDownItems.Add("Grande (150%)", null, (s, e) => ApplyFontSize("Grande"));
            itemSizeMenu.DropDownItems.Add("-");
            itemSizeMenu.DropDownItems.Add("🌞 Modo Claro (Light Theme)", null, (s, e) => ApplyTheme("Claro"));
            itemSizeMenu.DropDownItems.Add("🌙 Modo Oscuro (Dark Theme)", null, (s, e) => ApplyTheme("Oscuro"));
            trayMenu.Items.Add(itemSizeMenu);

            trayMenu.Items.Add("-");

            // Submenu de Pruebas y Simulaciones
            var itemSimMenu = new ToolStripMenuItem("🧪 Pruebas y Simulaciones");
            itemSimMenu.DropDownItems.Add("📦 Simular Llegada de Pedido (Prueba)", null, (s, e) =>
            {
                syncService.AppendLog("[NUEVO PEDIDO] Recibido pedido N. TEST-001 | Cliente: 1001 (CLIENTE DE PRUEBA SL) | Importe: 450,00 EUR", DbSyncService.LogLevel.Success);
                MessageBox.Show("Simulación de llegada de pedido registrada en el log.", "Prueba de Demonio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            itemSimMenu.DropDownItems.Add("🚨 Simular Error de Sync (Prueba Alerta Rojo)", null, (s, e) =>
            {
                syncService.AppendLog("[ERROR] Fallo de prueba simulado: Conexión intermitente con la base de datos.", DbSyncService.LogLevel.Error);
            });
            itemSimMenu.DropDownItems.Add("💥 Simular Ráfaga de Errores (Prueba Incremento Rápido)", null, (s, e) =>
            {
                for (int i = 1; i <= 4; i++)
                {
                    syncService.AppendLog($"[ERROR] Ráfaga de incidencia #{i}: Simulación de fallo en lote {i * 500}", DbSyncService.LogLevel.Error);
                }
            });
            trayMenu.Items.Add(itemSimMenu);

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

        private Button CreateModernButton(string text, Color baseColor, EventHandler onClick, int width = 160)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, 36),
                Margin = new Padding(0, 0, 8, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = baseColor,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;

            Color hoverColor = Color.FromArgb(
                Math.Min(255, baseColor.R + 25),
                Math.Min(255, baseColor.G + 25),
                Math.Min(255, baseColor.B + 25)
            );
            btn.MouseEnter += (s, e) => btn.BackColor = hoverColor;
            btn.MouseLeave += (s, e) => btn.BackColor = baseColor;

            return btn;
        }

        private Font GetConsolasFont(float size)
        {
            try
            {
                return new Font("Cascadia Code", size, FontStyle.Bold);
            }
            catch
            {
                return new Font("Consolas", size, FontStyle.Bold);
            }
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
            GuardarConfiguracionUI();

            float logFontSize = 12.5f;
            float btnFontSize = 9.5f;
            float statusFontSize = 10.5f;
            float targetFontSize = 12.5f;
            int formWidth = 1200;
            int formHeight = 750;

            switch (size)
            {
                case "Pequeno":
                    logFontSize = 9.5f;
                    btnFontSize = 8.5f;
                    statusFontSize = 9f;
                    targetFontSize = 11f;
                    formWidth = 980;
                    formHeight = 620;
                    btnSizeToggle.Text = "🔤 Fuente: PEQUEÑO";
                    break;
                case "Mediano":
                    logFontSize = 11f;
                    btnFontSize = 9f;
                    statusFontSize = 10f;
                    targetFontSize = 12f;
                    formWidth = 1080;
                    formHeight = 680;
                    btnSizeToggle.Text = "🔤 Fuente: MEDIANO";
                    break;
                default: // Grande
                    logFontSize = 12.5f;
                    btnFontSize = 9.5f;
                    statusFontSize = 10.5f;
                    targetFontSize = 12.5f;
                    formWidth = 1200;
                    formHeight = 750;
                    btnSizeToggle.Text = "🔤 Fuente: GRANDE";
                    break;
            }

            this.Size = new Size(formWidth, formHeight);
            txtLog.Font = GetConsolasFont(logFontSize);

            Font btnFont = new Font("Segoe UI", btnFontSize, FontStyle.Bold);
            btnAck.Font = btnFont;
            btnAutoToggle.Font = btnFont;
            btnViewErrors.Font = btnFont;
            btnExport.Font = btnFont;
            btnPrint.Font = btnFont;
            btnClear.Font = btnFont;
            btnSizeToggle.Font = btnFont;
            btnThemeToggle.Font = btnFont;
            btnSync.Font = btnFont;

            lblPendientes.Font = new Font("Segoe UI", statusFontSize, FontStyle.Bold);
            lblErrorStatus.Font = new Font("Segoe UI", statusFontSize, FontStyle.Bold);
            lblRutaTarget.Font = new Font("Segoe UI", targetFontSize, FontStyle.Bold);
        }

        private void ToggleTheme()
        {
            if (currentTheme == "Oscuro") ApplyTheme("Claro");
            else ApplyTheme("Oscuro");
        }

        private void ApplyTheme(string theme)
        {
            currentTheme = theme;
            GuardarConfiguracionUI();

            if (theme == "Claro")
            {
                this.BackColor = Color.FromArgb(241, 245, 249);
                pnlHeader.BackColor = Color.FromArgb(226, 232, 240);
                txtLog.BackColor = Color.White;
                txtLog.ForeColor = Color.FromArgb(15, 23, 42);

                lblPendientes.ForeColor = Color.FromArgb(51, 65, 85);
                lblRutaTarget.ForeColor = Color.FromArgb(2, 132, 199);

                var lightRenderer = new LightMenuRenderer();
                trayMenu.Renderer = lightRenderer;
                trayMenu.BackColor = Color.White;
                trayMenu.ForeColor = Color.FromArgb(15, 23, 42);

                ApplyMenuThemeRecursive(trayMenu.Items, lightRenderer, Color.White, Color.FromArgb(15, 23, 42));

                btnThemeToggle.Text = "🌞 Tema: CLARO";
                btnThemeToggle.BackColor = Color.FromArgb(203, 213, 225);
                btnThemeToggle.ForeColor = Color.FromArgb(15, 23, 42);
            }
            else // Oscuro (Slate High Contrast Dark)
            {
                this.BackColor = Color.FromArgb(15, 23, 42);
                pnlHeader.BackColor = Color.FromArgb(30, 41, 59);
                txtLog.BackColor = Color.FromArgb(2, 6, 23);
                txtLog.ForeColor = Color.FromArgb(52, 211, 153);

                lblPendientes.ForeColor = Color.FromArgb(203, 213, 225);
                lblRutaTarget.ForeColor = Color.FromArgb(56, 189, 248);

                var darkRenderer = new DarkMenuRenderer();
                trayMenu.Renderer = darkRenderer;
                trayMenu.BackColor = Color.FromArgb(30, 41, 59);
                trayMenu.ForeColor = Color.White;

                ApplyMenuThemeRecursive(trayMenu.Items, darkRenderer, Color.FromArgb(30, 41, 59), Color.White);

                btnThemeToggle.Text = "🌙 Tema: OSCURO";
                btnThemeToggle.BackColor = Color.FromArgb(51, 65, 85);
                btnThemeToggle.ForeColor = Color.White;
            }

            UpdateStatusLabels();
        }

        private void ApplyMenuThemeRecursive(ToolStripItemCollection items, ToolStripRenderer renderer, Color backColor, Color foreColor)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    menuItem.DropDown.Renderer = renderer;
                    menuItem.DropDown.BackColor = backColor;
                    menuItem.DropDown.ForeColor = foreColor;
                    if (menuItem.HasDropDownItems)
                    {
                        ApplyMenuThemeRecursive(menuItem.DropDownItems, renderer, backColor, foreColor);
                    }
                }
            }
        }

        private void CargarConfiguracionUI()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("TamanoFuente", out var elemFuente))
                    {
                        currentFontSize = elemFuente.GetString() ?? "Grande";
                    }
                    if (doc.RootElement.TryGetProperty("AutoAceptar", out var elemAuto))
                    {
                        autoAcceptMode = elemAuto.GetBoolean();
                    }
                    if (doc.RootElement.TryGetProperty("TemaInterfaz", out var elemTema))
                    {
                        currentTheme = elemTema.GetString() ?? "Oscuro";
                    }
                }
            }
            catch { }
        }

        private void GuardarConfiguracionUI()
        {
            try
            {
                var data = new
                {
                    TamanoFuente = currentFontSize,
                    AutoAceptar = autoAcceptMode,
                    TemaInterfaz = currentTheme
                };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsFilePath, json);
            }
            catch { }
        }

        private void ToggleAutoAcceptMode()
        {
            autoAcceptMode = !autoAcceptMode;
            GuardarConfiguracionUI();

            if (autoAcceptMode)
            {
                btnAutoToggle.Text = "⚙️ AUTO-ACEPTAR ACTIVO";
                btnAutoToggle.BackColor = Color.FromArgb(16, 185, 129);
                syncService.AppendLog("[CONFIG] Auto-aceptación de pedidos ACTIVADA.", DbSyncService.LogLevel.Info);
            }
            else
            {
                btnAutoToggle.Text = "⚙️ CONFIRMACIÓN MANUAL";
                btnAutoToggle.BackColor = Color.FromArgb(99, 102, 241);
                syncService.AppendLog("[CONFIG] Auto-aceptación de pedidos DESACTIVADA (Modo Confirmación Manual).", DbSyncService.LogLevel.Info);
            }
        }

        private void UpdateStatusLabels()
        {
            lblPendientes.Text = "Sin pedidos pendientes";

            if (currentTheme == "Claro")
            {
                lblPendientes.ForeColor = Color.FromArgb(51, 65, 85);
                lblRutaTarget.ForeColor = Color.FromArgb(2, 132, 199);
            }
            else
            {
                lblPendientes.ForeColor = Color.FromArgb(203, 213, 225);
                lblRutaTarget.ForeColor = Color.FromArgb(56, 189, 248);
            }

            if (autoAcceptMode)
            {
                btnAutoToggle.Text = "⚙️ AUTO-ACEPTAR ACTIVO";
                btnAutoToggle.BackColor = Color.FromArgb(16, 185, 129);
            }
            else
            {
                btnAutoToggle.Text = "⚙️ CONFIRMACIÓN MANUAL";
                btnAutoToggle.BackColor = Color.FromArgb(99, 102, 241);
            }

            if (syncService.ErrorCount > 0)
            {
                lblErrorStatus.Text = $"Incidencias: {syncService.ErrorCount} errores/alertas acumuladas";
                lblErrorStatus.ForeColor = Color.FromArgb(244, 63, 94);
                btnViewErrors.Text = $"🚨 VER ERRORES ({syncService.ErrorCount})";
                btnViewErrors.BackColor = Color.FromArgb(225, 29, 72);
            }
            else
            {
                lblErrorStatus.Text = "Incidencias: 0 errores | 0 advertencias (Sin errores)";
                lblErrorStatus.ForeColor = Color.FromArgb(52, 211, 153);
                btnViewErrors.Text = "🚨 VER ERRORES (0)";
                btnViewErrors.BackColor = Color.FromArgb(71, 85, 105);
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

            Color logColor;
            if (currentTheme == "Claro")
            {
                logColor = level switch
                {
                    DbSyncService.LogLevel.Error => Color.FromArgb(220, 38, 38),   // Dark Red
                    DbSyncService.LogLevel.Warning => Color.FromArgb(217, 119, 6), // Amber Dark
                    DbSyncService.LogLevel.Success => Color.FromArgb(5, 150, 105), // Emerald Dark
                    _ => Color.FromArgb(2, 132, 199)                               // Blue Info
                };
            }
            else
            {
                logColor = level switch
                {
                    DbSyncService.LogLevel.Error => Color.FromArgb(244, 63, 94),   // Rose Red
                    DbSyncService.LogLevel.Warning => Color.FromArgb(245, 158, 11), // Amber Warning
                    DbSyncService.LogLevel.Success => Color.FromArgb(52, 211, 153), // Emerald Green
                    _ => Color.FromArgb(56, 189, 248)                                // Sky Blue Info
                };
            }

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
                $"🚨 Se han detectado {count} incidencias consecutivas en la sincronización.\n\n" +
                "¿Deseas DETENER la sincronización actual?\n\n" +
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
                MessageBox.Show("El registro está vacío. No hay datos para exportar.", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Exportar Registro de Sincronización y Pedidos",
                Filter = "Archivos de texto (*.txt)|*.txt|Archivos de Log (*.log)|*.log|Todos los archivos (*.*)|*.*",
                FileName = $"Registro_ReprediSL_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, txtLog.Text, System.Text.Encoding.UTF8);
                    MessageBox.Show($"Registro exportado exitosamente en:\n{sfd.FileName}", "Exportación Completa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exportando el archivo: {ex.Message}", "Error de Exportación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImprimirRegistro()
        {
            if (string.IsNullOrWhiteSpace(txtLog.Text))
            {
                MessageBox.Show("El registro está vacío. No hay datos para imprimir.", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show("El registro ya está vacío.", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            Color dialogBg = currentTheme == "Claro" ? Color.FromArgb(254, 242, 242) : Color.FromArgb(24, 15, 20);
            Color topBg = currentTheme == "Claro" ? Color.FromArgb(254, 226, 226) : Color.FromArgb(38, 20, 28);
            Color textBg = currentTheme == "Claro" ? Color.White : Color.FromArgb(15, 10, 14);

            using var errForm = new Form
            {
                Text = "ReprediSL V4 - Log Especial de Errores e Incidencias (sync_errors.log)",
                StartPosition = FormStartPosition.CenterParent,
                BackColor = dialogBg,
                Size = new Size(1050, 650)
            };

            var errTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 55,
                BackColor = topBg
            };

            var lblSummary = new Label
            {
                Text = $"Resumen de Incidencias: {syncService.ErrorCount} Registradas",
                Location = new Point(14, 14),
                AutoSize = true,
                ForeColor = Color.FromArgb(225, 29, 72),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };

            var btnClearErr = new Button
            {
                Text = "🗑️ Limpiar Log de Errores",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(225, 29, 72),
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
                BackColor = textBg,
                ForeColor = currentTheme == "Claro" ? Color.FromArgb(15, 23, 42) : Color.FromArgb(254, 205, 211),
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
                            txtErr.SelectionColor = line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("Fallo", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(225, 29, 72) : Color.FromArgb(217, 119, 6);
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
                trayIcon.ShowBalloonTip(2000, "ReprediSL V4", "El demonio sigue ejecutándose en segundo plano en la barra de tareas.", ToolTipIcon.Info);
            }
            else
            {
                base.OnFormClosing(e);
            }
        }
    }

    // Renderizador Claro Personalizado para ContextMenuStrip
    public class LightMenuRenderer : ToolStripProfessionalRenderer
    {
        public LightMenuRenderer() : base(new LightColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.FromArgb(15, 23, 42) : Color.FromArgb(148, 163, 184);
            base.OnRenderItemText(e);
        }
    }

    public class LightColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(226, 232, 240);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(226, 232, 240);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(226, 232, 240);
        public override Color MenuItemBorder => Color.FromArgb(203, 213, 225);
        public override Color MenuBorder => Color.FromArgb(203, 213, 225);
        public override Color ToolStripDropDownBackground => Color.FromArgb(255, 255, 255);
        public override Color ImageMarginGradientBegin => Color.FromArgb(255, 255, 255);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(255, 255, 255);
        public override Color ImageMarginGradientEnd => Color.FromArgb(255, 255, 255);
        public override Color SeparatorDark => Color.FromArgb(203, 213, 225);
        public override Color SeparatorLight => Color.Transparent;
    }

    // Renderizador Oscuro Personalizado para ContextMenuStrip (Letra Blanca 100% Nitida)
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.FromArgb(255, 255, 255) : Color.FromArgb(148, 163, 184);
            base.OnRenderItemText(e);
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(51, 65, 85);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(51, 65, 85);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(51, 65, 85);
        public override Color MenuItemBorder => Color.FromArgb(71, 85, 105);
        public override Color MenuBorder => Color.FromArgb(51, 65, 85);
        public override Color ToolStripDropDownBackground => Color.FromArgb(30, 41, 59);
        public override Color ImageMarginGradientBegin => Color.FromArgb(30, 41, 59);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 41, 59);
        public override Color ImageMarginGradientEnd => Color.FromArgb(30, 41, 59);
        public override Color SeparatorDark => Color.FromArgb(71, 85, 105);
        public override Color SeparatorLight => Color.Transparent;
    }
}
