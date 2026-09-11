using System.Text.Json;
using PCMonitor.Core.Interfaces;

namespace PCMonitor.Core.State;

/// <summary>
/// 基于 JSON 文件的开机状态持久化实现
/// </summary>
public class JsonBootStateManager : IBootStateManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly object _lock = new();

    public string StateFilePath => _filePath;

    public JsonBootStateManager(string? customFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _filePath = customFilePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "PCNotify");
            _filePath = Path.Combine(dir, "state.json");
        }
    }

    public BootState Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return new BootState();
                }

                var json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new BootState();
                }

                return JsonSerializer.Deserialize<BootState>(json, JsonOptions) ?? new BootState();
            }
            catch
            {
                // 状态文件若有损坏，安全降级为初始状态并备份
                return new BootState();
            }
        }
    }

    public void Save(BootState state)
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(state, JsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                // 记录或静默容错，避免状态保存失败导致主程序崩溃
                Console.Error.WriteLine($"[JsonBootStateManager] Failed to save state to {_filePath}: {ex.Message}");
            }
        }
    }
}
