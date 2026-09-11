using PCMonitor.Core.Models;

namespace PCMonitor.Core.Interfaces;

/// <summary>
/// 通知推送提供者契约（Phase 1 本地日志，Phase 2 接入 ntfy/微信）
/// </summary>
public interface INotificationProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 是否已启用
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 异步发送通知
    /// </summary>
    Task<bool> SendAsync(NotificationEvent notification, CancellationToken cancellationToken = default);
}
