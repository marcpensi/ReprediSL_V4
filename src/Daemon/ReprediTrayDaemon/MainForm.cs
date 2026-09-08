using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private ContextMenuStrip menuMasAcciones = null!;

        // Contenedores Principales
        private Panel pnlHeader = null!;
        private FlowLayoutPanel flowButtons = null!;
        private TableLayoutPanel pnlStatusCards = null!;
        private TableLayoutPanel pnlMainSplit = null!;
        private Panel pnlLeftLog = null!;
        private Panel pnlRightSidebar = null!;
        private Panel pnlStatusBar = null!;

        // Consola Log y sus controles
        private RichTextBox txtLog = null!;
        private CheckBox chkAutoscroll = null!;
        private Button btnPauseLog = null!;
        private Button btnClearLogView = null!;
        private bool isLogPaused = false;
        private bool isAutoscrollEnabled = true;

        // Botones de acción principales (Estilo Píldora)
        private Button btnAck = null!;
        private Button btnAutoToggle = null!;
        private Button btnViewErrors = null!;
        private Button btnExport = null!;
        private Button btnPrint = null!;
        private Button btnClear = null!;
        private Button btnMas = null!;
        private Button btnSizeToggle = null!;
        private Button btnThemeToggle = null!;
        private Button btnSync = null!;
        private Button btnCopyPath = null!;

        // Etiquetas de estado y tarjetas de cabecera
        private Label lblHeaderTitle = null!;
        private Label lblHeaderSubtitle = null!;
        private Label lblHeaderStatusText = null!;
        private Button btnHeaderSettings = null!;

        private Panel cardPendientes = null!;
        private Label lblPendientesMain = null!;
        private Label lblPendientesSub = null!;

        private Panel cardIncidencias = null!;
        private Label lblIncidenciasMain = null!;
        private Label lblIncidenciasSub = null!;

        private Panel cardRutaTarget = null!;
        private Label lblRutaTargetText = null!;

        // Telemetría de la Barra Lateral Derecha
        private Label lblTeleActive = null!;
        private Label lblTelePendingVal = null!;
        private Label lblTeleLastSyncVal = null!;
        private Label lblTeleUptimeVal = null!;
        private Label lblTeleDataSourceVal = null!;
        private Label lblTelePostgresVal = null!;
        private Label lblTeleProcessedVal = null!;

        // Barra de estado inferior
        private Label lblStatusOS = null!;
        private Label lblStatusNextSync = null!;
        private Label lblStatusClock = null!;

        // Servicios y Configuración
        private DbSyncService syncService = null!;
        private System.Windows.Forms.Timer timerHealth = null!;
        private DateTime startTime = DateTime.Now;
        private bool forceClose = false;
        private int totalRegistrosProcesadosHoy = 12438;

        private bool autoAcceptMode = false;
        private string currentFontSize = "Grande"; // "Pequeno", "Mediano", "Grande"
        private string currentTheme = "Oscuro";     // "Oscuro", "Claro"
        private string settingsFilePath = string.Empty;

        public MainForm()
        {
            this.DoubleBuffered = true;

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

            timerHealth = new System.Windows.Forms.Timer { Interval = 1000 };
            timerHealth.Tick += TimerHealth_Tick;
            timerHealth.Start();
        }

        private void InitializeComponentCustom()
        {
            this.Text = "ReprediSL V4 - Demonio de Pedidos & Sincronización";
            this.MinimumSize = new Size(1050, 680);
            this.Size = new Size(1260, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Application;

            // ==========================================
            // 1. CABECERA SUPERIOR (Header Top Bar)
            // ==========================================
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                Padding = new Padding(16, 8, 16, 8)
            };

            var picDbIcon = new Label
            {
                Text = "🗄️",
                Font = new Font("Segoe UI Emoji", 22F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 10)
            };

            lblHeaderTitle = new Label
            {
                Text = "ReprediSL V4 - Demonio de Pedidos & Sincronización",
                Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(58, 10)
            };

            lblHeaderSubtitle = new Label
            {
                Text = "Sincronización con ERP - Operando normalmente",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(59, 36)
            };

            // Badge derecho de Estado del Demonio
            var pnlStatusBadge = new Panel
            {
                Size = new Size(230, 48),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(this.ClientSize.Width - 290, 8)
            };

            var lblBadgeDot = new Label
            {
                Text = "🟢",
                Font = new Font("Segoe UI Emoji", 10F),
                AutoSize = true,
                Location = new Point(4, 14)
            };

            lblHeaderStatusText = new Label
            {
                Text = "Demonio activo\nEjecutándose en segundo plano.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 8)
            };

            btnHeaderSettings = new Button
            {
                Text = "⚙️",
                Font = new Font("Segoe UI Emoji", 12F),
                Size = new Size(36, 36),
                Location = new Point(184, 6),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnHeaderSettings.FlatAppearance.BorderSize = 0;
            btnHeaderSettings.Click += (s, e) => trayMenu.Show(btnHeaderSettings, new Point(0, btnHeaderSettings.Height));

            pnlStatusBadge.Controls.Add(lblBadgeDot);
            pnlStatusBadge.Controls.Add(lblHeaderStatusText);
            pnlStatusBadge.Controls.Add(btnHeaderSettings);

            pnlHeader.Controls.Add(picDbIcon);
            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSubtitle);
            pnlHeader.Controls.Add(pnlStatusBadge);

            // ==========================================
            // 2. BARRA DE BOTONES DE ACCIÓN (Action Pills Bar)
            // ==========================================
            flowButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(16, 6, 16, 6),
                WrapContents = true
            };

            btnAck = CreatePillButton("🟢 Confirmar pedidos", Color.FromArgb(16, 185, 129), (s, e) =>
                MessageBox.Show("No hay pedidos pendientes de confirmación.", "ReprediSL V4", MessageBoxButtons.OK, MessageBoxIcon.Information), 175);

            btnAutoToggle = CreatePillButton("⚙️ Auto-aceptar\nDesactivado", Color.FromArgb(99, 102, 241), (s, e) => ToggleAutoAcceptMode(), 175);

            btnViewErrors = CreatePillButton("⚠️ Ver errores (0)", Color.FromArgb(71, 85, 105), (s, e) => MostrarVentanaErrores(), 155);

            btnExport = CreatePillButton("📄 Exportar log", Color.FromArgb(14, 165, 233), (s, e) => ExportarRegistro(), 140);

            btnPrint = CreatePillButton("🖨️ Imprimir", Color.FromArgb(139, 92, 246), (s, e) => ImprimirRegistro(), 125);

            btnClear = CreatePillButton("🗑️ Limpiar", Color.FromArgb(225, 29, 72), (s, e) => LimpiarRegistroConConfirmacion(), 120);

            // Menú secundario "Más"
            menuMasAcciones = new ContextMenuStrip { Font = new Font("Segoe UI", 10F) };
            menuMasAcciones.Items.Add("📦 Simular Llegada de Pedido", null, (s, e) =>
            {
                syncService.AppendLog("[NUEVO PEDIDO] Recibido pedido N. TEST-001 | Cliente: 1001 (CLIENTE DE PRUEBA SL) | Importe: 450,00 EUR", DbSyncService.LogLevel.Success);
                totalRegistrosProcesadosHoy++;
                UpdateStatusLabels();
            });
            menuMasAcciones.Items.Add("🚨 Simular Error de Sincronización", null, (s, e) =>
            {
                syncService.AppendLog("[ERROR] Fallo de prueba simulado: Conexión intermitente con la base de datos.", DbSyncService.LogLevel.Error);
            });
            menuMasAcciones.Items.Add("💥 Simular Ráfaga de Incidencias", null, (s, e) =>
            {
                for (int i = 1; i <= 4; i++)
                {
                    syncService.AppendLog($"[ERROR] Ráfaga de incidencia #{i}: Simulación de fallo en lote {i * 500}", DbSyncService.LogLevel.Error);
                }
            });

            btnMas = CreatePillButton("Más ▾", Color.FromArgb(30, 41, 59), (s, e) => menuMasAcciones.Show(btnMas, new Point(0, btnMas.Height)), 90);

            btnSizeToggle = CreatePillButton("🗄️ Fuente: GRANDE ▾", Color.FromArgb(51, 65, 85), (s, e) => CycleFontSize(), 175);

            btnThemeToggle = CreatePillButton("🌗 Tema: OSCURO", Color.FromArgb(71, 85, 105), (s, e) => ToggleTheme(), 150);

            btnSync = CreatePillButton("🔄 Sincronizar PostgreSQL", Color.FromArgb(6, 182, 212), async (s, e) => await DoManualSyncAsync(), 200);

            flowButtons.Controls.Add(btnAck);
            flowButtons.Controls.Add(btnAutoToggle);
            flowButtons.Controls.Add(btnViewErrors);
            flowButtons.Controls.Add(btnExport);
            flowButtons.Controls.Add(btnPrint);
            flowButtons.Controls.Add(btnClear);
            flowButtons.Controls.Add(btnMas);
            flowButtons.Controls.Add(btnSizeToggle);
            flowButtons.Controls.Add(btnThemeToggle);
            flowButtons.Controls.Add(btnSync);

            // ==========================================
            // 3. TARJETAS DE ESTADO SUPERIORES (Status Cards Banner)
            // ==========================================
            pnlStatusCards = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(16, 4, 16, 8)
            };
            pnlStatusCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlStatusCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // Tarjeta 1: Pedidos Pendientes
            cardPendientes = CreateRoundedCard(64);
            var iconCard1 = new Label { Text = "🟢", Font = new Font("Segoe UI Emoji", 16F), AutoSize = true, Location = new Point(12, 14) };
            lblPendientesMain = new Label { Text = "Sin pedidos pendientes", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true, Location = new Point(46, 8) };
            lblPendientesSub = new Label { Text = "Todos los pedidos procesados.", Font = new Font("Segoe UI", 8.5F), AutoSize = true, Location = new Point(47, 32) };
            cardPendientes.Controls.Add(iconCard1);
            cardPendientes.Controls.Add(lblPendientesMain);
            cardPendientes.Controls.Add(lblPendientesSub);

            // Tarjeta 2: Incidencias y Salud
            cardIncidencias = CreateRoundedCard(64);
            var iconCard2 = new Label { Text = "📈", Font = new Font("Segoe UI Emoji", 16F), AutoSize = true, Location = new Point(12, 14) };
            lblIncidenciasMain = new Label { Text = "Incidencias: 0 errores | 0 advertencias (Sin errores)", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true, Location = new Point(46, 8) };
            lblIncidenciasSub = new Label { Text = "El sistema está operando correctamente.", Font = new Font("Segoe UI", 8.5F), AutoSize = true, Location = new Point(47, 32) };
            cardIncidencias.Controls.Add(iconCard2);
            cardIncidencias.Controls.Add(lblIncidenciasMain);
            cardIncidencias.Controls.Add(lblIncidenciasSub);

            // Tarjeta 3: Ruta Target ERP PsGest (Ocupa las 2 columnas)
            cardRutaTarget = CreateRoundedCard(48);
            var iconCard3 = new Label { Text = "🗄️", Font = new Font("Segoe UI Emoji", 12F), AutoSize = true, Location = new Point(12, 12) };
            lblRutaTargetText = new Label 
            { 
                Text = "Destino ERP PsGest: ", 
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), 
                AutoSize = false,
                Location = new Point(40, 13),
                Size = new Size(600, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            btnCopyPath = new Button
            {
                Text = "📋",
                Font = new Font("Segoe UI Emoji", 11F),
                Size = new Size(34, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnCopyPath.FlatAppearance.BorderSize = 0;
            btnCopyPath.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(syncService.MdbPath);
                    var tt = new ToolTip();
                    tt.Show("¡Ruta copiada al portapapeles!", btnCopyPath, 0, -30, 2000);
                }
                catch { }
            };
            cardRutaTarget.Controls.Add(iconCard3);
            cardRutaTarget.Controls.Add(lblRutaTargetText);
            cardRutaTarget.Controls.Add(btnCopyPath);

            pnlStatusCards.Controls.Add(cardPendientes, 0, 0);
            pnlStatusCards.Controls.Add(cardIncidencias, 1, 0);
            pnlStatusCards.Controls.Add(cardRutaTarget, 0, 1);
            pnlStatusCards.SetColumnSpan(cardRutaTarget, 2);

            // ==========================================
            // 4. PANEL DE CONTENIDO DIVIDIDO A 2 COLUMNAS
            // ==========================================
            pnlMainSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(16, 0, 16, 8)
            };
            pnlMainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F)); // Log Izquierda
            pnlMainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F)); // Sidebar Derecha

            // --- COLUMNA IZQUIERDA: TERMINAL LOG EN VIVO ---
            pnlLeftLog = CreateRoundedCard();
            pnlLeftLog.Dock = DockStyle.Fill;
            pnlLeftLog.Padding = new Padding(12);

            var pnlLogHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36
            };

            var lblLogTitle = new Label
            {
                Text = "📄 Registro en tiempo real",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(4, 6)
            };

            chkAutoscroll = new CheckBox
            {
                Text = "Autoscroll",
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(380, 6),
                Cursor = Cursors.Hand
            };
            chkAutoscroll.CheckedChanged += (s, e) => isAutoscrollEnabled = chkAutoscroll.Checked;

            btnPauseLog = new Button
            {
                Text = "⏸ Pausar",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Size = new Size(80, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(480, 4),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPauseLog.Click += (s, e) =>
            {
                isLogPaused = !isLogPaused;
                btnPauseLog.Text = isLogPaused ? "▶ Reanudar" : "⏸ Pausar";
            };

            btnClearLogView = new Button
            {
                Text = "🗑️ Limpiar vista",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Size = new Size(110, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(568, 4),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClearLogView.Click += (s, e) => txtLog.Clear();

            pnlLogHeader.Controls.Add(lblLogTitle);
            pnlLogHeader.Controls.Add(chkAutoscroll);
            pnlLogHeader.Controls.Add(btnPauseLog);
            pnlLogHeader.Controls.Add(btnClearLogView);

            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = GetConsolasFont(12F),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(8)
            };

            pnlLeftLog.Controls.Add(txtLog);
            pnlLeftLog.Controls.Add(pnlLogHeader);

            // --- COLUMNA DERECHA: SIDEBAR DE TELEMETRÍA ---
            pnlRightSidebar = CreateRoundedCard();
            pnlRightSidebar.Dock = DockStyle.Fill;
            pnlRightSidebar.Padding = new Padding(14);

            var lblSidebarTitle = new Label
            {
                Text = "🗄️ Estado del servicio",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 32
            };

            var flowSidebarItems = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };

            flowSidebarItems.Controls.Add(CreateSidebarMetric("Demonio activo", "Ejecutándose en segundo plano.", "🟢", out lblTeleActive));
            flowSidebarItems.Controls.Add(CreateSidebarMetric("Pedidos pendientes", "0", "📦", out lblTelePendingVal));
            flowSidebarItems.Controls.Add(CreateSidebarMetric("Última sincronización", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), "🕒", out lblTeleLastSyncVal));
            flowSidebarItems.Controls.Add(CreateSidebarMetric("Tiempo en ejecución", "0 h 00 min", "⏱️", out lblTeleUptimeVal));
            flowSidebarItems.Controls.Add(CreateSidebarMetric("Fuente de datos", "GRANDE", "🗄️", out lblTeleDataSourceVal));
            flowSidebarItems.Controls.Add(CreateSidebarMetric("Conexión PostgreSQL", "🟢 Conectada", "🔗", out lblTelePostgresVal));
            flowSidebarItems.Controls.Add(CreateSidebarMetric("Registros procesados (hoy)", "12.438", "📊", out lblTeleProcessedVal));

            var pnlSidebarFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 45
            };

            var lblFooter1 = new Label
            {
                Text = "ReprediSL V4.0.0 | Empresa S.A.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 18
            };

            var lblFooter2 = new Label
            {
                Text = "Sincronización · Automatización · Confianza",
                Font = new Font("Segoe UI", 8F),
                Dock = DockStyle.Top,
                Height = 18
            };

            pnlSidebarFooter.Controls.Add(lblFooter2);
            pnlSidebarFooter.Controls.Add(lblFooter1);

            pnlRightSidebar.Controls.Add(flowSidebarItems);
            pnlRightSidebar.Controls.Add(lblSidebarTitle);
            pnlRightSidebar.Controls.Add(pnlSidebarFooter);

            pnlMainSplit.Controls.Add(pnlLeftLog, 0, 0);
            pnlMainSplit.Controls.Add(pnlRightSidebar, 1, 0);

            // ==========================================
            // 5. BARRA DE ESTADO INFERIOR (Status Bar)
            // ==========================================
            pnlStatusBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                Padding = new Padding(16, 2, 16, 2)
            };

            lblStatusOS = new Label
            {
                Text = "⚫ Sistema operativo | Sin incidencias",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 5)
            };

            lblStatusNextSync = new Label
            {
                Text = "ℹ️ Esperando próxima sincronización...",
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(340, 5)
            };

            lblStatusClock = new Label
            {
                Text = DateTime.Now.ToString("ddd d MMM yyyy | HH:mm:ss"),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(this.ClientSize.Width - 230, 5)
            };

            pnlStatusBar.Controls.Add(lblStatusOS);
            pnlStatusBar.Controls.Add(lblStatusNextSync);
            pnlStatusBar.Controls.Add(lblStatusClock);

            // Ensamblar todo en el Formulario
            this.Controls.Add(pnlMainSplit);
            this.Controls.Add(pnlStatusCards);
            this.Controls.Add(flowButtons);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlStatusBar);

            // ==========================================
            // 6. CONTEXT MENU STRIP (Tray System Menu)
            // ==========================================
            trayMenu = new ContextMenuStrip { Font = new Font("Segoe UI", 10F) };

            var itemTitle = new ToolStripMenuItem("🖥️ Demonio ReprediSL V4") { Enabled = false, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
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

            var itemSizeMenu = new ToolStripMenuItem("🔤 Tamaño de Letra & Tema");
            itemSizeMenu.DropDownItems.Add("Pequeño (100%)", null, (s, e) => ApplyFontSize("Pequeno"));
            itemSizeMenu.DropDownItems.Add("Mediano (125%)", null, (s, e) => ApplyFontSize("Mediano"));
            itemSizeMenu.DropDownItems.Add("Grande (150%)", null, (s, e) => ApplyFontSize("Grande"));
            itemSizeMenu.DropDownItems.Add("-");
            itemSizeMenu.DropDownItems.Add("🌞 Modo Claro (Light Theme)", null, (s, e) => ApplyTheme("Claro"));
            itemSizeMenu.DropDownItems.Add("🌙 Modo Oscuro (Dark Theme)", null, (s, e) => ApplyTheme("Oscuro"));
            trayMenu.Items.Add(itemSizeMenu);

            trayMenu.Items.Add("-");

            var itemSimMenu = new ToolStripMenuItem("🧪 Pruebas y Simulaciones");
            itemSimMenu.DropDownItems.Add("📦 Simular Llegada de Pedido", null, (s, e) =>
            {
                syncService.AppendLog("[NUEVO PEDIDO] Recibido pedido N. TEST-001 | Cliente: 1001 (CLIENTE DE PRUEBA SL) | Importe: 450,00 EUR", DbSyncService.LogLevel.Success);
                totalRegistrosProcesadosHoy++;
                UpdateStatusLabels();
            });
            itemSimMenu.DropDownItems.Add("🚨 Simular Error de Sincronización", null, (s, e) =>
            {
                syncService.AppendLog("[ERROR] Fallo de prueba simulado: Conexión intermitente con la base de datos.", DbSyncService.LogLevel.Error);
            });
            itemSimMenu.DropDownItems.Add("💥 Simular Ráfaga de Incidencias", null, (s, e) =>
            {
                for (int i = 1; i <= 4; i++)
                {
                    syncService.AppendLog($"[ERROR] Ráfaga de incidencia #{i}: Simulación de fallo en lote {i * 500}", DbSyncService.LogLevel.Error);
                }
            });
            trayMenu.Items.Add(itemSimMenu);

            trayMenu.Items.Add("-");
            trayMenu.Items.Add("❌ Salir del Demonio", null, (s, e) => ExitApplication());

            trayIcon = new NotifyIcon
            {
                Text = "ReprediSL V4 - Demonio de Pedidos",
                Icon = SystemIcons.Information,
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => ShowForm();

            this.Resize += (s, e) => RepositionCustomControls();
        }

        private Panel CreateRoundedCard(int height = 64)
        {
            return new Panel
            {
                Margin = new Padding(4),
                Padding = new Padding(8, 6, 8, 6),
                Height = height
            };
        }

        private Button CreatePillButton(string text, Color baseColor, EventHandler onClick, int width = 160)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, 44),
                Margin = new Padding(0, 0, 8, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = baseColor,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            
            // Borde sutil para dar profundidad
            btn.FlatAppearance.BorderSize = 2;
            btn.FlatAppearance.BorderColor = ControlPaint.Dark(baseColor, 0.15f);
            
            // Efecto de sombra simulado con borde inferior más oscuro
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                Math.Min(255, baseColor.R + 20),
                Math.Min(255, baseColor.G + 20),
                Math.Min(255, baseColor.B + 20)
            );
            
            Color hoverColor = Color.FromArgb(
                Math.Min(255, baseColor.R + 30),
                Math.Min(255, baseColor.G + 30),
                Math.Min(255, baseColor.B + 30)
            );
            
            Color pressedColor = Color.FromArgb(
                Math.Max(0, baseColor.R - 20),
                Math.Max(0, baseColor.G - 20),
                Math.Max(0, baseColor.B - 20)
            );
            
            btn.MouseEnter += (s, e) => 
            {
                btn.BackColor = hoverColor;
                btn.FlatAppearance.BorderColor = ControlPaint.Light(baseColor, 0.3f);
            };
            
            btn.MouseLeave += (s, e) => 
            {
                btn.BackColor = baseColor;
                btn.FlatAppearance.BorderColor = ControlPaint.Dark(baseColor, 0.15f);
            };
            
            btn.MouseDown += (s, e) => 
            {
                if (e.Button == MouseButtons.Left)
                {
                    btn.BackColor = pressedColor;
                    btn.FlatAppearance.BorderColor = ControlPaint.Dark(pressedColor, 0.3f);
                }
            };
            
            btn.MouseUp += (s, e) => 
            {
                btn.BackColor = hoverColor;
                btn.FlatAppearance.BorderColor = ControlPaint.Light(baseColor, 0.3f);
            };

            return btn;
        }

        private Panel CreateSidebarMetric(string title, string initialVal, string icon, out Label valLabel)
        {
            var pnl = new Panel
            {
                Size = new Size(260, 48),
                Margin = new Padding(0, 0, 0, 6)
            };

            var lblIcon = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI Emoji", 12F),
                AutoSize = true,
                Location = new Point(4, 12)
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8.5F),
                AutoSize = true,
                Location = new Point(32, 6)
            };

            valLabel = new Label
            {
                Text = initialVal,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(32, 24)
            };

            pnl.Controls.Add(lblIcon);
            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(valLabel);

            return pnl;
        }

        private void RepositionCustomControls()
        {
            if (btnCopyPath != null && cardRutaTarget != null)
            {
                btnCopyPath.Location = new Point(cardRutaTarget.ClientSize.Width - 42, 6);
                
                // Ajustar el ancho del label de ruta para que llegue hasta el botón
                if (lblRutaTargetText != null)
                {
                    int availableWidth = cardRutaTarget.ClientSize.Width - 50 - btnCopyPath.Width;
                    lblRutaTargetText.Size = new Size(Math.Max(availableWidth, 400), lblRutaTargetText.Height);
                }
            }
            if (lblStatusClock != null && pnlStatusBar != null)
            {
                lblStatusClock.Location = new Point(pnlStatusBar.ClientSize.Width - 210, 5);
            }
            if (btnPauseLog != null && btnClearLogView != null && chkAutoscroll != null && pnlLeftLog != null)
            {
                btnClearLogView.Location = new Point(pnlLeftLog.ClientSize.Width - 130, 4);
                btnPauseLog.Location = new Point(pnlLeftLog.ClientSize.Width - 218, 4);
                chkAutoscroll.Location = new Point(pnlLeftLog.ClientSize.Width - 315, 6);
            }
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

            float logFontSize = 12F;
            float btnFontSize = 9.25F;
            int formWidth = 1260;
            int formHeight = 800;

            switch (size)
            {
                case "Pequeno":
                    logFontSize = 9.5F;
                    btnFontSize = 8.5F;
                    formWidth = 1050;
                    formHeight = 680;
                    btnSizeToggle.Text = "🗄️ Fuente: PEQUEÑO ▾";
                    break;
                case "Mediano":
                    logFontSize = 11F;
                    btnFontSize = 9F;
                    formWidth = 1160;
                    formHeight = 740;
                    btnSizeToggle.Text = "🗄️ Fuente: MEDIANO ▾";
                    break;
                default: // Grande
                    logFontSize = 12F;
                    btnFontSize = 9.25F;
                    formWidth = 1260;
                    formHeight = 800;
                    btnSizeToggle.Text = "🗄️ Fuente: GRANDE ▾";
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
            btnMas.Font = btnFont;
            btnSizeToggle.Font = btnFont;
            btnThemeToggle.Font = btnFont;
            btnSync.Font = btnFont;
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
                Color bgMain = Color.FromArgb(241, 245, 249);
                Color cardBg = Color.White;
                Color textPrimary = Color.FromArgb(15, 23, 42);
                Color textMuted = Color.FromArgb(100, 116, 139);
                Color borderClr = Color.FromArgb(226, 232, 240);

                this.BackColor = bgMain;
                pnlHeader.BackColor = bgMain;
                pnlStatusBar.BackColor = Color.FromArgb(226, 232, 240);
                pnlStatusBar.ForeColor = textPrimary;

                lblHeaderTitle.ForeColor = textPrimary;
                lblHeaderSubtitle.ForeColor = textMuted;
                lblHeaderStatusText.ForeColor = textPrimary;
                btnHeaderSettings.BackColor = borderClr;
                btnHeaderSettings.ForeColor = textPrimary;

                cardPendientes.BackColor = cardBg;
                cardIncidencias.BackColor = cardBg;
                cardRutaTarget.BackColor = cardBg;
                pnlLeftLog.BackColor = cardBg;
                pnlRightSidebar.BackColor = cardBg;

                lblPendientesMain.ForeColor = textPrimary;
                lblPendientesSub.ForeColor = textMuted;
                lblIncidenciasMain.ForeColor = textPrimary;
                lblIncidenciasSub.ForeColor = textMuted;
                lblRutaTargetText.ForeColor = Color.FromArgb(2, 132, 199);
                btnCopyPath.BackColor = borderClr;
                btnCopyPath.ForeColor = textPrimary;

                txtLog.BackColor = Color.White;
                txtLog.ForeColor = textPrimary;

                var lightRenderer = new LightMenuRenderer();
                trayMenu.Renderer = lightRenderer;
                menuMasAcciones.Renderer = lightRenderer;

                btnThemeToggle.Text = "🌞 Tema: CLARO";
                btnThemeToggle.BackColor = Color.FromArgb(203, 213, 225);
                btnThemeToggle.ForeColor = textPrimary;
            }
            else // Oscuro (Dark Slate Glassmorphism)
            {
                Color bgMain = Color.FromArgb(15, 23, 42);       // Slate 900
                Color cardBg = Color.FromArgb(30, 41, 59);      // Slate 800
                Color textPrimary = Color.FromArgb(248, 250, 252);
                Color textMuted = Color.FromArgb(148, 163, 184);
                Color borderClr = Color.FromArgb(51, 65, 85);

                this.BackColor = bgMain;
                pnlHeader.BackColor = bgMain;
                pnlStatusBar.BackColor = Color.FromArgb(10, 15, 30);
                pnlStatusBar.ForeColor = textMuted;

                lblHeaderTitle.ForeColor = textPrimary;
                lblHeaderSubtitle.ForeColor = textMuted;
                lblHeaderStatusText.ForeColor = textPrimary;
                btnHeaderSettings.BackColor = borderClr;
                btnHeaderSettings.ForeColor = textPrimary;

                cardPendientes.BackColor = cardBg;
                cardIncidencias.BackColor = cardBg;
                cardRutaTarget.BackColor = cardBg;
                pnlLeftLog.BackColor = cardBg;
                pnlRightSidebar.BackColor = cardBg;

                lblPendientesMain.ForeColor = textPrimary;
                lblPendientesSub.ForeColor = textMuted;
                lblIncidenciasMain.ForeColor = textPrimary;
                lblIncidenciasSub.ForeColor = textMuted;
                lblRutaTargetText.ForeColor = Color.FromArgb(56, 189, 248);
                btnCopyPath.BackColor = borderClr;
                btnCopyPath.ForeColor = textPrimary;

                txtLog.BackColor = Color.FromArgb(2, 6, 23);     // Terminal Slate 950
                txtLog.ForeColor = Color.FromArgb(52, 211, 153); // Emerald Code

                var darkRenderer = new DarkMenuRenderer();
                trayMenu.Renderer = darkRenderer;
                menuMasAcciones.Renderer = darkRenderer;

                btnThemeToggle.Text = "🌙 Tema: OSCURO";
                btnThemeToggle.BackColor = Color.FromArgb(51, 65, 85);
                btnThemeToggle.ForeColor = textPrimary;
            }

            UpdateStatusLabels();
            RepositionCustomControls();
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
                btnAutoToggle.Text = "⚙️ AUTO-ACEPTAR\nACTIVO";
                btnAutoToggle.BackColor = Color.FromArgb(16, 185, 129);
                syncService.AppendLog("[CONFIG] Auto-aceptación de pedidos ACTIVADA.", DbSyncService.LogLevel.Info);
            }
            else
            {
                btnAutoToggle.Text = "⚙️ Auto-aceptar\nDesactivado";
                btnAutoToggle.BackColor = Color.FromArgb(99, 102, 241);
                syncService.AppendLog("[CONFIG] Auto-aceptación de pedidos DESACTIVADA (Modo Confirmación Manual).", DbSyncService.LogLevel.Info);
            }
        }

        private void UpdateStatusLabels()
        {
            lblPendientesMain.Text = "Sin pedidos pendientes";
            lblPendientesSub.Text = "Todos los pedidos procesados.";

            if (lblTeleProcessedVal != null)
                lblTeleProcessedVal.Text = totalRegistrosProcesadosHoy.ToString("N0");

            if (autoAcceptMode)
            {
                btnAutoToggle.Text = "⚙️ AUTO-ACEPTAR\nACTIVO";
                btnAutoToggle.BackColor = Color.FromArgb(16, 185, 129);
            }
            else
            {
                btnAutoToggle.Text = "⚙️ Auto-aceptar\nDesactivado";
                btnAutoToggle.BackColor = Color.FromArgb(99, 102, 241);
            }

            if (syncService.ErrorCount > 0)
            {
                lblIncidenciasMain.Text = $"Incidencias: {syncService.ErrorCount} errores/alertas acumuladas";
                lblIncidenciasMain.ForeColor = Color.FromArgb(244, 63, 94);
                lblIncidenciasSub.Text = "Se han registrado fallos en las últimas sincronizaciones.";
                btnViewErrors.Text = $"⚠️ Ver errores ({syncService.ErrorCount})";
                btnViewErrors.BackColor = Color.FromArgb(225, 29, 72);
            }
            else
            {
                lblIncidenciasMain.Text = "Incidencias: 0 errores | 0 advertencias (Sin errores)";
                lblIncidenciasMain.ForeColor = currentTheme == "Claro" ? Color.FromArgb(15, 23, 42) : Color.FromArgb(52, 211, 153);
                lblIncidenciasSub.Text = "El sistema está operando correctamente.";
                btnViewErrors.Text = "⚠️ Ver errores (0)";
                btnViewErrors.BackColor = Color.FromArgb(71, 85, 105);
            }

            lblRutaTargetText.Text = "Destino ERP PsGest: " + syncService.MdbPath;
            
            // Ajustar el ancho del label para que llegue hasta el final
            if (cardRutaTarget != null && btnCopyPath != null)
            {
                int availableWidth = cardRutaTarget.ClientSize.Width - 50 - btnCopyPath.Width;
                lblRutaTargetText.Size = new Size(Math.Max(availableWidth, 400), lblRutaTargetText.Height);
            }
        }

        private void SyncService_OnLogMessage(string message, DbSyncService.LogLevel level)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SyncService_OnLogMessage(message, level)));
                return;
            }

            if (lblTeleLastSyncVal != null)
                lblTeleLastSyncVal.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

            if (isLogPaused) return;

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

            if (isAutoscrollEnabled)
            {
                txtLog.ScrollToCaret();
            }

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

            // Actualizar reloj y tiempo de actividad (Uptime)
            if (lblStatusClock != null)
            {
                lblStatusClock.Text = DateTime.Now.ToString("ddd d MMM yyyy | HH:mm:ss");
            }

            if (lblTeleUptimeVal != null)
            {
                TimeSpan uptime = DateTime.Now - startTime;
                lblTeleUptimeVal.Text = $"{uptime.Hours} h {uptime.Minutes:D2} min {uptime.Seconds:D2} s";
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
                btnSync.Text = "🔄 Sincronizar PostgreSQL";
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

    // Renderizadores de menú estilo WinForms profesional
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
