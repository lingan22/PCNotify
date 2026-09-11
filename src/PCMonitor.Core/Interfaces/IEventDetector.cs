using PCMonitor.Core.Models;

namespace PCMonitor.Core.Interfaces;

/// <summary>
/// 事件探测器契约
/// </summary>
public interface IEventDetector : IDisposable
{
    /// <summary>
    /// 当探测到有效业务事件时触发
    /// </summary>
    event Action<NotificationEvent>? EventDetected;

    /// <summary>
    /// 启动探测
    /// </summary>
    void Start();

    /// <summary>
    /// 停止探测
    /// </summary>
    void Stop();
}
