namespace PCMonitor.Core.Models;

/// <summary>
/// 核心事件类型枚举
/// </summary>
public enum EventType
{
    /// <summary>
    /// 电脑开机并进入桌面
    /// </summary>
    PC_BOOT,

    /// <summary>
    /// 电脑从锁屏恢复并进入桌面
    /// </summary>
    PC_UNLOCK
}
