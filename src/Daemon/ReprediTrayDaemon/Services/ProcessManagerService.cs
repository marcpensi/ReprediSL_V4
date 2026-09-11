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
        public string ProjectRoot => Config.ProjectRoot;
        private readonly ConcurrentDictionary<ServiceId, ServiceStatusModel> services = new();
        private readonly ConcurrentDictionary<ServiceId, Process> activeProcesses = new();
        private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(2) };

        public event Action<ServiceId, ServiceStatusModel>? OnServiceStatusChanged;
        public event Action<ServiceId, string, bool>? OnProcessOutput; // id, text, isError

        public DaemonConfig Config { get; }

        public ProcessManagerService(string projectRoot)
            : this(DaemonConfig.Load())
        {
        }

        public ProcessManagerService(DaemonConfig config)
        {
            Config = config ?? DaemonConfig.Load();
            Config.EnsureDirectories();

            services[ServiceId.Postgres] = new ServiceStatusModel
            {
                Id = ServiceId.Postgres,
                DisplayName = $"PostgreSQL ({Config.PostgreSQL.ServiceName})",
                Subtitle = $"Puerto {Config.PostgreSQL.Port} · {Config.PostgreSQL.Database}"
            };

            services[ServiceId.Postgrest] = new ServiceStatusModel
            {
                Id = ServiceId.Postgrest,
                DisplayName = "PostgREST API",
                Subtitle = $"Puerto {Config.PostgREST.Port} · REST / Open-API"
            };

            services[ServiceId.Caddy] = new ServiceStatusModel
            {
                Id = ServiceId.Caddy,
                DisplayName = "Caddy Reverse Proxy",
                Subtitle = $"Puertos {Config.Caddy.HttpPort}/{Config.Caddy.HttpsPort} · SSL"
            };

            services[ServiceId.SyncPedidos] = new ServiceStatusModel
            {
                Id = ServiceId.SyncPedidos,
                DisplayName = "Sincronizador Pedidos",
                Subtitle = $"Consumo pedidos_nuevos → {Config.PsGest.DatabaseFile}"
            };

            services[ServiceId.AccessErp] = new ServiceStatusModel
            {
                Id = ServiceId.AccessErp,
                DisplayName = $"Access ERP ({Config.PsGest.DatabaseFile})",
                Subtitle = Config.AccessDbPath
            };
        }

        public ServiceStatusModel GetStatus(ServiceId id)
        {
            return services.TryGetValue(id, out var status) ? status : new ServiceStatusModel { Id = id };
        }

        public string ResolveCaddyPath()
        {
            string cfg = Config.CaddyExecutable;
            if (File.Exists(cfg)) return cfg;

            string devCaddy = Path.Combine(ProjectRoot, "src", "API", "caddy.exe");
            if (File.Exists(devCaddy)) return devCaddy;

            return "caddy.exe";
        }

        public string ResolvePostgrestPath()
        {
            string cfg = Config.PostgrestExecutable;
            if (File.Exists(cfg)) return cfg;

            string appDirPostgrest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "postgrest.exe");
            if (File.Exists(appDirPostgrest)) return appDirPostgrest;

            string devPostgrest = Path.Combine(ProjectRoot, "src", "API", "postgrest.exe");
            if (File.Exists(devPostgrest)) return devPostgrest;

            return "postgrest.exe";
        }

        public string ResolvePostgrestConfig()
        {
            string cfg = Config.PostgrestConfig;
            if (File.Exists(cfg)) return cfg;

            string appDirConf = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "postgrest.conf");
            if (File.Exists(appDirConf)) return appDirConf;

            string devConf = Path.Combine(ProjectRoot, "src", "API", "postgrest.conf");
            if (File.Exists(devConf)) return devConf;

            return cfg;
        }

        public string ResolveCaddyfile()
        {
            string cfg = Config.Caddyfile;
            if (File.Exists(cfg)) return cfg;

            string devCaddyfile = Path.Combine(ProjectRoot, "src", "API", "Caddyfile");
            if (File.Exists(devCaddyfile)) return devCaddyfile;

            return cfg;
        }

        public string? ResolveSyncExecutable()
        {
            string cfg = Config.SyncExecutable;
            if (File.Exists(cfg)) return cfg;

            string appDirSync = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sincronizador.exe");
            if (File.Exists(appDirSync)) return appDirSync;

            string devSync = Path.Combine(ProjectRoot, "src", "Sync", "ReprediSync", "bin", "Release", "net10.0-windows", "sincronizador.exe");
            if (File.Exists(devSync)) return devSync;

            return null;
        }

        public string ResolveSyncScript()
        {
            string cfg = Config.SyncScript;
            if (File.Exists(cfg)) return cfg;

            string script = Path.Combine(Config.ScriptsDir, "BaseDatos", "SincronizarPedidosEntrantes.ps1");
            if (File.Exists(script)) return script;

            return cfg;
        }

        public string ResolveAccessMdbPath()
        {
            return Config.AccessDbPath;
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
            string host = string.IsNullOrWhiteSpace(Config.PostgreSQL.Host) ? "127.0.0.1" : Config.PostgreSQL.Host;
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) host = "127.0.0.1";
            int port = Config.PostgreSQL.Port;

            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync(host, port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(500));
                if (completed == connectTask && tcp.Connected)
                {
                    ok = true;
                }
            }
            catch { }

            if (ok)
            {
                UpdateServiceState(ServiceId.Postgres, ServiceStatusState.Activo, $"🟢 Escuchando en {host}:{port}");
            }
            else
            {
                UpdateServiceState(ServiceId.Postgres, ServiceStatusState.Detenido, $"🔴 Puerto {port} cerrado / Inactivo");
            }
            return ok;
        }

        public async Task<bool> CheckPostgrestAsync()
        {
            bool ok = false;
            int? pid = activeProcesses.TryGetValue(ServiceId.Postgrest, out var p) && !p.HasExited ? p.Id : null;
            int port = Config.PostgREST.Port;

            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync("127.0.0.1", port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(500));
                if (completed == connectTask && tcp.Connected)
                {
                    ok = true;
                }
            }
            catch { }

            if (ok)
            {
                string info = pid.HasValue ? $"🟢 Activo (PID {pid}, :{port})" : $"🟢 Activo en 127.0.0.1:{port}";
                UpdateServiceState(ServiceId.Postgrest, ServiceStatusState.Activo, info, pid);
            }
            else
            {
                UpdateServiceState(ServiceId.Postgrest, ServiceStatusState.Detenido, $"🔴 Detenido (Puerto {port} libre)");
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

            // Chequeo TCP HttpPort y HttpsPort
            bool portOk = false;
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync("127.0.0.1", Config.Caddy.HttpPort);
                var completed = await Task.WhenAny(connectTask, Task.Delay(400));
                if (completed == connectTask && tcp.Connected) portOk = true;
            }
            catch { }

            if (!portOk)
            {
                try
                {
                    using var tcp2 = new TcpClient();
                    var connectTask2 = tcp2.ConnectAsync("127.0.0.1", Config.Caddy.HttpsPort);
                    var completed2 = await Task.WhenAny(connectTask2, Task.Delay(400));
                    if (completed2 == connectTask2 && tcp2.Connected) portOk = true;
                }
                catch { }
            }

            if (isProcessRunning || portOk)
            {
                string info = pid.HasValue ? $"🟢 Activo (PID {pid}, SSL {Config.Caddy.HttpsPort})" : $"🟢 Activo en puertos {Config.Caddy.HttpPort}/{Config.Caddy.HttpsPort}";
                UpdateServiceState(ServiceId.Caddy, ServiceStatusState.Activo, info, pid);
                return true;
            }
            else
            {
                UpdateServiceState(ServiceId.Caddy, ServiceStatusState.Detenido, $"🔴 Detenido (Puertos {Config.Caddy.HttpPort}/{Config.Caddy.HttpsPort} libres)");
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
                try
                {
                    var syncProcs = Process.GetProcessesByName("sincronizador");
                    if (syncProcs.Length > 0 && !syncProcs[0].HasExited)
                    {
                        pid = syncProcs[0].Id;
                        isRunning = true;
                    }
                }
                catch { }
            }

            // Verificar actividad reciente en log centralizado
            string logPath = Config.ProgressLogPath;
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
            string ps1 = Path.Combine(Config.ScriptsDir, "Migracion", "MigrarYActualizarAccess.ps1");
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

                // Inyectar variables de entorno para scripts dependientes
                psi.EnvironmentVariables["PGHOST"] = Config.PostgreSQL.Host;
                psi.EnvironmentVariables["PGPORT"] = Config.PostgreSQL.Port.ToString();
                psi.EnvironmentVariables["PGDATABASE"] = Config.PostgreSQL.Database;
                psi.EnvironmentVariables["PGUSER"] = Config.PostgreSQL.User;
                psi.EnvironmentVariables["PGCLIENTENCODING"] = Config.PostgreSQL.ClientEncoding;
                psi.EnvironmentVariables["PSFORCE_LOGS_DIR"] = Config.LogsDir;
                string pwd = DaemonConfig.GetPostgresPassword();
                if (!string.IsNullOrEmpty(pwd))
                {
                    psi.EnvironmentVariables["PGPASSWORD"] = pwd;
                    psi.EnvironmentVariables["PGREPREAPIPWD"] = pwd;
                }

                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                proc.Start();

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
            OnProcessOutput?.Invoke(ServiceId.Postgres, "--- INICIANDO TODOS LOS SERVICIOS DE PSFORCE ---", false);

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
                OnProcessOutput?.Invoke(ServiceId.Postgres, $"Intentando levantar servicio PostgreSQL ({Config.PostgreSQL.ServiceName})...", false);
                try
                {
                    var psi = new ProcessStartInfo("net", $"start {Config.PostgreSQL.ServiceName}")
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
                    WorkingDirectory = Path.GetDirectoryName(conf) ?? ProjectRoot,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                // Inyectar puerto y configuración segura en variables de entorno del proceso
                psi.EnvironmentVariables["PGRST_SERVER_PORT"] = Config.PostgREST.Port.ToString();
                string pwd = DaemonConfig.GetPostgresPassword();
                if (!string.IsNullOrEmpty(pwd))
                {
                    string host = string.IsNullOrWhiteSpace(Config.PostgreSQL.Host) ? "localhost" : Config.PostgreSQL.Host;
                    psi.EnvironmentVariables["PGRST_DB_URI"] = $"postgres://authenticator:{pwd}@{host}:{Config.PostgreSQL.Port}/{Config.PostgreSQL.Database}";
                }

                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

                proc.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OnProcessOutput?.Invoke(ServiceId.Postgrest, $"[PostgREST] {e.Data}", false);
                };

                proc.ErrorDataReceived += (s, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;

                    // PostgREST escribe todo su log operativo a stderr por diseño estándar de Haskell.
                    // Solo clasificar como error si el contenido indica un fallo real.
                    bool isActualError = e.Data.Contains("error:", StringComparison.OrdinalIgnoreCase) ||
                                         e.Data.Contains("fatal:", StringComparison.OrdinalIgnoreCase) ||
                                         e.Data.Contains("failed to", StringComparison.OrdinalIgnoreCase) ||
                                         e.Data.Contains("panic:", StringComparison.OrdinalIgnoreCase) ||
                                         e.Data.Contains("could not connect", StringComparison.OrdinalIgnoreCase) ||
                                         e.Data.Contains("connection refused", StringComparison.OrdinalIgnoreCase);

                    string prefix = isActualError ? "[PostgREST ERR]" : "[PostgREST]";
                    OnProcessOutput?.Invoke(ServiceId.Postgrest, $"{prefix} {e.Data}", isActualError);
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
                UpdateServiceState(ServiceId.Postgrest, ServiceStatusState.Activo, $"🟢 Activo (PID {proc.Id}, :{Config.PostgREST.Port})", proc.Id);
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
                    WorkingDirectory = Path.GetDirectoryName(caddyfile) ?? ProjectRoot,
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
                UpdateServiceState(ServiceId.Caddy, ServiceStatusState.Activo, $"🟢 Activo (PID {proc.Id}, SSL {Config.Caddy.HttpsPort})", proc.Id);
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
                    OnProcessOutput?.Invoke(ServiceId.SyncPedidos, $"[ERROR] No se encuentra script ni ejecutable de sincronización: {script}", true);
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

            // Inyectar variables de proceso seguras
            psi.EnvironmentVariables["PGHOST"] = Config.PostgreSQL.Host;
            psi.EnvironmentVariables["PGPORT"] = Config.PostgreSQL.Port.ToString();
            psi.EnvironmentVariables["PGDATABASE"] = Config.PostgreSQL.Database;
            psi.EnvironmentVariables["PGUSER"] = Config.PostgreSQL.User;
            psi.EnvironmentVariables["PGCLIENTENCODING"] = Config.PostgreSQL.ClientEncoding;
            psi.EnvironmentVariables["PSFORCE_LOGS_DIR"] = Config.LogsDir;
            string pwd = DaemonConfig.GetPostgresPassword();
            if (!string.IsNullOrEmpty(pwd))
            {
                psi.EnvironmentVariables["PGPASSWORD"] = pwd;
                psi.EnvironmentVariables["PGREPREAPIPWD"] = pwd;
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

            try
            {
                foreach (var p in Process.GetProcessesByName("sincronizador"))
                {
                    try { p.Kill(true); } catch { }
                }
            }
            catch { }

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
