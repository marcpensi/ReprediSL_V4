using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace ReprediSync
{
    internal class PsGestConfig
    {
        public string FolderName { get; set; } = "PsGestw";
        public int Empresa { get; set; } = 1;
        public int Ejercicio { get; set; } = 2026;
        public string DatabaseFile { get; set; } = "gestion.mdb";
    }

    internal class PostgreSQLConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5433;
        public string Database { get; set; } = "repredisl_api";
        public string User { get; set; } = "postgres";
        public string ClientEncoding { get; set; } = "UTF8";
        public string ServiceName { get; set; } = "postgresql-x64-17";
    }

    internal class SyncConfig
    {
        public string PensiPath { get; set; } = @"C:\Pensi";
        public string ProjectName { get; set; } = "PsForce";
        public PsGestConfig PsGest { get; set; } = new();
        public PostgreSQLConfig PostgreSQL { get; set; } = new();
        public int IntervalSeconds { get; set; } = 3;

        [JsonIgnore]
        public string ProjectRoot { get; set; } = string.Empty;

        [JsonIgnore]
        public string AccessDbPath => Path.Combine(
            PensiPath,
            PsGest?.FolderName ?? "PsGestw",
            $"e{(PsGest?.Empresa ?? 1):D3}{(PsGest?.Ejercicio ?? 2026)}",
            PsGest?.DatabaseFile ?? "gestion.mdb"
        );

        [JsonIgnore]
        public string LogsDir => Path.Combine(ProjectRoot, "Logs");
    }

    internal class Program
    {
        private static Process? activeChild = null;

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "ReprediSL - Sincronizador de Pedidos";

            bool loop = true;
            int interval = 3;
            string? explicitConfigPath = null;

            // Parse args
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("-Loop", StringComparison.OrdinalIgnoreCase) || arg.Equals("--loop", StringComparison.OrdinalIgnoreCase))
                {
                    loop = true;
                }
                else if (arg.Equals("--once", StringComparison.OrdinalIgnoreCase) || arg.Equals("-Once", StringComparison.OrdinalIgnoreCase))
                {
                    loop = false;
                }
                else if ((arg.Equals("-IntervaloSegundos", StringComparison.OrdinalIgnoreCase) || arg.Equals("--interval", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int sec) && sec > 0)
                    {
                        interval = sec;
                        i++;
                    }
                }
                else if (arg.Equals("--config", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    explicitConfigPath = args[i + 1];
                    i++;
                }
            }

            string resolvedConfigPath = ResolveConfigPath(explicitConfigPath);
            SyncConfig cfg = LoadConfig(resolvedConfigPath);

            if (interval == 3 && cfg.IntervalSeconds > 0)
            {
                interval = cfg.IntervalSeconds;
            }

            string logsDir = cfg.LogsDir;
            if (!Directory.Exists(logsDir))
            {
                try { Directory.CreateDirectory(logsDir); } catch { }
            }

            string progressLog = Path.Combine(logsDir, "sync_progress.log");
            string errorLog = Path.Combine(logsDir, "sync_errors.log");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine("  REPREDISL V4 - SINCRONIZADOR DE PEDIDOS (PostgreSQL -> Access gestion.mdb)    ");
            Console.WriteLine("================================================================================");
            Console.ResetColor();
            Console.WriteLine($"• Configuración       : {resolvedConfigPath}");
            Console.WriteLine($"• Raíz del Proyecto   : {cfg.ProjectRoot}");
            Console.WriteLine($"• Base de Datos Access: {cfg.AccessDbPath}");
            Console.WriteLine($"• PostgreSQL          : {cfg.PostgreSQL.Host}:{cfg.PostgreSQL.Port} ({cfg.PostgreSQL.Database})");
            Console.WriteLine($"• Directorio de Logs  : {logsDir}");
            Console.WriteLine($"• Modo de Ejecución   : {(loop ? $"Bucle continuo (cada {interval}s)" : "Ejecución única (--once)")}");
            Console.WriteLine("--------------------------------------------------------------------------------");

            // Locate script dynamically relative to project root or current directory
            string scriptPath = ResolveScriptPath(cfg.ProjectRoot);

            if (string.IsNullOrEmpty(scriptPath))
            {
                string msg = "[ERROR] No se ha encontrado el script SincronizarPedidosEntrantes.ps1 en ninguna ruta.";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                Console.ResetColor();
                AppendLog(errorLog, msg);
                return 1;
            }

            Console.WriteLine($"• Script de Procesado : {scriptPath}");
            Console.WriteLine();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[AVISO] Deteniendo sincronizador de pedidos...");
                Console.ResetColor();
                try
                {
                    if (activeChild != null && !activeChild.HasExited)
                    {
                        activeChild.Kill(true);
                    }
                }
                catch { }
                Environment.Exit(0);
            };

            string psArguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -ConfigFilePath \"{resolvedConfigPath}\"";
            if (loop)
            {
                psArguments += $" -Loop -IntervaloSegundos {interval}";
            }

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = psArguments,
                WorkingDirectory = Path.GetDirectoryName(scriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Inject connection environment variables
            psi.EnvironmentVariables["PGHOST"] = cfg.PostgreSQL.Host;
            psi.EnvironmentVariables["PGPORT"] = cfg.PostgreSQL.Port.ToString();
            psi.EnvironmentVariables["PGDATABASE"] = cfg.PostgreSQL.Database;
            psi.EnvironmentVariables["PGUSER"] = cfg.PostgreSQL.User;
            psi.EnvironmentVariables["PGCLIENTENCODING"] = cfg.PostgreSQL.ClientEncoding;
            psi.EnvironmentVariables["PSFORCE_PROJECT_ROOT"] = cfg.ProjectRoot;
            psi.EnvironmentVariables["PSFORCE_LOGS_DIR"] = logsDir;
            psi.EnvironmentVariables["PSFORCE_CONFIG"] = resolvedConfigPath;

            string? secretPwd = Environment.GetEnvironmentVariable("PGREPREAPIPWD");
            if (!string.IsNullOrEmpty(secretPwd))
            {
                psi.EnvironmentVariables["PGPASSWORD"] = secretPwd;
            }

            try
            {
                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                activeChild = proc;

                proc.OutputDataReceived += (s, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;

                    if (e.Data.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(e.Data);
                        Console.ResetColor();
                        AppendLog(errorLog, e.Data);
                    }
                    else if (e.Data.Contains("[NUEVO PEDIDO]", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(e.Data);
                        Console.ResetColor();
                    }
                    else if (e.Data.Contains("[INFO]", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(e.Data);
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine(e.Data);
                    }

                    AppendLog(progressLog, e.Data);
                };

                proc.ErrorDataReceived += (s, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERR] {e.Data}");
                    Console.ResetColor();
                    AppendLog(errorLog, $"[ERR] {e.Data}");
                    AppendLog(progressLog, $"[ERR] {e.Data}");
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                proc.WaitForExit();
                return proc.ExitCode;
            }
            catch (Exception ex)
            {
                string err = $"[ERROR CRITICO] Fallo al iniciar el proceso PowerShell: {ex.Message}";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(err);
                Console.ResetColor();
                AppendLog(errorLog, err);
                return 2;
            }
        }

        private static string ResolveConfigPath(string? explicitPath)
        {
            if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
            {
                return Path.GetFullPath(explicitPath);
            }

            string? envPath = Environment.GetEnvironmentVariable("PSFORCE_CONFIG");
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            {
                return Path.GetFullPath(envPath);
            }

            // Search upwards from BaseDirectory
            string? currentDir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(currentDir))
            {
                string candidate = Path.Combine(currentDir, "config.json");
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }

                var parent = Directory.GetParent(currentDir);
                currentDir = parent?.FullName;
            }

            // Standard fallback
            string defaultPath = Path.Combine(@"C:\Pensi", "PsForce", "config.json");
            return defaultPath;
        }

        private static SyncConfig LoadConfig(string configPath)
        {
            var cfg = new SyncConfig();
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var loaded = JsonSerializer.Deserialize<SyncConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (loaded != null) cfg = loaded;
                }
                catch { }

                cfg.ProjectRoot = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? string.Empty;
            }
            else
            {
                cfg.ProjectRoot = Path.Combine(cfg.PensiPath, cfg.ProjectName);
            }

            return cfg;
        }

        private static string ResolveScriptPath(string projectRoot)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new string[]
            {
                Path.Combine(projectRoot, "Scripts", "BaseDatos", "SincronizarPedidosEntrantes.ps1"),
                Path.Combine(projectRoot, "Sync", "SincronizarPedidosEntrantes.ps1"),
                Path.Combine(baseDir, "SincronizarPedidosEntrantes.ps1"),
                Path.Combine(baseDir, "..", "Scripts", "BaseDatos", "SincronizarPedidosEntrantes.ps1"),
                Path.Combine(baseDir, "..", "..", "Scripts", "BaseDatos", "SincronizarPedidosEntrantes.ps1")
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand))
                {
                    return Path.GetFullPath(cand);
                }
            }

            return string.Empty;
        }

        private static void AppendLog(string filePath, string line)
        {
            try
            {
                var utf8Bom = new UTF8Encoding(true);
                File.AppendAllText(filePath, line + Environment.NewLine, utf8Bom);
            }
            catch { }
        }
    }
}