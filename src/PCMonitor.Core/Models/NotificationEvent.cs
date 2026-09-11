namespace PCMonitor.Core.Models;

/// <summary>
/// 统一通知事件领域模型
/// </summary>
public class NotificationEvent
{
    /// <summary>
    /// 事件类型
    /// </summary>
    public EventType Type { get; set; }

    /// <summary>
    /// 通知标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 通知正文内容
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// 事件发生时间（UTC）
    /// </summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 本地发生时间（本地格式化字符串）
    /// </summary>
    public string LocalTimestampFormatted => TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// 电脑设备标识/计算机名
    /// </summary>
    public string DeviceName { get; set; } = Environment.MachineName;

    /// <summary>
    /// 扩展元数据（键值对）
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    public override string ToString()
    {
        return $"[{Type}] {Title} @ {LocalTimestampFormatted} ({DeviceName}) - {Body}";
    }
}
