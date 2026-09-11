using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace ReprediSync
{
    internal class SyncConfig
    {
        public string AccessMdbPath { get; set; } = @"C:\pensi\psgestw\e0012026\gestion.mdb";
        public string LogsDir { get; set; } = @"..\Logs";
        public string ScriptFile { get; set; } = "SincronizarPedidosEntrantes.ps1";
        public int IntervalSeconds { get; set; } = 3;
        public string PostgresHost { get; set; } = "127.0.0.1";
        public int PostgresPort { get; set; } = 5432;
        public string PostgresDatabase { get; set; } = "repredisl_api";
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
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

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
                    configPath = args[i + 1];
                    i++;
                }
            }

            // Load config if exists
            var cfg = new SyncConfig();
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var loaded = JsonSerializer.Deserialize<SyncConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (loaded != null) cfg = loaded;
                }
                catch { }
            }
            else
            {
                // Create default config for convenience
                try
                {
                    var opt = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(configPath, JsonSerializer.Serialize(cfg, opt), new UTF8Encoding(true));
                }
                catch { }
            }

            if (interval == 3 && cfg.IntervalSeconds > 0)
            {
                interval = cfg.IntervalSeconds;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logsDir = Path.IsPathRooted(cfg.LogsDir) ? cfg.LogsDir : Path.GetFullPath(Path.Combine(baseDir, cfg.LogsDir));
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
            Console.WriteLine($"• Base de Datos Access: {cfg.AccessMdbPath}");
            Console.WriteLine($"• Directorio de Logs  : {logsDir}");
            Console.WriteLine($"• Modo de Ejecución   : {(loop ? $"Bucle continuo (cada {interval}s)" : "Ejecución única (--once)")}");
            Console.WriteLine("--------------------------------------------------------------------------------");

            // Locate script
            string[] scriptCandidates = new string[]
            {
                Path.Combine(baseDir, cfg.ScriptFile),
                Path.Combine(baseDir, "SincronizarPedidosEntrantes.ps1"),
                Path.Combine(baseDir, "..", "Scripts", "BaseDatos", "SincronizarPedidosEntrantes.ps1"),
                Path.Combine(baseDir, "..", "..", "Scripts", "BaseDatos", "SincronizarPedidosEntrantes.ps1"),
                @"D:\programacio\repredi\ReprediSL_V4\Scripts\BaseDatos\SincronizarPedidosEntrantes.ps1"
            };

            string scriptPath = string.Empty;
            foreach (var cand in scriptCandidates)
            {
                if (File.Exists(cand))
                {
                    scriptPath = Path.GetFullPath(cand);
                    break;
                }
            }

            if (string.IsNullOrEmpty(scriptPath))
            {
                string msg = $"[ERROR] No se ha encontrado el script SincronizarPedidosEntrantes.ps1 en ninguna ruta.";
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

            string psArguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
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