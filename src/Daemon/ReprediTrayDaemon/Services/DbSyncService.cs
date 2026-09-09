using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ReprediTrayDaemon.Services
{
    public class DbSyncService
    {
        public string ProjectRoot { get; }
        public string MdbPath { get; private set; } = string.Empty;
        public string ProgressLogPath { get; }
        public string AltProgressLogPath { get; }
        public string ErrorLogPath { get; }

        private long lastReadOffset = 0;
        private long lastAltReadOffset = 0;

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

        public DbSyncService(string projectRoot)
        {
            ProjectRoot = ResolveFallbackProjectRoot(projectRoot);
            ProgressLogPath = Path.Combine(ProjectRoot, "src", "Access", "sync_progress.log");
            AltProgressLogPath = Path.Combine(ProjectRoot, "src", "Access", "E0012026", "sync_progress.log");
            ErrorLogPath = Path.Combine(ProjectRoot, "src", "Access", "sync_errors.log");

            ResolveMdbPath();
            InitLogOffsets();
        }

        private static string ResolveFallbackProjectRoot(string candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                File.Exists(Path.Combine(candidate, "Scripts", "BaseDatos", "EjecutarExportacionAccess.ps1")))
            {
                return candidate;
            }

            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Scripts", "BaseDatos", "EjecutarExportacionAccess.ps1")))
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

            return candidate;
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
                if (File.Exists(AltProgressLogPath))
                {
                    var info = new FileInfo(AltProgressLogPath);
                    lastAltReadOffset = Math.Max(0, info.Length - 5000);
                }
            }
            catch { }
        }

        public string ResolveMdbPath()
        {
            string[] candidates = new string[]
            {
                Path.Combine(ProjectRoot, "src", "Access", "E0012026", "gestion.mdb"),
                Path.Combine(ProjectRoot, "src", "Access", "gestion.mdb"),
                @"C:\PsGest\E0012026\gestion.mdb",
                Path.Combine(ProjectRoot, "src", "Access", "BdDestino.mdb")
            };

            foreach (var cand in candidates)
            {
                if (File.Exists(cand))
                {
                    MdbPath = cand;
                    return MdbPath;
                }
            }

            MdbPath = candidates[0];
            return MdbPath;
        }

        public void CheckLogFilesForNewLines()
        {
            ReadNewLines(ProgressLogPath, ref lastReadOffset);
            ReadNewLines(AltProgressLogPath, ref lastAltReadOffset);
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
                        }

                        EmitLogLine(line, level);

                        if (ErrorCount > 3 && !burstAlertTriggered && !SilenceAlerts)
                        {
                            burstAlertTriggered = true;
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
                        if (ErrorCount > 3 && !burstAlertTriggered && !SilenceAlerts)
                        {
                            burstAlertTriggered = true;
                            OnErrorThresholdExceeded?.Invoke(ErrorCount);
                        }
                    }
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

            string scriptPath = Path.Combine(ProjectRoot, "Scripts", "BaseDatos", "EjecutarExportacionAccess.ps1");
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
