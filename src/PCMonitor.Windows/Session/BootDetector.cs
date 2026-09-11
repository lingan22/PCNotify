using PCMonitor.Core.Common;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Windows.Session;

/// <summary>
/// 系统登录/启动探测器（负责登录自启时的首登感知，抛弃死板的 Uptime 猜测，确保开机登录 100% 触发）
/// </summary>
public class BootDetector : IEventDetector
{
    private readonly IBootStateManager _stateManager;
    private readonly SimpleLogger _logger;
    private readonly bool _triggerOnStart;
    private bool _disposed;

    public event Action<NotificationEvent>? EventDetected;

    public BootDetector(
        IBootStateManager stateManager,
        SimpleLogger logger,
        bool triggerOnStart = true)
    {
        _stateManager = stateManager;
        _logger = logger;
        _triggerOnStart = triggerOnStart;
    }

    /// <summary>
    /// 生成本次启动/登录的感知事件
    /// </summary>
    public NotificationEvent CreateLogonEvent()
    {
        var now = DateTime.UtcNow;
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

        return new NotificationEvent
        {
            Type = EventType.PC_BOOT,
            Title = "电脑已登录进入桌面",
            Body = $"电脑已完成启动/登录并进入桌面（设备：{Environment.MachineName}，系统已运行：{uptime.TotalMinutes:F0}分钟）",
            TimestampUtc = now,
            DeviceName = Environment.MachineName,
            Metadata =
            {
                ["TriggerTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["UptimeMinutes"] = uptime.TotalMinutes.ToString("F0"),
                ["Source"] = "LogonTrigger"
            }
        };
    }

    public void Start()
    {
        if (!_triggerOnStart) return;

        _logger.Info("[BootDetector] 响应登录自启探测，正在准备派发首登进入桌面通知...");
        var evt = CreateLogonEvent();

        try
        {
            EventDetected?.Invoke(evt);
        }
        catch (Exception ex)
        {
            _logger.Error("[BootDetector] 派发登录事件异常", ex);
        }
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
