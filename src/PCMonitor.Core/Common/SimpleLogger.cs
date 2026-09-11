using System.Text.RegularExpressions;

namespace PCMonitor.Core.Common;

/// <summary>
/// 轻量安全日志记录器（支持控制台与按日轮转文件输出，自动对敏感信息脱敏）
/// </summary>
public class SimpleLogger
{
    private static readonly object FileLock = new();
    private readonly string _logDirectory;
    private readonly bool _consoleOutput;

    // 常见敏感关键字模式，用于安全脱敏
    private static readonly Regex SensitivePatterns = new(
        @"(?i)(token|secret|password|key|authorization|bearer)\s*[:=]\s*([^\s,;&]+)",
        RegexOptions.Compiled);

    public string LogDirectory => _logDirectory;

    public SimpleLogger(string? customLogDirectory = null, bool consoleOutput = true)
    {
        _consoleOutput = consoleOutput;
        if (!string.IsNullOrWhiteSpace(customLogDirectory))
        {
            _logDirectory = customLogDirectory;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logDirectory = Path.Combine(appData, "PCNotify", "logs");
        }
    }

    public void Info(string message) => Log("INFO", message);
    public void Warn(string message) => Log("WARN", message);
    public void Error(string message, Exception? ex = null)
    {
        var msg = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}" : message;
        Log("ERROR", msg);
    }
    public void Debug(string message) => Log("DEBUG", message);

    private void Log(string level, string message)
    {
        var sanitizedMessage = Sanitize(message);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] [{level}] {sanitizedMessage}";

        if (_consoleOutput)
        {
            try
            {
                if (level == "ERROR")
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(logLine);
                    Console.ResetColor();
                }
                else if (level == "WARN")
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(logLine);
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine(logLine);
                }
            }
            catch
            {
                // 控制台不可用时静默忽略
            }
        }

        WriteToFile(logLine);
    }

    private void WriteToFile(string logLine)
    {
        lock (FileLock)
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                var fileName = $"pcmonitor_{DateTime.Now:yyyyMMdd}.log";
                var filePath = Path.Combine(_logDirectory, fileName);
                File.AppendAllText(filePath, logLine + Environment.NewLine);
            }
            catch
            {
                // 日志写入失败时不抛出异常，防止主守护进程崩溃
            }
        }
    }

    /// <summary>
    /// 对潜在敏感信息（Token、密钥等）进行脱敏掩码
    /// </summary>
    public static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        return SensitivePatterns.Replace(input, match =>
        {
            var key = match.Groups[1].Value;
            var val = match.Groups[2].Value;
            var masked = val.Length > 4
                ? val[..2] + "****" + val[^2..]
                : "****";
            return $"{key}={masked}";
        });
    }
}
