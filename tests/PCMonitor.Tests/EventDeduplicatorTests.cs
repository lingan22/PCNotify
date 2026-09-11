using PCMonitor.Core.Common;
using PCMonitor.Core.Models;
using PCMonitor.Core.Services;
using PCMonitor.Core.State;
using Xunit;

namespace PCMonitor.Tests;

public class EventDeduplicatorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonBootStateManager _stateManager;
    private readonly SimpleLogger _logger;

    public EventDeduplicatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pcnotify_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var statePath = Path.Combine(_tempDir, "state.json");
        _stateManager = new JsonBootStateManager(statePath);
        _logger = new SimpleLogger(_tempDir, consoleOutput: false);
    }

    [Fact]
    public void ShouldEmit_BootEvent_ReturnsTrue_AndSavesState()
    {
        var deduplicator = new EventDeduplicator(_stateManager, _logger);
        var bootEvt = new NotificationEvent
        {
            Type = EventType.PC_BOOT,
            Title = "电脑已登录进入桌面",
            DeviceName = "TEST-PC"
        };

        var result = deduplicator.ShouldEmit(bootEvt);

        Assert.True(result);
        var savedState = _stateManager.Load();
        Assert.Equal(nameof(EventType.PC_BOOT), savedState.LastEventType);
        Assert.NotNull(savedState.LastNotificationTimeUtc);
    }

    [Fact]
    public void ShouldEmit_ConsecutiveBootEventsWithinWindow_ReturnsFalse()
    {
        var deduplicator = new EventDeduplicator(
            _stateManager,
            _logger,
            bootDebounceWindow: TimeSpan.FromMilliseconds(500));

        var bootEvt = new NotificationEvent
        {
            Type = EventType.PC_BOOT,
            Title = "电脑已登录进入桌面",
            DeviceName = "TEST-PC"
        };

        var first = deduplicator.ShouldEmit(bootEvt);
        var second = deduplicator.ShouldEmit(bootEvt);

        Assert.True(first);
        Assert.False(second); // 短时间内被拦截

        Thread.Sleep(600);
        var third = deduplicator.ShouldEmit(bootEvt);
        Assert.True(third); // 超过防抖窗口放行
    }

    [Fact]
    public void ShouldEmit_UnlockEvents_DebouncesWithinWindow()
    {
        // 500ms 防抖窗口
        var deduplicator = new EventDeduplicator(
            _stateManager,
            _logger,
            unlockDebounceWindow: TimeSpan.FromMilliseconds(500));

        var unlockEvt = new NotificationEvent
        {
            Type = EventType.PC_UNLOCK,
            Title = "电脑已解锁"
        };

        var first = deduplicator.ShouldEmit(unlockEvt);
        var second = deduplicator.ShouldEmit(unlockEvt);

        Assert.True(first);
        Assert.False(second); // 立即发生的第二次被防抖拦截

        // 等待超过防抖窗口
        Thread.Sleep(600);
        var third = deduplicator.ShouldEmit(unlockEvt);
        Assert.True(third); // 窗口外放行
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }
}
