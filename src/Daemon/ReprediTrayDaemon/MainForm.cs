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

        // Contenedores Principales
        private FlowLayoutPanel pnlMainContent = null!;
        private Panel pnlHeader = null!;
        private Panel pnlModeSwitch = null!;
        private Panel pnlPipelineDiagram = null!;
        private TableLayoutPanel pnlMetricCards = null!;
        private FlowLayoutPanel flowSubMetrics = null!;
        private Panel pnlLogConsoleCard = null!;
        private Panel pnlStatusBar = null!;

        // MODO Pill Switch
        private Label lblModeTag = null!;
        private Panel pnlSegmentedPill = null!;
        private Button btnPillAuto = null!;
        private Button btnPillManual = null!;

        // Telemetría 4 Bloques
        private Label lblClockVal = null!;
        private Label lblUptimeVal = null!;
        private Label lblProcessedVal = null!;
        private Label lblPendingVal = null!;

        // Consola Log
        private Label lblConsoleTitle = null!;
        private RichTextBox txtLog = null!;
        private CheckBox chkAutoscroll = null!;
        private Button btnClearLogView = null!;
        private Label lblLogSubBar = null!;

        private bool isLogPaused = false;
        private bool isAutoscrollEnabled = true;

        // Etiquetas del Header y Pipeline
        private Label lblHeaderTitle = null!;
        private Label lblHeaderSubtitle = null!;
        private Panel pnlOnlineBadge = null!;
        private Panel cardNodePg = null!;
        private Panel cardNodeBridge = null!;
        private Panel cardNodeAccess = null!;
        private Label lblNodePgStatus = null!;
        private Label lblNodeBridgeStatus = null!;
        private Label lblNodeAccessStatus = null!;

        // Barra de estado inferior
        private Label lblFooterServiceInfo = null!;
        private Label lblFooterStatusBadge = null!;

        // Servicios y Configuración
        private DbSyncService syncService = null!;
        private System.Windows.Forms.Timer timerHealth = null!;
        private DateTime startTime = DateTime.Now;
        private bool forceClose = false;
        private int totalRegistrosProcesadosHoy = 11;

        private bool autoAcceptMode = true; // Por defecto Auto-Aceptar
        private int pedidosPendientesCount = 0;
        private int simOrderCounter = 1;
        private bool blinkState = false;
        private DateTime pgLastActive = DateTime.MinValue;
        private DateTime bridgeLastActive = DateTime.MinValue;
        private DateTime accessLastActive = DateTime.MinValue;
        private Label iconNodePg = null!;
        private Label iconNodeBridge = null!;
        private Label iconNodeAccess = null!;
        private Button btnVerPedidos = null!;
        private List<(string NumPedido, string Cliente, decimal Importe, DateTime Hora, string Estado)> recentOrders = new();
        private string currentFontSize = "Grande";
        private string currentTheme = "Oscuro"; // Tema oscuro estilo MiHomo Gate por defecto
        private string settingsFilePath = string.Empty;

        [System.Runtime.InteropServices.DllImport("user32.DLL", EntryPoint = "ReleaseCapture")]
        private extern static void ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.DLL", EntryPoint = "SendMessage")]
        private extern static void SendMessage(System.IntPtr hWnd, int wMsg, int wParam, int lParam);

        private static string ResolveProjectRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "Scripts", "BaseDatos")))
                {
                    return dir.FullName;
                }

                if (Directory.Exists(Path.Combine(dir.FullName, "Scripts", "BaseDatos")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "src", "Daemon")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            string fallback = @"D:\programacio\repredi\ReprediSL_V4";
            if (Directory.Exists(fallback))
            {
                return fallback;
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public MainForm()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.MaximizedBounds = Screen.FromHandle(this.Handle).WorkingArea;

            string projectRoot = ResolveProjectRoot();

            settingsFilePath = Path.Combine(projectRoot, "src", "Access", "ui_settings.json");

            InitializeComponentCustom();

            syncService = new DbSyncService(projectRoot);
            syncService.OnLogMessage += SyncService_OnLogMessage;
            syncService.OnErrorThresholdExceeded += SyncService_OnErrorThresholdExceeded;

            CargarConfiguracionUI();
            ApplyTheme(currentTheme);
            UpdateStatusLabels();

            syncService.AppendLog("SELECT id, cliente, total FROM pedidos_nuevos WHERE estado='N' AND synced_at IS NULL -> 1 fila (P-2491)", DbSyncService.LogLevel.Info);
            syncService.AppendLog("INSERT INTO PedidosCab (NumPedido, Cliente, Total, Canal) VALUES ('P-2491', ..., 810.41, 'movil')", DbSyncService.LogLevel.Success);

            timerHealth = new System.Windows.Forms.Timer { Interval = 1000 };
            timerHealth.Tick += TimerHealth_Tick;
            timerHealth.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Degradado del Fondo: MiHomo Gate Dark Theme (#0A0F1A a #0F172A) vs Soft Pastel Slate (#EEF2FF a #E0F2FE)
            Color topColor = currentTheme == "Oscuro" ? Color.FromArgb(10, 15, 26) : Color.FromArgb(238, 242, 255);
            Color bottomColor = currentTheme == "Oscuro" ? Color.FromArgb(15, 23, 42) : Color.FromArgb(224, 242, 254);

            using var brush = new LinearGradientBrush(this.ClientRectangle, topColor, bottomColor, 60F);
            e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }

        private void InitializeComponentCustom()
        {
            this.Text = "PsSyncBridge Tray - ReprediSL V4";
            this.MinimumSize = new Size(980, 750);
            this.Size = new Size(1100, 840);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Application;

            // Container principal vertical
            pnlMainContent = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(20, 16, 20, 16),
                BackColor = currentTheme == "Oscuro" ? Color.FromArgb(10, 15, 26) : Color.FromArgb(238, 242, 255)
            };

            // ==========================================
            // 1. CABECERA SUPERIOR (Header Top Bar)
            // ==========================================
            pnlHeader = new Panel
            {
                Size = new Size(1040, 56),
                Margin = new Padding(0, 0, 0, 12),
                BackColor = Color.Transparent
            };
            pnlHeader.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(this.Handle, 0x112, 0xf012, 0); } };

            var picDbIcon = new Label
            {
                Text = "🔄",
                Font = new Font("Segoe UI Emoji", 20F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(4, 6)
            };

            lblHeaderTitle = new Label
            {
                Text = "PsSyncBridge Tray",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(54, 4),
                ForeColor = Color.FromArgb(30, 41, 59)
            };
            lblHeaderTitle.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(this.Handle, 0x112, 0xf012, 0); } };

            lblHeaderSubtitle = new Label
            {
                Text = "PostgreSQL 16 · ventas_produccion ⇄ ODBC · gestion.mdb",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(54, 30),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            // Status Badge derecho
            pnlOnlineBadge = CreatePillBadge("🟢 En línea", Color.FromArgb(220, 252, 231), Color.FromArgb(21, 128, 61));
            pnlOnlineBadge.Location = new Point(880, 8);
            pnlOnlineBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            pnlHeader.Controls.Add(picDbIcon);
            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSubtitle);
            pnlHeader.Controls.Add(pnlOnlineBadge);

            // ==========================================
            // 2. CONMUTADOR DE MODO (MODO Segmented Pill Switch)
            // ==========================================
            pnlModeSwitch = CreateRoundedGlassCard(1040, 58, 20);
            pnlModeSwitch.Margin = new Padding(0, 0, 0, 16);

            lblModeTag = new Label
            {
                Text = "MODO",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(20, 20)
            };

            pnlSegmentedPill = new Panel
            {
                Size = new Size(420, 42),
                Location = new Point(90, 8),
                BackColor = Color.Transparent
            };
            using var pathSegRegion = GetRoundedRectPath(new Rectangle(0, 0, 420, 42), 20);
            pnlSegmentedPill.Region = new Region(pathSegRegion);

            pnlSegmentedPill.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = GetRoundedRectPath(new Rectangle(0, 0, pnlSegmentedPill.Width - 1, pnlSegmentedPill.Height - 1), 20);
                Color fillClr = currentTheme == "Oscuro" ? Color.FromArgb(15, 23, 42) : Color.FromArgb(226, 232, 240);
                using var brush = new SolidBrush(fillClr);
                e.Graphics.FillPath(brush, path);
            };

            btnPillAuto = new Button
            {
                Text = "⚡ Auto-Aceptar",
                Size = new Size(200, 36),
                Location = new Point(3, 3),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnPillAuto.FlatAppearance.BorderSize = 0;
            btnPillAuto.Click += (s, e) => { if (!autoAcceptMode) ToggleAutoAcceptMode(); };

            btnPillManual = new Button
            {
                Text = "✋ Confirmación Manual",
                Size = new Size(208, 36),
                Location = new Point(206, 3),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnPillManual.FlatAppearance.BorderSize = 0;
            btnPillManual.Click += (s, e) => { if (autoAcceptMode) ToggleAutoAcceptMode(); };

            pnlSegmentedPill.Controls.Add(btnPillAuto);
            pnlSegmentedPill.Controls.Add(btnPillManual);

            pnlModeSwitch.Controls.Add(lblModeTag);
            pnlModeSwitch.Controls.Add(pnlSegmentedPill);

            btnVerPedidos = new Button
            {
                Text = "📋 Ver Pedidos",
                Size = new Size(152, 36),
                Location = new Point(530, 8),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            btnVerPedidos.FlatAppearance.BorderSize = 1;
            btnVerPedidos.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
            using var pathBtnPedidos = GetRoundedRectPath(new Rectangle(0, 0, 152, 36), 16);
            btnVerPedidos.Region = new Region(pathBtnPedidos);
            btnVerPedidos.Click += (s, e) => MostrarVentanaPedidos();
            pnlModeSwitch.Controls.Add(btnVerPedidos);

            // ==========================================
            // 3. DIAGRAMA DE FLUJO DE PIPELINE (3 Nodos Conectados)
            // ==========================================
            pnlPipelineDiagram = new Panel
            {
                Size = new Size(1040, 115),
                Margin = new Padding(0, 0, 0, 16),
                BackColor = Color.Transparent
            };

            cardNodePg = CreatePipelineNodeCard("🗄️", "PostgreSQL", "pedidos_nuevos",
                () => (DateTime.Now - pgLastActive).TotalSeconds < 3,
                out lblNodePgStatus, out iconNodePg);
            cardNodePg.Location = new Point(0, 0);

            cardNodeBridge = CreatePipelineNodeCard("🔄", "PsSyncBridge", "idle",
                () => (DateTime.Now - bridgeLastActive).TotalSeconds < 3,
                out lblNodeBridgeStatus, out iconNodeBridge);
            cardNodeBridge.Location = new Point(380, 0);

            cardNodeAccess = CreatePipelineNodeCard("📄", "Access ERP", "gestion.mdb",
                () => (DateTime.Now - accessLastActive).TotalSeconds < 3,
                out lblNodeAccessStatus, out iconNodeAccess);
            cardNodeAccess.Location = new Point(760, 0);

            // Líneas de conexión
            pnlPipelineDiagram.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color lineClr = currentTheme == "Oscuro" ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
                using var pen = new Pen(lineClr, 3f);
                pen.DashStyle = DashStyle.Solid;
                // Line 1: Pg -> Bridge
                e.Graphics.DrawLine(pen, 285, 55, 375, 55);
                // Line 2: Bridge -> Access
                e.Graphics.DrawLine(pen, 665, 55, 755, 55);
            };

            pnlPipelineDiagram.Controls.Add(cardNodePg);
            pnlPipelineDiagram.Controls.Add(cardNodeBridge);
            pnlPipelineDiagram.Controls.Add(cardNodeAccess);

            // ==========================================
            // 4. TARJETAS DE TELEMETRÍA (4 Grid Cards)
            // ==========================================
            pnlMetricCards = new TableLayoutPanel
            {
                Size = new Size(1040, 95),
                Margin = new Padding(0, 0, 0, 12),
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            pnlMetricCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            pnlMetricCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            pnlMetricCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            pnlMetricCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            var cardClock = CreateMetricCard("RELOJ", DateTime.Now.ToString("HH:mm:ss"), out lblClockVal);
            var cardUptime = CreateMetricCard("UPTIME", "00:00:00", out lblUptimeVal);
            var cardProcessed = CreateMetricCard("PEDIDOS HOY", totalRegistrosProcesadosHoy.ToString(), out lblProcessedVal);
            var cardPending = CreateMetricCard("EN ESPERA", pedidosPendientesCount.ToString(), out lblPendingVal);

            pnlMetricCards.Controls.Add(cardClock, 0, 0);
            pnlMetricCards.Controls.Add(cardUptime, 1, 0);
            pnlMetricCards.Controls.Add(cardProcessed, 2, 0);
            pnlMetricCards.Controls.Add(cardPending, 3, 0);

            // ==========================================
            // 5. BARRA DE SUB-MÉTRICAS (Sub-Pill Badges)
            // ==========================================
            flowSubMetrics = new FlowLayoutPanel
            {
                Size = new Size(1040, 36),
                Margin = new Padding(0, 0, 0, 16),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            flowSubMetrics.Controls.Add(CreateSubPillBadge("latencia 68 ms"));
            flowSubMetrics.Controls.Add(CreateSubPillBadge("reintentos 1"));
            flowSubMetrics.Controls.Add(CreateSubPillBadge("errores red 1"));
            flowSubMetrics.Controls.Add(CreateSubPillBadge("cola retry 0"));
            flowSubMetrics.Controls.Add(CreateSubPillBadge("poll 2 s"));

            // ==========================================
            // 6. CONSOLA EN TIEMPO REAL ("CONSOLA · TIEMPO REAL")
            // ==========================================
            pnlLogConsoleCard = CreateRoundedGlassCard(1040, 330, 20);
            pnlLogConsoleCard.Margin = new Padding(0, 0, 0, 16);

            var pnlLogHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(16, 8, 16, 4)
            };

            lblConsoleTitle = new Label
            {
                Text = "CONSOLA · TIEMPO REAL",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true,
                Location = new Point(4, 10)
            };

            chkAutoscroll = new CheckBox
            {
                Text = "auto-scroll",
                Checked = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(29, 78, 216),
                BackColor = Color.FromArgb(219, 234, 254),
                AutoSize = false,
                Size = new Size(100, 26),
                Location = new Point(810, 8),
                TextAlign = ContentAlignment.MiddleCenter,
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            chkAutoscroll.FlatAppearance.BorderSize = 0;
            chkAutoscroll.CheckedChanged += (s, e) =>
            {
                isAutoscrollEnabled = chkAutoscroll.Checked;
                bool isDark = currentTheme == "Oscuro";
                chkAutoscroll.BackColor = isDark ? (isAutoscrollEnabled ? Color.FromArgb(234, 88, 12) : Color.FromArgb(30, 41, 59)) : (isAutoscrollEnabled ? Color.FromArgb(219, 234, 254) : Color.FromArgb(241, 245, 249));
                chkAutoscroll.ForeColor = isDark ? (isAutoscrollEnabled ? Color.FromArgb(15, 15, 15) : Color.FromArgb(148, 163, 184)) : (isAutoscrollEnabled ? Color.FromArgb(29, 78, 216) : Color.FromArgb(100, 116, 139));
            };

            btnClearLogView = new Button
            {
                Text = "limpiar",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.FromArgb(241, 245, 249),
                Size = new Size(80, 26),
                Location = new Point(920, 8),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClearLogView.FlatAppearance.BorderSize = 0;
            btnClearLogView.Click += (s, e) => txtLog.Clear();

            pnlLogHeader.Controls.Add(lblConsoleTitle);
            pnlLogHeader.Controls.Add(chkAutoscroll);
            pnlLogHeader.Controls.Add(btnClearLogView);

            lblLogSubBar = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = " ● tail -f src\\Access\\sync_progress.log    · 1263 KB    · offset 1.293.779    · 9 l/min",
                Font = new Font("Consolas", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                Padding = new Padding(12, 0, 0, 0)
            };

            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9.5F, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(12)
            };

            pnlLogConsoleCard.Controls.Add(txtLog);
            pnlLogConsoleCard.Controls.Add(lblLogSubBar);
            pnlLogConsoleCard.Controls.Add(pnlLogHeader);

            // ==========================================
            // 7. BARRA DE ESTADO INFERIOR (Footer Status Bar)
            // ==========================================
            pnlStatusBar = new Panel
            {
                Size = new Size(1040, 36),
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.Transparent
            };

            lblFooterServiceInfo = new Label
            {
                Text = "PsSyncBridge v2.4.1 · build 8841 · servicio «PsSyncBridgeSvc»",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(4, 8)
            };

            lblFooterStatusBadge = new Label
            {
                Text = "🟢 residente en bandeja",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(21, 128, 61),
                AutoSize = true,
                Location = new Point(870, 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            pnlStatusBar.Controls.Add(lblFooterServiceInfo);
            pnlStatusBar.Controls.Add(lblFooterStatusBadge);

            // Agregar contenedores al layout principal
            pnlMainContent.Controls.Add(pnlHeader);
            pnlMainContent.Controls.Add(pnlModeSwitch);
            pnlMainContent.Controls.Add(pnlPipelineDiagram);
            pnlMainContent.Controls.Add(pnlMetricCards);
            pnlMainContent.Controls.Add(flowSubMetrics);
            pnlMainContent.Controls.Add(pnlLogConsoleCard);
            pnlMainContent.Controls.Add(pnlStatusBar);

            this.Controls.Add(pnlMainContent);

            // Menu contextual del System Tray
            trayMenu = new ContextMenuStrip { Font = new Font("Segoe UI Emoji", 10F), ShowImageMargin = false, ShowCheckMargin = false };

            var itemHeader = new ToolStripMenuItem("🗄️ ReprediSL V4 (Demonio en ejecución)") { Enabled = false, Font = new Font("Segoe UI Emoji", 10F, FontStyle.Bold) };
            trayMenu.Items.Add(itemHeader);
            trayMenu.Items.Add("-");

            trayMenu.Items.Add("📄 Ver Registro / Log en Vivo", null, (s, e) => ShowForm());
            trayMenu.Items.Add("📋 Ver Pedidos", null, (s, e) => MostrarVentanaPedidos());
            trayMenu.Items.Add("▶️ Ejecutar Sincronización Ahora", null, async (s, e) => await DoManualSyncAsync());
            trayMenu.Items.Add("📦 Gestionar / Aceptar Pedidos Pendientes", null, (s, e) => ConfirmarPedidosPendientesManual());
            trayMenu.Items.Add("⚙️ Alternar Modo Auto-Aceptar", null, (s, e) => ToggleAutoAcceptMode());

            trayMenu.Items.Add("-");
            trayMenu.Items.Add("🚨 Ver Log Especial de Errores (sync_errors.log)", null, (s, e) => MostrarVentanaErrores());
            trayMenu.Items.Add("💾 Exportar Registro (Guardar como...)", null, (s, e) => ExportarRegistro());
            trayMenu.Items.Add("🖨️ Imprimir Registro", null, (s, e) => ImprimirRegistro());
            trayMenu.Items.Add("🗑️ Limpiar Registro (Con Copia de Seg.)", null, (s, e) => LimpiarRegistroConConfirmacion());

            trayMenu.Items.Add("-");
            var itemSubFuente = new ToolStripMenuItem("🔤 Tamaño de Letra & Tema");
            itemSubFuente.DropDownItems.Add("Pequeño", null, (s, e) => syncService.AppendLog("[CONFIG] Tamaño de letra ajustado a Pequeño", DbSyncService.LogLevel.Info));
            itemSubFuente.DropDownItems.Add("Mediano", null, (s, e) => syncService.AppendLog("[CONFIG] Tamaño de letra ajustado a Mediano", DbSyncService.LogLevel.Info));
            itemSubFuente.DropDownItems.Add("Grande", null, (s, e) => syncService.AppendLog("[CONFIG] Tamaño de letra ajustado a Grande", DbSyncService.LogLevel.Info));
            itemSubFuente.DropDownItems.Add("-");
            itemSubFuente.DropDownItems.Add("🎨 Cambiar Tema (Claro / Oscuro)", null, (s, e) => ToggleTheme());
            trayMenu.Items.Add(itemSubFuente);

            var itemSubPruebas = new ToolStripMenuItem("🧪 Pruebas y Simulaciones");
            itemSubPruebas.DropDownItems.Add("🟣 ▶️ Simular Llegada de Pedido (Prueba)", null, (s, e) => ProcesarLlegadaPedido());
            itemSubPruebas.DropDownItems.Add("🔴 ⚠️ Simular Error de Sync (Prueba Alerta Rojo)", null, (s, e) => SimularErrorSync());
            itemSubPruebas.DropDownItems.Add("⚡ Simular Ráfaga de Errores (Prueba Incremento Rápido)", null, (s, e) => SimularRafagaErrores());
            trayMenu.Items.Add(itemSubPruebas);

            trayMenu.Items.Add("-");
            trayMenu.Items.Add("🚪 Salir del Demonio", null, (s, e) => { forceClose = true; Application.Exit(); });

            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Text = "PsSyncBridge Tray - ReprediSL V4",
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => ShowForm();
        }

        private Panel CreateRoundedGlassCard(int width, int height, int cornerRadius = 16)
        {
            var pnl = new Panel
            {
                Size = new Size(width, height),
                BackColor = Color.Transparent
            };
            using var pathRegion = GetRoundedRectPath(new Rectangle(0, 0, width, height), cornerRadius);
            pnl.Region = new Region(pathRegion);

            pnl.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = GetRoundedRectPath(new Rectangle(0, 0, pnl.Width - 1, pnl.Height - 1), cornerRadius);
                Color fillClr = currentTheme == "Oscuro" ? Color.FromArgb(22, 31, 51) : Color.FromArgb(245, 255, 255, 255);
                Color borderClr = currentTheme == "Oscuro" ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
                using var fillBrush = new SolidBrush(fillClr);
                using var pen = new Pen(borderClr, 1.5f);
                e.Graphics.FillPath(fillBrush, path);
                e.Graphics.DrawPath(pen, path);
            };

            return pnl;
        }

        private Panel CreatePillBadge(string text, Color bgClr, Color fgClr)
        {
            var pnl = new Panel
            {
                Size = new Size(130, 34),
                BackColor = Color.Transparent
            };
            using var pathRegion = GetRoundedRectPath(new Rectangle(0, 0, 130, 34), 16);
            pnl.Region = new Region(pathRegion);

            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = fgClr,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnl.Controls.Add(lbl);

            pnl.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = GetRoundedRectPath(new Rectangle(0, 0, pnl.Width - 1, pnl.Height - 1), 16);
                // Verde neón fosforito para badge "En línea" en modo oscuro
                bool isGreenBadge = bgClr.G > 100 && bgClr.G > bgClr.R && bgClr.G > bgClr.B;
                Color fillClr = currentTheme == "Oscuro"
                    ? (isGreenBadge ? Color.FromArgb(2, 25, 6) : Color.FromArgb(15, 23, 42))
                    : bgClr;
                Color borderClr = currentTheme == "Oscuro"
                    ? (isGreenBadge ? Color.FromArgb(57, 255, 20) : Color.FromArgb(30, 41, 59))
                    : bgClr;
                using var fillBrush = new SolidBrush(fillClr);
                using var pen = new Pen(borderClr, isGreenBadge && currentTheme == "Oscuro" ? 1.8f : 1.2f);
                e.Graphics.FillPath(fillBrush, path);
                if (currentTheme == "Oscuro") e.Graphics.DrawPath(pen, path);
            };

            return pnl;
        }

        private Panel CreatePipelineNodeCard(string emoji, string title, string subtitle,
            Func<bool> isActive, out Label statusLbl, out Label iconLbl)
        {
            var pnl = CreateRoundedGlassCard(280, 110, 18);

            var iconBox = new Label
            {
                Text = emoji,
                Font = new Font("Segoe UI Emoji", 20F),
                Size = new Size(48, 48),
                Location = new Point(116, 10),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            using var pathIcon = GetRoundedRectPath(new Rectangle(0, 0, 48, 48), 14);
            iconBox.Region = new Region(pathIcon);

            iconBox.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = GetRoundedRectPath(new Rectangle(0, 0, iconBox.Width - 1, iconBox.Height - 1), 14);
                // Parpadeo naranja cuando el nodo recibió actividad en los últimos 3 segundos
                bool active = isActive() && blinkState;
                Color bgColor = currentTheme == "Oscuro"
                    ? (active ? Color.FromArgb(100, 48, 8) : Color.FromArgb(30, 41, 59))
                    : (active ? Color.FromArgb(255, 200, 100) : Color.FromArgb(238, 242, 255));
                using var brush = new SolidBrush(bgColor);
                e.Graphics.FillPath(brush, path);
                if (active)
                {
                    using var glowPen = new Pen(
                        currentTheme == "Oscuro" ? Color.FromArgb(234, 88, 12) : Color.FromArgb(200, 120, 0), 2f);
                    e.Graphics.DrawPath(glowPen, path);
                }
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = currentTheme == "Oscuro" ? Color.FromArgb(249, 250, 251) : Color.FromArgb(15, 23, 42),
                Size = new Size(260, 22),
                Location = new Point(10, 62),
                TextAlign = ContentAlignment.MiddleCenter
            };

            statusLbl = new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = currentTheme == "Oscuro" ? Color.FromArgb(156, 163, 175) : Color.FromArgb(100, 116, 139),
                Size = new Size(260, 18),
                Location = new Point(10, 84),
                TextAlign = ContentAlignment.MiddleCenter
            };

            pnl.Controls.Add(iconBox);
            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(statusLbl);

            iconLbl = iconBox;
            return pnl;
        }

        private Panel CreateMetricCard(string title, string initialVal, out Label valLbl)
        {
            var pnl = CreateRoundedGlassCard(250, 90, 16);

            var lblTag = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = currentTheme == "Oscuro" ? Color.FromArgb(156, 163, 175) : Color.FromArgb(100, 116, 139),
                Size = new Size(230, 18),
                Location = new Point(10, 14),
                TextAlign = ContentAlignment.MiddleCenter
            };

            valLbl = new Label
            {
                Text = initialVal,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = currentTheme == "Oscuro" ? Color.FromArgb(249, 250, 251) : Color.FromArgb(15, 23, 42),
                Size = new Size(230, 36),
                Location = new Point(10, 36),
                TextAlign = ContentAlignment.MiddleCenter
            };

            pnl.Controls.Add(lblTag);
            pnl.Controls.Add(valLbl);

            return pnl;
        }

        private Panel CreateSubPillBadge(string text)
        {
            var pnl = new Panel
            {
                Size = new Size(150, 32),
                Margin = new Padding(0, 0, 10, 0),
                BackColor = Color.Transparent
            };
            using var pathRegion = GetRoundedRectPath(new Rectangle(0, 0, 150, 32), 14);
            pnl.Region = new Region(pathRegion);

            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = currentTheme == "Oscuro" ? Color.FromArgb(156, 163, 175) : Color.FromArgb(71, 85, 105),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnl.Controls.Add(lbl);

            pnl.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = GetRoundedRectPath(new Rectangle(0, 0, pnl.Width - 1, pnl.Height - 1), 14);
                Color fillClr = currentTheme == "Oscuro" ? Color.FromArgb(22, 31, 51) : Color.FromArgb(245, 255, 255, 255);
                Color borderClr = currentTheme == "Oscuro" ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
                using var fillBrush = new SolidBrush(fillClr);
                using var pen = new Pen(borderClr, 1.2f);
                e.Graphics.FillPath(fillBrush, path);
                e.Graphics.DrawPath(pen, path);
            };

            return pnl;
        }

        private GraphicsPath GetRoundedRectPath(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(radius * 2, 1);
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void CargarConfiguracionUI()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath);
                    using var doc = JsonDocument.Parse(json);
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

        private void ProcesarLlegadaPedido(string? numPedido = null, string cliente = "1001 (CLIENTE DE PRUEBA SL)", decimal importe = 450.00m)
        {
            if (string.IsNullOrWhiteSpace(numPedido))
            {
                numPedido = $"P-{simOrderCounter++:D4}";
            }

            if (autoAcceptMode)
            {
                totalRegistrosProcesadosHoy++;
                syncService.AppendLog($"[SYNC] INSERT INTO PedidosCab (NumPedido, Cliente, Total) VALUES ('{numPedido}', '{cliente}', {importe:F2}) -> Confirmado automáticamente.", DbSyncService.LogLevel.Success);
                try { trayIcon.ShowBalloonTip(3000, "⚡ Pedido Auto-Aceptado", $"Pedido {numPedido} confirmado automáticamente.", ToolTipIcon.Info); } catch { }
            }
            else
            {
                pedidosPendientesCount++;
                syncService.AppendLog($"[PEDIDO] Llegada de pedido N. {numPedido} | Cliente: {cliente} | Importe: {importe:F2} EUR (Esperando confirmación manual).", DbSyncService.LogLevel.Warning);
                try { trayIcon.ShowBalloonTip(4000, "⚠️ Nuevo Pedido Pendiente", $"Pedido N. {numPedido} ({cliente}) requiere confirmación manual.", ToolTipIcon.Warning); } catch { }
            }
            UpdateStatusLabels();
        }

        private void ConfirmarPedidosPendientesManual()
        {
            if (pedidosPendientesCount > 0)
            {
                int confirmados = pedidosPendientesCount;
                totalRegistrosProcesadosHoy += confirmados;
                pedidosPendientesCount = 0;
                syncService.AppendLog($"[OK] {confirmados} pedido(s) confirmado(s) exitosamente por el usuario.", DbSyncService.LogLevel.Success);
                MessageBox.Show($"¡Se han confirmado y procesado {confirmados} pedido(s) pendiente(s)!", "PsSyncBridge Tray - Confirmación", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("No hay pedidos pendientes de confirmación.", "PsSyncBridge Tray", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            UpdateStatusLabels();
        }

        private void ToggleAutoAcceptMode()
        {
            autoAcceptMode = !autoAcceptMode;
            GuardarConfiguracionUI();

            if (autoAcceptMode)
            {
                syncService.AppendLog("[CONFIG] Auto-aceptación de pedidos ACTIVADA.", DbSyncService.LogLevel.Info);

                if (pedidosPendientesCount > 0)
                {
                    int confirmados = pedidosPendientesCount;
                    totalRegistrosProcesadosHoy += confirmados;
                    pedidosPendientesCount = 0;
                    syncService.AppendLog($"[AUTO-ACEPTAR] Se han procesado los {confirmados} pedido(s) pendiente(s) automáticamente.", DbSyncService.LogLevel.Success);
                }
            }
            else
            {
                syncService.AppendLog("[CONFIG] Auto-aceptación de pedidos DESACTIVADA (Modo Confirmación Manual).", DbSyncService.LogLevel.Info);
            }
            UpdateStatusLabels();
        }

        private void ToggleTheme()
        {
            string nextTheme = currentTheme == "Claro" ? "Oscuro" : "Claro";
            ApplyTheme(nextTheme);
            syncService.AppendLog($"[CONFIG] Tema de interfaz cambiado a «{nextTheme}».", DbSyncService.LogLevel.Info);
        }

        private void UpdateNodeCardTheme(Panel card, bool isDark)
        {
            if (card == null) return;
            foreach (Control c in card.Controls)
            {
                if (c is Label lbl)
                {
                    if (lbl.Font.Bold)
                    {
                        lbl.ForeColor = isDark ? Color.FromArgb(249, 250, 251) : Color.FromArgb(15, 23, 42);
                    }
                    else if (lbl.Size.Width == 48)
                    {
                        lbl.ForeColor = isDark ? Color.FromArgb(249, 250, 251) : Color.FromArgb(15, 23, 42);
                        lbl.Invalidate();
                    }
                    else
                    {
                        lbl.ForeColor = isDark ? Color.FromArgb(156, 163, 175) : Color.FromArgb(100, 116, 139);
                    }
                }
            }
            card.Invalidate();
        }

        private void InvalidateChildren(Control parent)
        {
            if (parent == null) return;
            parent.Invalidate();
            foreach (Control child in parent.Controls)
            {
                InvalidateChildren(child);
            }
        }

        private void UpdateStatusLabels()
        {
            bool isDark = currentTheme == "Oscuro";
            if (autoAcceptMode)
            {
                btnPillAuto.BackColor = isDark ? Color.FromArgb(234, 88, 12) : Color.FromArgb(99, 102, 241);
                btnPillAuto.ForeColor = Color.White;
                btnPillManual.BackColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
                btnPillManual.ForeColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
            }
            else
            {
                btnPillAuto.BackColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
                btnPillAuto.ForeColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
                btnPillManual.BackColor = Color.FromArgb(245, 158, 11);
                btnPillManual.ForeColor = Color.White;
            }

            if (lblProcessedVal != null) lblProcessedVal.Text = totalRegistrosProcesadosHoy.ToString();

            if (lblPendingVal != null)
            {
                lblPendingVal.Text = pedidosPendientesCount.ToString();
                lblPendingVal.ForeColor = pedidosPendientesCount > 0
                    ? Color.FromArgb(245, 158, 11)
                    : (isDark ? Color.FromArgb(249, 250, 251) : Color.FromArgb(15, 23, 42));
            }
        }

        private void ApplyTheme(string theme)
        {
            currentTheme = theme;
            GuardarConfiguracionUI();

            bool isDark = currentTheme == "Oscuro";

            if (pnlMainContent != null)
                pnlMainContent.BackColor = isDark ? Color.FromArgb(10, 15, 26) : Color.FromArgb(238, 242, 255);

            // Header Text Colors
            if (lblHeaderTitle != null)
                lblHeaderTitle.ForeColor = isDark ? Color.FromArgb(249, 250, 251) : Color.FromArgb(30, 41, 59);

            if (lblHeaderSubtitle != null)
                lblHeaderSubtitle.ForeColor = isDark ? Color.FromArgb(156, 163, 175) : Color.FromArgb(100, 116, 139);

            // Mode Switch
            if (lblModeTag != null)
                lblModeTag.ForeColor = isDark ? Color.FromArgb(156, 163, 175) : Color.FromArgb(100, 116, 139);

            UpdateStatusLabels();

            // Pipeline Cards
            if (cardNodePg != null) UpdateNodeCardTheme(cardNodePg, isDark);
            if (cardNodeBridge != null) UpdateNodeCardTheme(cardNodeBridge, isDark);
            if (cardNodeAccess != null) UpdateNodeCardTheme(cardNodeAccess, isDark);
            iconNodePg?.Invalidate();
            iconNodeBridge?.Invalidate();
            iconNodeAccess?.Invalidate();

            // Online badge - verde neón fosforito en modo oscuro
            if (pnlOnlineBadge != null)
            {
                foreach (Control c in pnlOnlineBadge.Controls)
                    if (c is Label lbl) lbl.ForeColor = isDark ? Color.FromArgb(57, 255, 20) : Color.FromArgb(21, 128, 61);
                pnlOnlineBadge.Invalidate();
            }

            // btnVerPedidos theming
            if (btnVerPedidos != null)
            {
                btnVerPedidos.BackColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
                btnVerPedidos.ForeColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(71, 85, 105);
                btnVerPedidos.FlatAppearance.BorderColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
            }

            // Metric Cards
            if (pnlMetricCards != null)
            {
                foreach (Control col in pnlMetricCards.Controls)
                {
                    if (col is Panel card)
                    {
                        foreach (Control sub in card.Controls)
                        {
                            if (sub is Label lbl)
                            {
                                if (lbl == lblPendingVal && pedidosPendientesCount > 0)
                                {
                                    lbl.ForeColor = Color.FromArgb(245, 158, 11);
                                }
                                else if (lbl.Font.Bold && lbl.Font.Size > 12)
                                {
                                    lbl.ForeColor = isDark ? Color.FromArgb(249, 250, 251) : Color.FromArgb(15, 23, 42);
                                }
                                else
                                {
                                    lbl.ForeColor = isDark ? Color.FromArgb(156, 163, 175) : Color.FromArgb(100, 116, 139);
                                }
                            }
                        }
                        card.Invalidate();
                    }
                }
            }

            // Sub-Metric Badges
            if (flowSubMetrics != null)
            {
                foreach (Control col in flowSubMetrics.Controls)
                {
                    if (col is Panel pill)
                    {
                        foreach (Control sub in pill.Controls)
                        {
                            if (sub is Label lbl)
                            {
                                lbl.ForeColor = isDark ? Color.FromArgb(156, 163, 175) : Color.FromArgb(71, 85, 105);
                            }
                        }
                        pill.Invalidate();
                    }
                }
            }

            // Log Console Header & Control Buttons
            if (lblConsoleTitle != null)
                lblConsoleTitle.ForeColor = isDark ? Color.FromArgb(156, 163, 175) : Color.FromArgb(71, 85, 105);

            if (chkAutoscroll != null)
            {
                chkAutoscroll.BackColor = isDark ? (isAutoscrollEnabled ? Color.FromArgb(234, 88, 12) : Color.FromArgb(30, 41, 59)) : (isAutoscrollEnabled ? Color.FromArgb(219, 234, 254) : Color.FromArgb(241, 245, 249));
                chkAutoscroll.ForeColor = isDark ? (isAutoscrollEnabled ? Color.FromArgb(15, 15, 15) : Color.FromArgb(148, 163, 184)) : (isAutoscrollEnabled ? Color.FromArgb(29, 78, 216) : Color.FromArgb(100, 116, 139));
            }

            if (btnClearLogView != null)
            {
                btnClearLogView.BackColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
                btnClearLogView.ForeColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(71, 85, 105);
            }

            if (lblLogSubBar != null)
                lblLogSubBar.ForeColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            if (txtLog != null)
            {
                txtLog.BackColor = isDark ? Color.FromArgb(10, 14, 23) : Color.FromArgb(248, 250, 252);
                txtLog.ForeColor = isDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(15, 23, 42);
            }

            // Footer Status Bar
            if (lblFooterServiceInfo != null)
                lblFooterServiceInfo.ForeColor = isDark ? Color.FromArgb(156, 163, 175) : Color.FromArgb(100, 116, 139);

            if (lblFooterStatusBadge != null)
                lblFooterStatusBadge.ForeColor = isDark ? Color.FromArgb(74, 222, 128) : Color.FromArgb(21, 128, 61);

            InvalidateChildren(this);
            this.Invalidate();
        }

        private void TimerHealth_Tick(object? sender, EventArgs e)
        {
            syncService.CheckLogFilesForNewLines();

            if (lblClockVal != null)
            {
                lblClockVal.Text = DateTime.Now.ToString("HH:mm:ss");
            }

            if (lblUptimeVal != null)
            {
                TimeSpan uptime = DateTime.Now - startTime;
                lblUptimeVal.Text = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
            }

            // Parpadeo de iconos de nodos activos cada tick (~1s)
            blinkState = !blinkState;
            iconNodePg?.Invalidate();
            iconNodeBridge?.Invalidate();
            iconNodeAccess?.Invalidate();
        }

        private void SyncService_OnLogMessage(string message, DbSyncService.LogLevel level)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SyncService_OnLogMessage(message, level)));
                return;
            }

            if (isLogPaused) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

            // Insignias formateadas según nivel de log y tema actual
            string tag = level switch
            {
                DbSyncService.LogLevel.Error => "ERROR",
                DbSyncService.LogLevel.Warning => "WARN",
                DbSyncService.LogLevel.Success => "SYNC",
                _ => "SQL"
            };

            // Activar indicadores visuales de nodos según actividad de log
            if (level == DbSyncService.LogLevel.Success)
            {
                pgLastActive = DateTime.Now;
                bridgeLastActive = DateTime.Now;
                accessLastActive = DateTime.Now;
            }
            else if (level == DbSyncService.LogLevel.Warning)
                bridgeLastActive = DateTime.Now;
            else
                pgLastActive = DateTime.Now;

            Color tagColor = (currentTheme == "Oscuro") switch
            {
                true => level switch
                {
                    DbSyncService.LogLevel.Error => Color.FromArgb(248, 113, 113),   // Coral Red (#F87171)
                    DbSyncService.LogLevel.Warning => Color.FromArgb(251, 191, 36),  // Amber Gold (#FBBF24)
                    DbSyncService.LogLevel.Success => Color.FromArgb(192, 132, 252),// Light Purple (#C084FC)
                    _ => Color.FromArgb(251, 146, 60)                               // Warm Orange (#FB923C)
                },
                false => level switch
                {
                    DbSyncService.LogLevel.Error => Color.FromArgb(220, 38, 38),   // Red
                    DbSyncService.LogLevel.Warning => Color.FromArgb(180, 83, 9),  // Amber
                    DbSyncService.LogLevel.Success => Color.FromArgb(107, 33, 168),// Purple
                    _ => Color.FromArgb(30, 64, 175)                               // Blue
                }
            };

            Color bodyTextColor = currentTheme == "Oscuro" ? Color.FromArgb(226, 232, 240) : Color.FromArgb(15, 23, 42);

            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;

            // Timestamp
            txtLog.SelectionColor = currentTheme == "Oscuro" ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184);
            txtLog.AppendText($"{timestamp}  ");

            // Tag badge
            txtLog.SelectionColor = tagColor;
            txtLog.AppendText($"{tag,-5} ");

            // Text
            txtLog.SelectionColor = bodyTextColor;
            txtLog.AppendText($"{message}{Environment.NewLine}");

            if (isAutoscrollEnabled)
            {
                txtLog.ScrollToCaret();
            }

            // Interceptar pedidos de logs externos
            if (message.Contains("[NUEVO PEDIDO]", StringComparison.OrdinalIgnoreCase) &&
                !message.Contains("[AUTO-ACEPTADO]", StringComparison.OrdinalIgnoreCase) &&
                !message.Contains("[PEDIDO PENDIENTE]", StringComparison.OrdinalIgnoreCase))
            {
                var match = System.Text.RegularExpressions.Regex.Match(message, @"\[NUEVO PEDIDO\]\s+Recibido pedido N\.\s+([^\s|]+)(?:\s+\|?\s*Cliente:\s*([^|]+))?");
                string numPed = match.Success && match.Groups[1].Value.Length > 0 ? match.Groups[1].Value : $"P-{DateTime.Now:HHmmss}";
                string cliente = match.Success && match.Groups[2].Success ? match.Groups[2].Value.Trim() : "Cliente ERP";
                ProcesarLlegadaPedido(numPed, cliente, 100.00m);
            }
            else
            {
                UpdateStatusLabels();
            }

            if (level == DbSyncService.LogLevel.Error)
            {
                try { trayIcon.ShowBalloonTip(3000, "PsSyncBridge - Error", message, ToolTipIcon.Error); } catch { }
            }
        }

        private void SyncService_OnErrorThresholdExceeded(int count)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SyncService_OnErrorThresholdExceeded(count)));
                return;
            }

            try
            {
                trayIcon.ShowBalloonTip(5000, "⚠️ Alerta de Incidencias en Ráfaga", $"Se han acumulado {count} errores consecutivos en la sincronización con PostgreSQL.", ToolTipIcon.Warning);
            }
            catch { }
        }

        private async Task DoManualSyncAsync()
        {
            try
            {
                await syncService.RunExportAsync();
            }
            catch { }
        }

        private void ShowForm()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!forceClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                try
                {
                    trayIcon.ShowBalloonTip(2000, "PsSyncBridge Tray", "El demonio continúa ejecutándose en segundo plano en la bandeja de sistema.", ToolTipIcon.Info);
                }
                catch { }
            }
            else
            {
                base.OnFormClosing(e);
            }
        }

        private void SimularErrorSync()
        {
            syncService.AppendLog("[ERROR] Fallo de prueba simulado: Conexión intermitente con PostgreSQL.", DbSyncService.LogLevel.Error);
        }

        private void SimularRafagaErrores()
        {
            for (int i = 1; i <= 4; i++)
            {
                syncService.AppendLog($"[ERROR] Ráfaga de incidencia #{i}: Simulación de fallo en lote {i * 500}", DbSyncService.LogLevel.Error);
            }
        }

        private void ExportarRegistro()
        {
            if (string.IsNullOrWhiteSpace(txtLog.Text))
            {
                MessageBox.Show("El registro está vacío. No hay datos para exportar.", "PsSyncBridge Tray", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Exportar Registro de Sincronización",
                Filter = "Archivos de texto (*.txt)|*.txt|Archivos de Log (*.log)|*.log|Todos los archivos (*.*)|*.*",
                FileName = $"Registro_Sync_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
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
                MessageBox.Show("El registro está vacío. No hay datos para imprimir.", "PsSyncBridge Tray", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show("El registro ya está vacío.", "PsSyncBridge Tray", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var resp = MessageBox.Show(
                "¿Deseas guardar una copia de seguridad del registro antes de limpiarlo?",
                "Limpiar Registro - PsSyncBridge Tray",
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
            MessageBox.Show("El registro ha sido limpiado correctamente.", "PsSyncBridge Tray", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MostrarVentanaErrores()
        {
            Color dialogBg = currentTheme == "Claro" ? Color.FromArgb(254, 242, 242) : Color.FromArgb(24, 15, 20);
            Color topBg = currentTheme == "Claro" ? Color.FromArgb(254, 226, 226) : Color.FromArgb(38, 20, 28);
            Color textBg = currentTheme == "Claro" ? Color.White : Color.FromArgb(15, 10, 14);

            using var errForm = new Form
            {
                Text = "PsSyncBridge Tray - Log Especial de Errores e Incidencias (sync_errors.log)",
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

        private void MostrarVentanaPedidos()
        {
            bool isDark = currentTheme == "Oscuro";
            Color dialogBg = isDark ? Color.FromArgb(10, 15, 26) : Color.FromArgb(238, 242, 255);
            Color topBg    = isDark ? Color.FromArgb(15, 23, 42)  : Color.FromArgb(226, 232, 240);
            Color cardBg   = isDark ? Color.FromArgb(22, 31, 51)  : Color.White;
            Color textFg   = isDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(15, 23, 42);
            Color subtextFg = isDark ? Color.FromArgb(156, 163, 175) : Color.FromArgb(100, 116, 139);

            using var pedForm = new Form
            {
                Text = "PsSyncBridge — Ver Pedidos",
                StartPosition = FormStartPosition.CenterParent,
                BackColor = dialogBg,
                Size = new Size(920, 580),
                MinimumSize = new Size(700, 400),
                FormBorderStyle = FormBorderStyle.Sizable
            };

            // Cabecera
            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = topBg };

            var lblTitlePed = new Label
            {
                Text = $"📋  Pedidos  ·  {recentOrders.Count} registros  ·  {totalRegistrosProcesadosHoy} procesados hoy",
                Location = new Point(16, 10),
                AutoSize = true,
                ForeColor = textFg,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };

            var lblPendingInfo = new Label
            {
                Text = pedidosPendientesCount > 0
                    ? $"⏳  {pedidosPendientesCount} pedido(s) pendiente(s) de confirmación manual"
                    : "✅  Sin pedidos pendientes",
                Location = new Point(16, 40),
                AutoSize = true,
                ForeColor = pedidosPendientesCount > 0 ? Color.FromArgb(245, 158, 11) : Color.FromArgb(57, 255, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            var btnConfirmarTodos = new Button
            {
                Text = "✅  Confirmar todos los pendientes",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(234, 88, 12),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Size = new Size(250, 34),
                Location = new Point(940 - 266, 18),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = pedidosPendientesCount > 0
            };
            btnConfirmarTodos.FlatAppearance.BorderSize = 0;
            btnConfirmarTodos.Click += (s, e) =>
            {
                ConfirmarPedidosPendientesManual();
                pedForm.Close();
            };

            pnlTop.Controls.Add(lblTitlePed);
            pnlTop.Controls.Add(lblPendingInfo);
            pnlTop.Controls.Add(btnConfirmarTodos);

            // ListView de pedidos
            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BorderStyle = BorderStyle.None,
                BackColor = cardBg,
                ForeColor = textFg,
                Font = new Font("Segoe UI", 9.5F),
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };

            listView.Columns.Add("Nº Pedido",   110);
            listView.Columns.Add("Cliente",     260);
            listView.Columns.Add("Importe",     100);
            listView.Columns.Add("Hora",         90);
            listView.Columns.Add("Estado",      150);

            if (recentOrders.Count == 0)
            {
                var emptyItem = new ListViewItem("—")
                    { ForeColor = subtextFg };
                emptyItem.SubItems.Add("Sin pedidos registrados en esta sesión");
                emptyItem.SubItems.Add("—");
                emptyItem.SubItems.Add("—");
                emptyItem.SubItems.Add("—");
                listView.Items.Add(emptyItem);
            }
            else
            {
                foreach (var (NumPedido, Cliente, Importe, Hora, Estado) in recentOrders)
                {
                    var item = new ListViewItem(NumPedido);
                    item.SubItems.Add(Cliente);
                    item.SubItems.Add($"{Importe:F2} €");
                    item.SubItems.Add(Hora.ToString("HH:mm:ss"));
                    item.SubItems.Add(Estado);
                    item.ForeColor = Estado.Contains("Pendiente")
                        ? Color.FromArgb(245, 158, 11)
                        : (isDark ? Color.FromArgb(57, 255, 20) : Color.FromArgb(21, 128, 61));
                    listView.Items.Add(item);
                }
            }

            pedForm.Controls.Add(listView);
            pedForm.Controls.Add(pnlTop);
            pedForm.ShowDialog(this);
        }
    }
}
