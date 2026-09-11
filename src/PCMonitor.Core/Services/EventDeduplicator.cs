using PCMonitor.Core.Common;
using PCMonitor.Core.Interfaces;
using PCMonitor.Core.Models;

namespace PCMonitor.Core.Services;

/// <summary>
/// 事件去重与防抖服务（保障高灵敏度响应，同时消除底层瞬间重复广播与崩溃连环拉起）
/// </summary>
public class EventDeduplicator
{
    private readonly IBootStateManager _stateManager;
    private readonly SimpleLogger _logger;
    private readonly TimeSpan _unlockDebounceWindow;
    private readonly TimeSpan _bootDebounceWindow;
    private readonly object _lock = new();

    private DateTime _lastUnlockEventTime = DateTime.MinValue;
    private DateTime _lastBootEventTime = DateTime.MinValue;

    public EventDeduplicator(
        IBootStateManager stateManager,
        SimpleLogger logger,
        TimeSpan? unlockDebounceWindow = null,
        TimeSpan? bootDebounceWindow = null)
    {
        _stateManager = stateManager;
        _logger = logger;
        _unlockDebounceWindow = unlockDebounceWindow ?? TimeSpan.FromSeconds(3);
        _bootDebounceWindow = bootDebounceWindow ?? TimeSpan.FromSeconds(30);

        // 初始化加载持久化状态中的历史记录
        var state = _stateManager.Load();
        if (state.LastNotificationTimeUtc.HasValue)
        {
            if (state.LastEventType == nameof(EventType.PC_UNLOCK))
            {
                _lastUnlockEventTime = state.LastNotificationTimeUtc.Value;
            }
            else if (state.LastEventType == nameof(EventType.PC_BOOT))
            {
                _lastBootEventTime = state.LastNotificationTimeUtc.Value;
            }
        }
    }

    /// <summary>
    /// 判断事件是否应当放行并触发通知
    /// </summary>
    public bool ShouldEmit(NotificationEvent evt)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            switch (evt.Type)
            {
                case EventType.PC_UNLOCK:
                    return ShouldEmitUnlock(now);

                case EventType.PC_BOOT:
                    return ShouldEmitBoot(evt, now);

                default:
                    return true;
            }
        }
    }

    private bool ShouldEmitUnlock(DateTime now)
    {
        var elapsed = now - _lastUnlockEventTime;
        if (elapsed < _unlockDebounceWindow)
        {
            _logger.Info($"[Deduplicator] 忽略抖动解锁事件: 距离上次解锁仅 {elapsed.TotalMilliseconds:F0}ms (< {_unlockDebounceWindow.TotalSeconds}s)。");
            return false;
        }

        _lastUnlockEventTime = now;
        var state = _stateManager.Load();
        state.LastNotificationTimeUtc = now;
        state.LastEventType = nameof(EventType.PC_UNLOCK);
        _stateManager.Save(state);

        _logger.Info($"[Deduplicator] 放行解锁事件: 时间戳 {now:yyyy-MM-dd HH:mm:ss} UTC。");
        return true;
    }

    private bool ShouldEmitBoot(NotificationEvent evt, DateTime now)
    {
        var elapsed = now - _lastBootEventTime;
        if (elapsed < _bootDebounceWindow)
        {
            _logger.Info($"[Deduplicator] 忽略短时间重复登录事件: 距离上次登录仅 {elapsed.TotalSeconds:F0}s (< {_bootDebounceWindow.TotalSeconds}s)。");
            return false;
        }

        _lastBootEventTime = now;
        var state = _stateManager.Load();
        state.LastNotificationTimeUtc = now;
        state.LastEventType = nameof(EventType.PC_BOOT);
        _stateManager.Save(state);

        _logger.Info($"[Deduplicator] 放行登录/启动事件: 时间戳 {now:yyyy-MM-dd HH:mm:ss} UTC。");
        return true;
    }
}
