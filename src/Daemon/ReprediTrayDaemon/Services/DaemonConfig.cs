using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReprediTrayDaemon.Services
{
    public class PsGestConfig
    {
        public string FolderName { get; set; } = "PsGestw";
        public int Empresa { get; set; } = 1;
        public int Ejercicio { get; set; } = 2026;
        public string DatabaseFile { get; set; } = "gestion.mdb";
    }

    public class PostgresConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5433;
        public string Database { get; set; } = "repredisl_api";
        public string User { get; set; } = "postgres";
        public string ClientEncoding { get; set; } = "UTF8";
        public string ServiceName { get; set; } = "postgresql-x64-17";
    }

    public class PostgrestConfig
    {
        public string ServerHost { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 3000;
        public string DbSchemas { get; set; } = "api";
        public string DbAnonRole { get; set; } = "web_anon";
        public string DbUri { get; set; } = "postgres://authenticator@localhost:5433/repredisl_api";
        public string CorsAllowedOrigins { get; set; } = "https://pedidos.repredisl.com";
    }

    public class CaddyConfig
    {
        public int HttpPort { get; set; } = 80;
        public int HttpsPort { get; set; } = 443;
        public string Domain { get; set; } = "api.repredisl.com";
        public string ReverseProxyHost { get; set; } = "127.0.0.1";
        public int ReverseProxyPort { get; set; } = 3000;
    }

    public class UrlsConfig
    {
        public string Api { get; set; } = "https://api.repredisl.com";
        public string Pedidos { get; set; } = "https://pedidos.repredisl.com";
    }

    /// <summary>
    /// Configuración centralizada de PsForce cargada desde el archivo raíz config.json.
    /// No contiene rutas fijas en código; deriva dinámicamente todas las rutas de trabajo.
    /// </summary>
    public class DaemonConfig
    {
        private static DaemonConfig? _instance;

        // Propiedades base cargadas desde config.json
        public string PensiPath { get; set; } = @"C:\Pensi";
        public string ProjectName { get; set; } = "PsForce";
        public PsGestConfig PsGest { get; set; } = new();
        public PostgresConfig PostgreSQL { get; set; } = new();
        public PostgrestConfig PostgREST { get; set; } = new();
        public CaddyConfig Caddy { get; set; } = new();
        public UrlsConfig Urls { get; set; } = new();

        [JsonIgnore]
        public string LoadedFromPath { get; private set; } = string.Empty;

        // ─────────────────────────────────────────────────────────────
        // Rutas y valores derivados calculados en tiempo de ejecución
        // ─────────────────────────────────────────────────────────────

        [JsonIgnore]
        public string ProjectRoot
        {
            get
            {
                // Si existe la ruta canónica PensiPath\ProjectName, usarla
                string canonical = Path.Combine(PensiPath, ProjectName);
                if (Directory.Exists(canonical))
                {
                    return canonical;
                }

                // Si se cargó desde un config.json en el árbol local, usar la carpeta de config.json
                if (!string.IsNullOrWhiteSpace(LoadedFromPath))
                {
                    string? dir = Path.GetDirectoryName(LoadedFromPath);
                    if (!string.IsNullOrWhiteSpace(dir)) return dir;
                }

                return canonical;
            }
        }

        [JsonIgnore]
        public string PsGestRoot => Path.Combine(PensiPath, PsGest.FolderName);

        [JsonIgnore]
        public string PsGestDataFolder => $"E{PsGest.Empresa:D3}{PsGest.Ejercicio}";

        [JsonIgnore]
        public string AccessDbPath => Path.Combine(PsGestRoot, PsGestDataFolder, PsGest.DatabaseFile);

        [JsonIgnore]
        public string ScriptsDir => Path.Combine(ProjectRoot, "Scripts");

        [JsonIgnore]
        public string LogsDir => Path.Combine(ProjectRoot, "Logs");

        [JsonIgnore]
        public string SyncDir => Path.Combine(ProjectRoot, "Sync");

        [JsonIgnore]
        public string CaddyDir => Path.Combine(ProjectRoot, "Caddy");

        [JsonIgnore]
        public string DaemonDir => Path.Combine(ProjectRoot, "Daemon");

        [JsonIgnore]
        public string BackupDir => Path.Combine(ProjectRoot, "Backup");

        [JsonIgnore]
        public string ProgressLogPath => Path.Combine(LogsDir, "sync_progress.log");

        [JsonIgnore]
        public string ErrorLogPath => Path.Combine(LogsDir, "errors.log");

        [JsonIgnore]
        public string PendingOrdersLogPath => Path.Combine(LogsDir, "pedidos_pendientes.log");

        [JsonIgnore]
        public string DiscardedOrdersLogPath => Path.Combine(LogsDir, "pedidos_descartados.log");

        // ─────────────────────────────────────────────────────────────
        // Compatibilidad hacia atrás con propiedades legacy
        // ─────────────────────────────────────────────────────────────

        [JsonIgnore]
        public string AppName { get; set; } = "PsForce Centro de Control";

        [JsonIgnore]
        public string Version { get; set; } = "4.9.4";

        [JsonIgnore]
        public string BaseDir
        {
            get => ProjectRoot;
            set { /* No permitir sobreescritura manual en producción */ }
        }

        [JsonIgnore]
        public string AccessMdbPath
        {
            get => AccessDbPath;
            set { /* Derivado de PsGest */ }
        }

        [JsonIgnore]
        public int PostgresPort
        {
            get => PostgreSQL.Port;
            set => PostgreSQL.Port = value;
        }

        [JsonIgnore]
        public string PostgresHost
        {
            get => PostgreSQL.Host;
            set => PostgreSQL.Host = value;
        }

        [JsonIgnore]
        public string PostgresDatabase
        {
            get => PostgreSQL.Database;
            set => PostgreSQL.Database = value;
        }

        [JsonIgnore]
        public string PostgresUser
        {
            get => PostgreSQL.User;
            set => PostgreSQL.User = value;
        }

        [JsonIgnore]
        public string PostgresServiceName
        {
            get => PostgreSQL.ServiceName;
            set => PostgreSQL.ServiceName = value;
        }

        [JsonIgnore]
        public int PostgrestPort
        {
            get => PostgREST.Port;
            set => PostgREST.Port = value;
        }

        [JsonIgnore]
        public int CaddyPort
        {
            get => Caddy.HttpPort;
            set => Caddy.HttpPort = value;
        }

        [JsonIgnore]
        public int SyncIntervalSeconds { get; set; } = 3;

        [JsonIgnore]
        public bool AutoStartServices { get; set; } = true;

        [JsonIgnore]
        public string SyncExecutable => Path.Combine(SyncDir, "sincronizador.exe");

        [JsonIgnore]
        public string SyncScript => Path.Combine(SyncDir, "SincronizarPedidosEntrantes.ps1");

        [JsonIgnore]
        public string CaddyExecutable => Path.Combine(CaddyDir, "caddy.exe");

        [JsonIgnore]
        public string Caddyfile => Path.Combine(CaddyDir, "Caddyfile");

        [JsonIgnore]
        public string PostgrestExecutable => Path.Combine(DaemonDir, "postgrest.exe");

        [JsonIgnore]
        public string PostgrestConfig => Path.Combine(DaemonDir, "postgrest.conf");

        /// <summary>
        /// Obtiene la contraseña de PostgreSQL exclusivamente desde la variable de entorno PGREPREAPIPWD.
        /// Nunca se persiste ni se expone en logs.
        /// </summary>
        public static string GetPostgresPassword()
        {
            string? pwd = Environment.GetEnvironmentVariable("PGREPREAPIPWD");
            if (!string.IsNullOrEmpty(pwd)) return pwd;

            string? libpqPwd = Environment.GetEnvironmentVariable("PGPASSWORD");
            return libpqPwd ?? string.Empty;
        }

        public string ResolveFullPath(string relativeOrAbsolute)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolute)) return string.Empty;
            if (Path.IsPathRooted(relativeOrAbsolute)) return relativeOrAbsolute;

            return Path.GetFullPath(Path.Combine(ProjectRoot, relativeOrAbsolute));
        }

        public void EnsureDirectories()
        {
            try
            {
                if (!Directory.Exists(LogsDir)) Directory.CreateDirectory(LogsDir);
                if (!Directory.Exists(BackupDir)) Directory.CreateDirectory(BackupDir);
            }
            catch { }
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PensiPath))
                throw new InvalidOperationException("Validación de config.json: 'PensiPath' no puede estar vacío.");

            if (string.IsNullOrWhiteSpace(ProjectName))
                throw new InvalidOperationException("Validación de config.json: 'ProjectName' no puede estar vacío.");

            if (PsGest == null || string.IsNullOrWhiteSpace(PsGest.FolderName) || string.IsNullOrWhiteSpace(PsGest.DatabaseFile))
                throw new InvalidOperationException("Validación de config.json: La sección 'PsGest' es incompleta o inválida.");

            if (PostgreSQL == null || string.IsNullOrWhiteSpace(PostgreSQL.Database) || PostgreSQL.Port <= 0)
                throw new InvalidOperationException("Validación de config.json: La sección 'PostgreSQL' es incompleta o inválida.");

            if (PostgREST == null || PostgREST.Port <= 0)
                throw new InvalidOperationException("Validación de config.json: La sección 'PostgREST' es incompleta o inválida.");

            if (Caddy == null || Caddy.HttpPort <= 0 || Caddy.HttpsPort <= 0)
                throw new InvalidOperationException("Validación de config.json: La sección 'Caddy' es incompleta o inválida.");
        }

        public static string FindConfigFile()
        {
            // 1. Variable de entorno explícita
            string? envPath = Environment.GetEnvironmentVariable("PSFORCE_CONFIG");
            if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            {
                return Path.GetFullPath(envPath);
            }

            // 2. Directorio actual de ejecución
            string cwdPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
            if (File.Exists(cwdPath))
            {
                return Path.GetFullPath(cwdPath);
            }

            // 3. Directorio del ejecutable o subiendo por la jerarquía
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(appDir);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "config.json");
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
                dir = dir.Parent;
            }

            // 4. Ubicación de instalación estándar prevista en cliente
            string defaultClientPath = @"C:\Pensi\PsForce\config.json";
            if (File.Exists(defaultClientPath))
            {
                return Path.GetFullPath(defaultClientPath);
            }

            throw new FileNotFoundException(
                "No se pudo encontrar el archivo de configuración central 'config.json'. " +
                "Verifique que exista en la raíz del proyecto o en C:\\Pensi\\PsForce\\config.json.");
        }

        public static DaemonConfig Load()
        {
            if (_instance != null) return _instance;

            string configPath = FindConfigFile();
            string json = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var cfg = JsonSerializer.Deserialize<DaemonConfig>(json, options)
                      ?? throw new InvalidOperationException($"El archivo de configuración en {configPath} está vacío o es inválido.");

            cfg.LoadedFromPath = Path.GetFullPath(configPath);
            cfg.Validate();
            cfg.EnsureDirectories();

            _instance = cfg;
            return _instance;
        }

        public static void Reload()
        {
            _instance = null;
            Load();
        }
    }
}