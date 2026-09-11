using PCMonitor.Core.Common;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Notifications.Providers;

/// <summary>
/// 第一阶段本地日志通知提供者（验证核心事件流转与载荷，不进行外部网络调用）
/// </summary>
public class LogNotificationProvider : INotificationProvider
{
    private readonly SimpleLogger _logger;

    public string Name => "LogNotificationProvider";
    public bool IsEnabled => true;

    public LogNotificationProvider(SimpleLogger logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        var metaStr = string.Join(", ", notification.Metadata.Select(kv => $"{kv.Key}={kv.Value}"));
        var logMessage = $"[NOTIFICATION_DISPATCH] 渠道: {Name} | 事件: {notification.Type} | 标题: '{notification.Title}' | 详情: '{notification.Body}' | 设备: {notification.DeviceName} | 时间: {notification.LocalTimestampFormatted} | 元数据: [{metaStr}]";

        _logger.Info(logMessage);
        return Task.FromResult(true);
    }
}
