using PCMonitor.Core.State;

namespace PCMonitor.Core.Interfaces;

/// <summary>
/// 系统开机与运行状态持久化契约
/// </summary>
public interface IBootStateManager
{
    /// <summary>
    /// 加载当前存储的状态
    /// </summary>
    BootState Load();

    /// <summary>
    /// 保存最新状态
    /// </summary>
    void Save(BootState state);

    /// <summary>
    /// 状态文件存储路径
    /// </summary>
    string StateFilePath { get; }
}
