using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ReprediTrayDaemon.Services
{
    public class DbSyncService
    {
        public string ProjectRoot => Config.ProjectRoot;
        public string MdbPath { get; private set; } = string.Empty;
        public string ProgressLogPath => Config.ProgressLogPath;
        public string ErrorLogPath => Config.ErrorLogPath;

        private long lastReadOffset = 0;

        public int ErrorCount { get; private set; } = 0;
        public bool SilenceAlerts { get; set; } = false;
        private bool burstAlertTriggered = false;
        private Process? currentSyncProcess = null;

        public event Action<string, LogLevel>? OnLogMessage;
        public event Action<int>? OnErrorThresholdExceeded;

        public enum LogLevel
        {
            Info,
            Warning,
            Error,
            Success
        }

        public DaemonConfig Config { get; }

        public DbSyncService(string projectRoot) : this(DaemonConfig.Load()) { }

        public DbSyncService(DaemonConfig config)
        {
            Config = config ?? DaemonConfig.Load();
            Config.EnsureDirectories();

            MdbPath = Config.AccessDbPath;
            ResolveMdbPath();
            InitLogOffsets();
        }

        public void ResetErrorCounter()
        {
            ErrorCount = 0;
            burstAlertTriggered = false;
            SilenceAlerts = false;
        }

        private void InitLogOffsets()
        {
            try
            {
                if (File.Exists(ProgressLogPath))
                {
                    var info = new FileInfo(ProgressLogPath);
                    lastReadOffset = Math.Max(0, info.Length - 5000);
                }
            }
            catch { }
        }

        public string ResolveMdbPath()
        {
            MdbPath = Config.AccessDbPath;
            return MdbPath;
        }

        public void CheckLogFilesForNewLines()
        {
            ReadNewLines(ProgressLogPath, ref lastReadOffset);
        }

        private string lastEmittedLine = string.Empty;

        private void EmitLogLine(string line, LogLevel level)
        {
            if (string.Equals(lastEmittedLine, line, StringComparison.Ordinal)) return;
            lastEmittedLine = line;
            OnLogMessage?.Invoke(line, level);
        }

        private void ReadNewLines(string filePath, ref long lastOffset)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < lastOffset)
                {
                    lastOffset = 0;
                }

                if (fs.Length > lastOffset)
                {
                    fs.Seek(lastOffset, SeekOrigin.Begin);
                    using var sr = new StreamReader(fs, Encoding.UTF8);
                    string? line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        LogLevel level = LogLevel.Info;
                        bool isDebugStep = line.Contains("DEBUG STEP", StringComparison.OrdinalIgnoreCase);
                        if (isDebugStep)
                        {
                            level = LogLevel.Info;
                        }
                        else if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("Fallo", StringComparison.OrdinalIgnoreCase))
                        {
                            level = LogLevel.Error;
                            ErrorCount++;
                            try { File.AppendAllText(ErrorLogPath, line + Environment.NewLine, Encoding.UTF8); } catch { }
                        }
                        else if (line.Contains("WARNING", StringComparison.OrdinalIgnoreCase) || line.Contains("REINTENTO", StringComparison.OrdinalIgnoreCase))
                        {
                            level = LogLevel.Warning;
                            try { File.AppendAllText(ErrorLogPath, line + Environment.NewLine, Encoding.UTF8); } catch { }
                        }
                        else if (line.Contains("[OK]", StringComparison.OrdinalIgnoreCase) || line.Contains("éxito", StringComparison.OrdinalIgnoreCase) || line.Contains("exito", StringComparison.OrdinalIgnoreCase))
                        {
                            level = LogLevel.Success;
                            ErrorCount = 0;
                            burstAlertTriggered = false;
                            SilenceAlerts = false;
                        }

                        EmitLogLine(line, level);

                        if (ErrorCount > 4 && !burstAlertTriggered && !SilenceAlerts)
                        {
                            burstAlertTriggered = true;
                            SilenceAlerts = true;
                            OnErrorThresholdExceeded?.Invoke(ErrorCount);
                        }
                    }

                    lastOffset = fs.Position;
                }
            }
            catch { }
        }

        public void AppendLog(string message, LogLevel level = LogLevel.Info)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string formattedMsg = $"[{timestamp}] {message}";

            try
            {
                string dir = Path.GetDirectoryName(ProgressLogPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                File.AppendAllText(ProgressLogPath, formattedMsg + Environment.NewLine, Encoding.UTF8);

                if (level == LogLevel.Error || level == LogLevel.Warning)
                {
                    File.AppendAllText(ErrorLogPath, formattedMsg + Environment.NewLine, Encoding.UTF8);
                    if (level == LogLevel.Error)
                    {
                        ErrorCount++;
                        if (ErrorCount > 4 && !burstAlertTriggered && !SilenceAlerts)
                        {
                            burstAlertTriggered = true;
                            SilenceAlerts = true;
                            OnErrorThresholdExceeded?.Invoke(ErrorCount);
                        }
                    }
                }
                else if (level == LogLevel.Success)
                {
                    ErrorCount = 0;
                    burstAlertTriggered = false;
                    SilenceAlerts = false;
                }
            }
            catch { }

            EmitLogLine(formattedMsg, level);
        }

        public void StopCurrentSync()
        {
            try
            {
                if (currentSyncProcess != null && !currentSyncProcess.HasExited)
                {
                    currentSyncProcess.Kill(true);
                    AppendLog("[CANCELADO] Proceso de sincronizacion cancelado por el usuario debido a exceso de errores.", LogLevel.Warning);
                }
            }
            catch { }
        }

        public async Task<bool> RunExportAsync()
        {
            ResetErrorCounter();
            AppendLog("Iniciando exportacion masiva a PostgreSQL...", LogLevel.Info);

            string scriptPath = Path.Combine(Config.ScriptsDir, "BaseDatos", "EjecutarExportacionAccess.ps1");
            if (!File.Exists(scriptPath))
            {
                AppendLog($"[ERROR] Script de exportacion no encontrado: {scriptPath}", LogLevel.Error);
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = ProjectRoot
            };

            // Inyectar variables seguras al proceso sin persistir
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

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                using (currentSyncProcess = new Process { StartInfo = psi })
                {
                    currentSyncProcess.Start();
                    string stdout = await currentSyncProcess.StandardOutput.ReadToEndAsync();
                    string stderr = await currentSyncProcess.StandardError.ReadToEndAsync();
                    await currentSyncProcess.WaitForExitAsync();

                    sw.Stop();
                    double elapsed = Math.Round(sw.Elapsed.TotalSeconds, 2);

                    CheckLogFilesForNewLines();

                    int exitCode = currentSyncProcess.ExitCode;
                    currentSyncProcess = null;

                    if (exitCode == 0)
                    {
                        AppendLog($"Sincronizacion con PostgreSQL completada con exito ({elapsed}s) [OK].", LogLevel.Success);
                        return true;
                    }
                    else
                    {
                        AppendLog($"[ERROR] Fallo en exportacion (Code {exitCode}): {stderr} {stdout}", LogLevel.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                currentSyncProcess = null;
                AppendLog($"[ERROR] Excepcion ejecutando exportacion: {ex.Message}", LogLevel.Error);
                return false;
            }
        }
    }
}
