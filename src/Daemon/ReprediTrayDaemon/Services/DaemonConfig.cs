using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReprediTrayDaemon.Services
{
    /// <summary>
    /// Configuración del Centro de Control ReprediSL V4 para despliegue en cliente.
    /// Soporta carga desde config.json en el directorio del ejecutable o rutas estándar C:\pensi\psforce.
    /// </summary>
    public class DaemonConfig
    {
        public string AppName { get; set; } = "ReprediSL Centro de Control";
        public string Version { get; set; } = "4.9.2";
        public string BaseDir { get; set; } = @"C:\pensi\psforce";
        public string AccessMdbPath { get; set; } = @"C:\pensi\psgestw\e0012026\gestion.mdb";

        public string SyncExecutable { get; set; } = @"..\Sync\sincronizador.exe";
        public string SyncScript { get; set; } = @"..\Sync\SincronizarPedidosEntrantes.ps1";
        public int SyncIntervalSeconds { get; set; } = 3;

        public string CaddyExecutable { get; set; } = @"..\Caddy\caddy.exe";
        public string Caddyfile { get; set; } = @"..\Caddy\Caddyfile";

        public string PostgrestExecutable { get; set; } = "postgrest.exe";
        public string PostgrestConfig { get; set; } = "postgrest.conf";

        public string LogsDir { get; set; } = @"..\Logs";
        public string BackupDir { get; set; } = @"..\Backup";

        public string PostgresHost { get; set; } = "127.0.0.1";
        public int PostgresPort { get; set; } = 5432;
        public string PostgresDatabase { get; set; } = "repredisl_api";
        public string PostgresUser { get; set; } = "postgres";

        public int PostgrestPort { get; set; } = 3000;
        public int CaddyPort { get; set; } = 80;
        public bool AutoStartServices { get; set; } = true;

        [JsonIgnore]
        public string LoadedFromPath { get; private set; } = string.Empty;

        public string ResolveFullPath(string relativeOrAbsolute)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolute)) return string.Empty;
            if (Path.IsPathRooted(relativeOrAbsolute)) return relativeOrAbsolute;

            string baseDirectory = !string.IsNullOrWhiteSpace(LoadedFromPath)
                ? Path.GetDirectoryName(LoadedFromPath)!
                : AppDomain.CurrentDomain.BaseDirectory;

            return Path.GetFullPath(Path.Combine(baseDirectory, relativeOrAbsolute));
        }

        public static DaemonConfig Load()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidatePaths = new string[]
            {
                Path.Combine(appDir, "config.json"),
                Path.Combine(appDir, "..", "config.json"),
                @"C:\pensi\psforce\ReprediTrayDaemon\config.json",
                @"C:\pensi\psforce\config.json",
                @"C:\Pensisoft\ReprediSL\ReprediTrayDaemon\config.json",
                @"C:\Pensisoft\ReprediSL\config.json"
            };

            foreach (var path in candidatePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path);
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var cfg = JsonSerializer.Deserialize<DaemonConfig>(json, options);
                        if (cfg != null)
                        {
                            cfg.LoadedFromPath = Path.GetFullPath(path);
                            return cfg;
                        }
                    }
                }
                catch { }
            }

            var defaultCfg = new DaemonConfig();
            return defaultCfg;
        }

        public void Save(string? targetPath = null)
        {
            try
            {
                string path = targetPath ?? (!string.IsNullOrWhiteSpace(LoadedFromPath) 
                    ? LoadedFromPath 
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"));
                string dir = Path.GetDirectoryName(path)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(path, json, new System.Text.UTF8Encoding(true));
                LoadedFromPath = Path.GetFullPath(path);
            }
            catch { }
        }
    }
}