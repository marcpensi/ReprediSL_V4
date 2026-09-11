using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReprediTrayDaemon.Services
{
    public enum ServiceId
    {
        Postgres,
        Postgrest,
        Caddy,
        SyncPedidos,
        AccessErp
    }

    public enum ServiceStatusState
    {
        Detenido,
        Iniciando,
        Activo,
        Error
    }

    public class ServiceStatusModel
    {
        public ServiceId Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public ServiceStatusState State { get; set; } = ServiceStatusState.Detenido;
        public string Detail { get; set; } = "Detenido";
        public DateTime LastCheck { get; set; } = DateTime.MinValue;
        public int? ProcessId { get; set; }
    }

    public class ProcessManagerService
    {
        public string ProjectRoot { get; }
        private readonly ConcurrentDictionary<ServiceId, ServiceStatusModel> services = new();
        private readonly ConcurrentDictionary<ServiceId, Process> activeProcesses = new();
        private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(2) };

        public event Action<ServiceId, ServiceStatusModel>? OnServiceStatusChanged;
        public event Action<ServiceId, string, bool>? OnProcessOutput; // id, text, isError

        public DaemonConfig Config { get; }

        public ProcessManagerService(string projectRoot)
            : this(DaemonConfig.Load() ?? new DaemonConfig { BaseDir = projectRoot })
        {
        }

        public ProcessManagerService(DaemonConfig config)
        {
            Config = config;
            ProjectRoot = config.BaseDir;

            services[ServiceId.Postgres] = new ServiceStatusModel
            {
                Id = ServiceId.Postgres,
                DisplayName = "PostgreSQL 16",
                Subtitle = $"Puerto {config.PostgresPort} · {config.PostgresDatabase}"
            };

            services[ServiceId.Postgrest] = new ServiceStatusModel
            {
                Id = ServiceId.Postgrest,
                DisplayName = "PostgREST API",
                Subtitle = $"Puerto {config.PostgrestPort} · REST / Open-API"
            };

            services[ServiceId.Caddy] = new ServiceStatusModel
            {
                Id = ServiceId.Caddy,
                DisplayName = "Caddy Reverse Proxy",
                Subtitle = $"Puertos {config.CaddyPort}/443 · SSL api.repredisl.com"
            };

            services[ServiceId.SyncPedidos] = new ServiceStatusModel
            {
                Id = ServiceId.SyncPedidos,
                DisplayName = "Sincronizador Pedidos",
                Subtitle = "Consumo pedidos_nuevos → gestion.mdb"
            };

            services[ServiceId.AccessErp] = new ServiceStatusModel
            {
                Id = ServiceId.AccessErp,
                DisplayName = "Access ERP (gestion.mdb)",
                Subtitle = config.AccessMdbPath
            };
        }

        public ServiceStatusModel GetStatus(ServiceId id)
        {
            return services.TryGetValue(id, out var status) ? status : new ServiceStatusModel { Id = id };
        }

        public string ResolveCaddyPath()
        {
            string cfg = Config.ResolveFullPath(Config.CaddyExecutable);
            if (!string.IsNullOrEmpty(cfg) && File.Exists(cfg)) return cfg;

            string[] candidates = new string[]
            {
                "caddy.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims", "caddy.exe"),
                Path.Combine(ProjectRoot, "Caddy", "caddy.exe"),
                Path.Combine(ProjectRoot, "src", "API", "caddy.exe")
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand)) return cand;
            }

            return "caddy";
        }

        public string ResolvePostgrestPath()
        {
            string cfg = Config.ResolveFullPath(Config.PostgrestExecutable);
            if (!string.IsNullOrEmpty(cfg) && File.Exists(cfg)) return cfg;

            string[] candidates = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "postgrest.exe"),
                Path.Combine(ProjectRoot, "ReprediTrayDaemon", "postgrest.exe"),
                Path.Combine(ProjectRoot, "src", "API", "postgrest.exe"),
                "postgrest.exe"
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand)) return cand;
            }

            return "postgrest.exe";
        }

        public string ResolvePostgrestConfig()
        {
            string cfg = Config.ResolveFullPath(Config.PostgrestConfig);
            if (!string.IsNullOrEmpty(cfg) && File.Exists(cfg)) return cfg;

            string[] candidates = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "postgrest.conf"),
                Path.Combine(ProjectRoot, "ReprediTrayDaemon", "postgrest.conf"),
                Path.Combine(ProjectRoot, "src", "API", "postgrest.conf")
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand)) return cand;
            }

            return Path.Combine(ProjectRoot, "src", "API", "postgrest.conf");
        }

        public string ResolveCaddyfile()
        {
            string cfg = Config.ResolveFullPath(Config.Caddyfile);
            if (!string.IsNullOrEmpty(cfg) && File.Exists(cfg)) return cfg;

            string[] candidates = new string[]
            {
                Path.Combine(ProjectRoot, "Caddy", "Caddyfile"),
                Path.Combine(ProjectRoot, "src", "API", "Caddyfile")
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand)) return cand;
            }

            return Path.Combine(ProjectRoot, "src", "API", "Caddyfile");
        }

        public string? ResolveSyncExecutable()
        {
            string cfg = Config.ResolveFullPath(Config.SyncExecutable);
            if (!string.IsNullOrEmpty(cfg) && File.Exists(cfg)) return cfg;

            string[] candidates = new string[]
            {
                Path.Combine(ProjectRoot, "Sync", "sincronizador.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sincronizador.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Sync", "sincronizador.exe")
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand)) return cand;
            }

            return null;
        }

        public string ResolveSyncScript()
        {
            string cfg = Config.ResolveFullPath(Config.SyncScript);
            if (!string.IsNullOrEmpty(cfg) && File.Exists(cfg)) return cfg;

            string[] candidates = new string[]
            {
                Path.Combine(ProjectRoot, "Sync", "SincronizarPedidosEntrantes.ps1"),
                Path.Combine(ProjectRoot, "Scripts", "BaseDatos", "SincronizarPedidosEntrantes.ps1")
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand)) return cand;
            }

            return candidates[0];
        }

        public string ResolveAccessMdbPath()
        {
            if (!string.IsNullOrEmpty(Config.AccessMdbPath) && File.Exists(Config.AccessMdbPath))
            {
                return Config.AccessMdbPath;
            }

            string[] candidates = new string[]
            {
                @"C:\pensi\psgestw\e0012026\gestion.mdb",
                Path.Combine(ProjectRoot, "src", "Access", "E0012026", "gestion.mdb"),
                Path.Combine(ProjectRoot, "src", "Access", "gestion.mdb")
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand)) return cand;
            }

            return candidates[0];
        }

        private void UpdateServiceState(ServiceId id, ServiceStatusState state, string detail, int? pid = null)
        {
            if (services.TryGetValue(id, out var model))
            {
                bool changed = model.State != state || model.Detail != detail;
                model.State = state;
                model.Detail = detail;
                model.LastCheck = DateTime.Now;
                if (pid.HasValue) model.ProcessId = pid.Value;
                else if (state == ServiceStatusState.Detenido) model.ProcessId = null;

                if (changed)
                {
                    OnServiceStatusChanged?.Invoke(id, model);
                }
            }
        }

        public async Task CheckAllHealthAsync()
        {
            await CheckPostgresAsync();
            await CheckPostgrestAsync();
            await CheckCaddyAsync();
            await CheckSyncPedidosAsync();
            CheckAccessErp();
        }

        public async Task<bool> CheckPostgresAsync()
        {
            bool ok = false;
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync("127.0.0.1", 5432);
                var completed = await Task.WhenAny(connectTask, Task.Delay(500));
                if (completed == connectTask && tcp.Connected)
                {
                    ok = true;
                }
            }
            catch { }

            if (ok)
            {
                UpdateServiceState(ServiceId.Postgres, ServiceStatusState.Activo, "🟢 Escuchando en 127.0.0.1:5432");
            }
            else
            {
                UpdateServiceState(ServiceId.Postgres, ServiceStatusState.Detenido, "🔴 Puerto 5432 cerrado / Inactivo");
            }
            return ok;
        }

        public async Task<bool> CheckPostgrestAsync()
        {
            bool ok = false;
            int? pid = activeProcesses.TryGetValue(ServiceId.Postgrest, out var p) && !p.HasExited ? p.Id : null;

            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync("127.0.0.1", 3000);
                var completed = await Task.WhenAny(connectTask, Task.Delay(500));
                if (completed == connectTask && tcp.Connected)
                {
                    ok = true;
                }
            }
            catch { }

            if (ok)
            {
                string info = pid.HasValue ? $"🟢 Activo (PID {pid}, :3000)" : "🟢 Activo en 127.0.0.1:3000";
                UpdateServiceState(ServiceId.Postgrest, ServiceStatusState.Activo, info, pid);
            }
            else
            {
                UpdateServiceState(ServiceId.Postgrest, ServiceStatusState.Detenido, "🔴 Detenido (Puerto 3000 libre)");
            }
            return ok;
        }

        public async Task<bool> CheckCaddyAsync()
        {
            bool isProcessRunning = false;
            int? pid = activeProcesses.TryGetValue(ServiceId.Caddy, out var p) && !p.HasExited ? p.Id : null;

            if (!pid.HasValue)
            {
                var procs = Process.GetProcessesByName("caddy");
                if (procs.Length > 0)
                {
                    isProcessRunning = true;
                    pid = procs[0].Id;
                }
            }
            else
            {
                isProcessRunning = true;
            }

            // Also check TCP 80 or 443
            bool portOk = false;
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync("127.0.0.1", 80);
                var completed = await Task.WhenAny(connectTask, Task.Delay(400));
                if (completed == connectTask && tcp.Connected) portOk = true;
            }
            catch { }

            if (isProcessRunning || portOk)
            {
                string info = pid.HasValue ? $"🟢 Activo (PID {pid}, SSL 443)" : "🟢 Activo en puertos 80/443";
                UpdateServiceState(ServiceId.Caddy, ServiceStatusState.Activo, info, pid);
                return true;
            }
            else
            {
                UpdateServiceState(ServiceId.Caddy, ServiceStatusState.Detenido, "🔴 Detenido (Puertos 80/443 libres)");
                return false;
            }
        }

        public async Task<bool> CheckSyncPedidosAsync()
        {
            bool isRunning = false;
            int? pid = activeProcesses.TryGetValue(ServiceId.SyncPedidos, out var p) && !p.HasExited ? p.Id : null;

            if (pid.HasValue)
            {
                isRunning = true;
            }
            else
            {
                // Buscar si hay proceso sincronizador o powershell corriendo
                try
                {
                    var syncProcs = Process.GetProcessesByName("sincronizador");
                    if (syncProcs.Length > 0 && !syncProcs[0].HasExited)
                    {
                        pid = syncProcs[0].Id;
                        isRunning = true;
                    }
                    else
                    {
                        var procs = Process.GetProcessesByName("powershell");
                        foreach (var proc in procs)
                        {
                            // En proceso powershell
                        }
                    }
                }
                catch { }
            }

            // Also verify log file freshness
            string logPath = Path.Combine(Config.ResolveFullPath(Config.LogsDir), "sync_progress.log");
            if (!File.Exists(logPath))
            {
                logPath = Path.Combine(ProjectRoot, "src", "Access", "sync_progress.log");
            }
            DateTime lastWrite = File.Exists(logPath) ? File.GetLastWriteTime(logPath) : DateTime.MinValue;
            bool recentlyActive = (DateTime.Now - lastWrite).TotalSeconds < 30;

            if (isRunning || recentlyActive)
            {
                string info = pid.HasValue ? $"🟢 Activo (PID {pid}, loop {Config.SyncIntervalSeconds}s)" : "🟢 En ejecución en segundo plano";
                UpdateServiceState(ServiceId.SyncPedidos, ServiceStatusState.Activo, info, pid);
                return true;
            }
            else
            {
                UpdateServiceState(ServiceId.SyncPedidos, ServiceStatusState.Detenido, "🔴 Detenido (Inactivo)");
                return false;
            }
        }

        public bool CheckAccessErp()
        {
            string mdb = ResolveAccessMdbPath();
            services[ServiceId.AccessErp].Subtitle = mdb;

            if (File.Exists(mdb))
            {
                try
                {
                    using var fs = new FileStream(mdb, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    long sizeKb = fs.Length / 1024;
                    UpdateServiceState(ServiceId.AccessErp, ServiceStatusState.Activo, $"🟢 Conectado ({sizeKb:N0} KB)");
                    return true;
                }
                catch (Exception ex)
                {
                    UpdateServiceState(ServiceId.AccessErp, ServiceStatusState.Error, $"⚠️ Bloqueado: {ex.Message}");
                    return false;
                }
            }
            else
            {
                UpdateServiceState(ServiceId.AccessErp, ServiceStatusState.Detenido, "🔴 Archivo .mdb no encontrado");
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // PASO 0: Actualizar Base de Datos Access antes de arrancar
        // ─────────────────────────────────────────────────────────────
        public async Task<bool> ActualizarBaseDatosAccessAsync()
        {
            string ps1 = Path.Combine(ProjectRoot, "Scripts", "Migracion", "MigrarYActualizarAccess.ps1");
            if (!File.Exists(ps1))
            {
                OnProcessOutput?.Invoke(ServiceId.AccessErp,
                    $"[ERROR] No se encuentra el script de actualización: {ps1}", true);
                return false;
            }

            OnProcessOutput?.Invoke(ServiceId.AccessErp,
                "⚙️  [BD Access] Iniciando actualización automática de la base de datos...", false);
            UpdateServiceState(ServiceId.AccessErp, ServiceStatusState.Iniciando, "Actualizando BD...");

            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{ps1}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = ProjectRoot
                };

                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                proc.Start();

                // Leer stdout y stderr asíncronamente para evitar deadlocks y CA2024
                var stdoutTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
                        OnProcessOutput?.Invoke(ServiceId.AccessErp, "  " + line, false);
                });
                var stderrTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await proc.StandardError.ReadLineAsync()) != null)
                        OnProcessOutput?.Invoke(ServiceId.AccessErp, "  [ERR] " + line, true);
                });

                await Task.WhenAll(stdoutTask, stderrTask);
                await proc.WaitForExitAsync();

                if (proc.ExitCode == 0)
                {
                    OnProcessOutput?.Invoke(ServiceId.AccessErp,
                        "✅  [BD Access] Base de datos actualizada correctamente.", false);
                    UpdateServiceState(ServiceId.AccessErp, ServiceStatusState.Activo, "BD actualizada");
                    return true;
                }
                else
                {
                    OnProcessOutput?.Invoke(ServiceId.AccessErp,
                        $"⚠️  [BD Access] El script terminó con código {proc.ExitCode}. Revisa el log.", true);
                    UpdateServiceState(ServiceId.AccessErp, ServiceStatusState.Error,
                        $"Exit code {proc.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnProcessOutput?.Invoke(ServiceId.AccessErp,
                    $"[ERROR] Excepción al actualizar BD Access: {ex.Message}", true);
                UpdateServiceState(ServiceId.AccessErp, ServiceStatusState.Error, ex.Message);
                return false;
            }
        }

        public async Task StartAllServicesAsync()
        {
            OnProcessOutput?.Invoke(ServiceId.Postgres, "--- INICIANDO TODOS LOS SERVICIOS DE REPREDISL V4 ---", false);

            // PASO 0: Actualizar la base de datos Access antes de arrancar servicios
            bool bdOk = await ActualizarBaseDatosAccessAsync();
            if (!bdOk)
            {
                OnProcessOutput?.Invoke(ServiceId.Postgres,
                    "⚠️  La actualización de BD no completó sin errores. Los servicios se arrancarán de todas formas.", true);
            }

            // 1. Check PostgreSQL
            bool pgOk = await CheckPostgresAsync();
            if (!pgOk)
            {
                OnProcessOutput?.Invoke(ServiceId.Postgres, "Intentando levantar servicio PostgreSQL...", false);
                try
                {
                    var psi = new ProcessStartInfo("net", "start postgresql-x64-16")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null) await proc.WaitForExitAsync();
                }
                catch { }
                await Task.Delay(1000);
                pgOk = await CheckPostgresAsync();
            }

            // 2. Start PostgREST
            await StartPostgrestAsync();
            await Task.Delay(1500);

            // 3. Start Caddy
            await StartCaddyAsync();
            await Task.Delay(1500);

            // 4. Start SyncPedidos
            await StartSyncPedidosAsync();

            await CheckAllHealthAsync();
            OnProcessOutput?.Invoke(ServiceId.SyncPedidos, "--- TODOS LOS SERVICIOS PROCESADOS ---", false);
        }

        public async Task StopAllServicesAsync()
        {
            OnProcessOutput?.Invoke(ServiceId.SyncPedidos, "--- DETENIENDO TODOS LOS SERVICIOS ---", false);

            await StopSyncPedidosAsync();
            await StopCaddyAsync();
            await StopPostgrestAsync();

            await CheckAllHealthAsync();
            OnProcessOutput?.Invoke(ServiceId.Postgrest, "--- SERVICIOS DETENIDOS ---", false);
        }

        public async Task RestartAllServicesAsync()
        {
            await StopAllServicesAsync();
            await Task.Delay(1500);
            await StartAllServicesAsync();
        }

        public async Task StartPostgrestAsync()
        {
            if (await CheckPostgrestAsync())
            {
                OnProcessOutput?.Invoke(ServiceId.Postgrest, "[PostgREST] Ya está en ejecución.", false);
                return;
            }

            string exe = ResolvePostgrestPath();
            string conf = ResolvePostgrestConfig();

            if (!File.Exists(exe))
            {
                OnProcessOutput?.Invoke(ServiceId.Postgrest, $"[ERROR] No se encuentra {exe}", true);
                UpdateServiceState(ServiceId.Postgrest, ServiceStatusState.Error, "Executable missing");
                return;
            }

            UpdateServiceState(ServiceId.Postgrest, ServiceStatusState.Iniciando, "Iniciando...");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"\"{conf}\"",
                    WorkingDirectory = Path.GetDirectoryName(conf),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

                proc.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OnProcessOutput?.Invoke(ServiceId.Postgrest, $"[PostgREST] {e.Data}", false);
                };

                proc.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OnProcessOutput?.Invoke(ServiceId.Postgrest, $"[PostgREST ERR] {e.Data}", true);
                };

                proc.Exited += (s, e) =>
                {
                    activeProcesses.TryRemove(ServiceId.Postgrest, out _);
                    UpdateServiceState(ServiceId.Postgrest, ServiceStatusState.Detenido, "🔴 PostgREST detenido");
                    OnProcessOutput?.Invoke(ServiceId.Postgrest, "[PostgREST] Proceso finalizado.", false);
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                activeProcesses[ServiceId.Postgrest] = proc;
                UpdateServiceState(ServiceId.Postgrest, ServiceStatusState.Activo, $"🟢 Activo (PID {proc.Id}, :3000)", proc.Id);
                OnProcessOutput?.Invoke(ServiceId.Postgrest, $"[PostgREST] Iniciado con éxito en background (PID {proc.Id})", false);
            }
            catch (Exception ex)
            {
                UpdateServiceState(ServiceId.Postgrest, ServiceStatusState.Error, $"Error: {ex.Message}");
                OnProcessOutput?.Invoke(ServiceId.Postgrest, $"[ERROR PostgREST] {ex.Message}", true);
            }
        }

        public async Task StopPostgrestAsync()
        {
            if (activeProcesses.TryRemove(ServiceId.Postgrest, out var proc))
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(true);
                        await proc.WaitForExitAsync();
                    }
                }
                catch { }
            }

            // Also kill any remaining postgrest.exe
            try
            {
                foreach (var p in Process.GetProcessesByName("postgrest"))
                {
                    try { p.Kill(true); } catch { }
                }
            }
            catch { }

            UpdateServiceState(ServiceId.Postgrest, ServiceStatusState.Detenido, "🔴 Detenido");
            OnProcessOutput?.Invoke(ServiceId.Postgrest, "[PostgREST] Servicio detenido.", false);
        }

        public async Task StartCaddyAsync()
        {
            if (await CheckCaddyAsync())
            {
                OnProcessOutput?.Invoke(ServiceId.Caddy, "[Caddy] Ya está en ejecución.", false);
                return;
            }

            string caddyBin = ResolveCaddyPath();
            string caddyfile = ResolveCaddyfile();

            if (!File.Exists(caddyfile))
            {
                OnProcessOutput?.Invoke(ServiceId.Caddy, $"[ERROR] No se encuentra Caddyfile: {caddyfile}", true);
                UpdateServiceState(ServiceId.Caddy, ServiceStatusState.Error, "Caddyfile no encontrado");
                return;
            }

            UpdateServiceState(ServiceId.Caddy, ServiceStatusState.Iniciando, "Iniciando...");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = caddyBin,
                    Arguments = $"run --config \"{caddyfile}\"",
                    WorkingDirectory = Path.GetDirectoryName(caddyfile),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

                proc.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OnProcessOutput?.Invoke(ServiceId.Caddy, $"[Caddy] {e.Data}", false);
                };

                proc.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OnProcessOutput?.Invoke(ServiceId.Caddy, $"[Caddy] {e.Data}", false);
                };

                proc.Exited += (s, e) =>
                {
                    activeProcesses.TryRemove(ServiceId.Caddy, out _);
                    UpdateServiceState(ServiceId.Caddy, ServiceStatusState.Detenido, "🔴 Caddy detenido");
                    OnProcessOutput?.Invoke(ServiceId.Caddy, "[Caddy] Proceso finalizado.", false);
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                activeProcesses[ServiceId.Caddy] = proc;
                UpdateServiceState(ServiceId.Caddy, ServiceStatusState.Activo, $"🟢 Activo (PID {proc.Id}, SSL 443)", proc.Id);
                OnProcessOutput?.Invoke(ServiceId.Caddy, $"[Caddy] Iniciado con éxito en background (PID {proc.Id})", false);
            }
            catch (Exception ex)
            {
                UpdateServiceState(ServiceId.Caddy, ServiceStatusState.Error, $"Error: {ex.Message}");
                OnProcessOutput?.Invoke(ServiceId.Caddy, $"[ERROR Caddy] {ex.Message}", true);
            }
        }

        public async Task StopCaddyAsync()
        {
            if (activeProcesses.TryRemove(ServiceId.Caddy, out var proc))
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(true);
                        await proc.WaitForExitAsync();
                    }
                }
                catch { }
            }

            // Also kill any system caddy.exe
            try
            {
                foreach (var p in Process.GetProcessesByName("caddy"))
                {
                    try { p.Kill(true); } catch { }
                }
            }
            catch { }

            UpdateServiceState(ServiceId.Caddy, ServiceStatusState.Detenido, "🔴 Detenido");
            OnProcessOutput?.Invoke(ServiceId.Caddy, "[Caddy] Servicio detenido.", false);
        }

        public async Task StartSyncPedidosAsync()
        {
            if (await CheckSyncPedidosAsync())
            {
                OnProcessOutput?.Invoke(ServiceId.SyncPedidos, "[Sync] Ya está en ejecución.", false);
                return;
            }

            string? syncExe = ResolveSyncExecutable();
            ProcessStartInfo psi;
            string runDesc;

            if (!string.IsNullOrEmpty(syncExe) && File.Exists(syncExe))
            {
                runDesc = $"sincronizador.exe (Loop {Config.SyncIntervalSeconds}s)";
                psi = new ProcessStartInfo
                {
                    FileName = syncExe,
                    Arguments = $"-Loop -IntervaloSegundos {Config.SyncIntervalSeconds}",
                    WorkingDirectory = Path.GetDirectoryName(syncExe),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
            }
            else
            {
                string script = ResolveSyncScript();
                if (!File.Exists(script))
                {
                    OnProcessOutput?.Invoke(ServiceId.SyncPedidos, $"[ERROR] No se encuentra script ni ejecutable de sincronización", true);
                    UpdateServiceState(ServiceId.SyncPedidos, ServiceStatusState.Error, "Sincronizador no encontrado");
                    return;
                }

                runDesc = $"PowerShell ({Path.GetFileName(script)})";
                psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Loop -IntervaloSegundos {Config.SyncIntervalSeconds}",
                    WorkingDirectory = Path.GetDirectoryName(script) ?? ProjectRoot,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
            }

            UpdateServiceState(ServiceId.SyncPedidos, ServiceStatusState.Iniciando, "Iniciando...");

            try
            {
                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

                proc.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OnProcessOutput?.Invoke(ServiceId.SyncPedidos, $"[Sync] {e.Data}", false);
                };

                proc.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OnProcessOutput?.Invoke(ServiceId.SyncPedidos, $"[Sync ERR] {e.Data}", true);
                };

                proc.Exited += (s, e) =>
                {
                    activeProcesses.TryRemove(ServiceId.SyncPedidos, out _);
                    UpdateServiceState(ServiceId.SyncPedidos, ServiceStatusState.Detenido, "🔴 Sincronizador detenido");
                    OnProcessOutput?.Invoke(ServiceId.SyncPedidos, "[Sync] Proceso de sincronización finalizado.", false);
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                activeProcesses[ServiceId.SyncPedidos] = proc;
                UpdateServiceState(ServiceId.SyncPedidos, ServiceStatusState.Activo, $"🟢 Activo (PID {proc.Id})", proc.Id);
                OnProcessOutput?.Invoke(ServiceId.SyncPedidos, $"[Sync] {runDesc} iniciado en segundo plano (PID {proc.Id})", false);
            }
            catch (Exception ex)
            {
                UpdateServiceState(ServiceId.SyncPedidos, ServiceStatusState.Error, $"Error: {ex.Message}");
                OnProcessOutput?.Invoke(ServiceId.SyncPedidos, $"[ERROR Sync] {ex.Message}", true);
            }
        }

        public async Task StopSyncPedidosAsync()
        {
            if (activeProcesses.TryRemove(ServiceId.SyncPedidos, out var proc))
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(true);
                        await proc.WaitForExitAsync();
                    }
                }
                catch { }
            }

            // Also kill any orphaned sincronizador processes
            try
            {
                foreach (var p in Process.GetProcessesByName("sincronizador"))
                {
                    try { p.Kill(true); } catch { }
                }
            }
            catch { }

            // Also kill powershell instances running SincronizarPedidosEntrantes
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -Command \"Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*SincronizarPedidosEntrantes.ps1*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var p = Process.Start(psi);
                if (p != null) await p.WaitForExitAsync();
            }
            catch { }

            UpdateServiceState(ServiceId.SyncPedidos, ServiceStatusState.Detenido, "🔴 Detenido");
            OnProcessOutput?.Invoke(ServiceId.SyncPedidos, "[Sync] Sincronizador detenido.", false);
        }
    }
}
