namespace PCMonitor.Core.State;

/// <summary>
/// 持久化状态数据模型
/// </summary>
public class BootState
{
    /// <summary>
    /// 最近一次已成功通知的开机标识符（由开机时间戳与主机名生成）
    /// </summary>
    public string? LastNotifiedBootId { get; set; }

    /// <summary>
    /// 最近一次系统开机时间（UTC）
    /// </summary>
    public DateTime? LastBootTimeUtc { get; set; }

    /// <summary>
    /// 最近一次通知发送时间（UTC）
    /// </summary>
    public DateTime? LastNotificationTimeUtc { get; set; }

    /// <summary>
    /// 最近一次触发的事件类型
    /// </summary>
    public string? LastEventType { get; set; }
}
