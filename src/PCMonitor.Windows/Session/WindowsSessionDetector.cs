using Microsoft.Win32;
using PCMonitor.Core.Common;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Windows.Session;

/// <summary>
/// 基于 Windows 会话事件（SystemEvents.SessionSwitch）的锁屏与解锁状态探测器
/// </summary>
public class WindowsSessionDetector : IEventDetector
{
    private readonly SimpleLogger _logger;
    private bool _isStarted;
    private bool _disposed;
    private bool _isLocked;

    public event Action<NotificationEvent>? EventDetected;

    public bool IsLocked => _isLocked;

    public WindowsSessionDetector(SimpleLogger logger)
    {
        _logger = logger;
    }

    public void Start()
    {
        if (_isStarted) return;

        try
        {
            SystemEvents.SessionSwitch += OnSessionSwitch;
            _isStarted = true;
            _logger.Info("[WindowsSessionDetector] 已成功注册 Windows 会话状态变更监听器 (SystemEvents.SessionSwitch)。");
        }
        catch (Exception ex)
        {
            _logger.Error("[WindowsSessionDetector] 注册 Windows 会话监听器失败", ex);
        }
    }

    public void Stop()
    {
        if (!_isStarted) return;

        try
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _isStarted = false;
            _logger.Info("[WindowsSessionDetector] 已注销 Windows 会话状态变更监听器。");
        }
        catch (Exception ex)
        {
            _logger.Error("[WindowsSessionDetector] 注销 Windows 会话监听器异常", ex);
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        _logger.Info($"[WindowsSessionDetector] 捕获 Windows 会话事件: {e.Reason}");

        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                _isLocked = true;
                _logger.Info("[WindowsSessionDetector] 电脑进入锁屏状态。");
                break;

            case SessionSwitchReason.SessionUnlock:
                _isLocked = false;
                _logger.Info("[WindowsSessionDetector] 电脑从锁屏恢复并解锁进入桌面。");
                EmitEvent(EventType.PC_UNLOCK, "电脑已解锁", $"电脑已从锁屏状态解锁并进入桌面（设备：{Environment.MachineName}）", nameof(SessionSwitchReason.SessionUnlock));
                break;

            case SessionSwitchReason.SessionLogon:
                _isLocked = false;
                _logger.Info("[WindowsSessionDetector] 用户登录进入桌面。");
                EmitEvent(EventType.PC_BOOT, "电脑已登录进入桌面", $"用户已成功登录并进入系统桌面（设备：{Environment.MachineName}）", nameof(SessionSwitchReason.SessionLogon));
                break;

            case SessionSwitchReason.SessionLogoff:
                _logger.Info("[WindowsSessionDetector] 用户注销登录。");
                break;
        }
    }

    private void EmitEvent(EventType type, string title, string body, string reason)
    {
        var evt = new NotificationEvent
        {
            Type = type,
            Title = title,
            Body = body,
            TimestampUtc = DateTime.UtcNow,
            DeviceName = Environment.MachineName,
            Metadata =
            {
                ["Reason"] = reason,
                ["TriggerTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }
        };

        try
        {
            EventDetected?.Invoke(evt);
        }
        catch (Exception ex)
        {
            _logger.Error($"[WindowsSessionDetector] 派发 {type} 事件时发生异常", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
