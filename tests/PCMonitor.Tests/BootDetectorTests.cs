using PCMonitor.Core.Common;
using PCMonitor.Core.Models;
using PCMonitor.Core.State;
using PCMonitor.Windows.Session;
using Xunit;

namespace PCMonitor.Tests;

public class BootDetectorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonBootStateManager _stateManager;
    private readonly SimpleLogger _logger;

    public BootDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pcnotify_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var statePath = Path.Combine(_tempDir, "state.json");
        _stateManager = new JsonBootStateManager(statePath);
        _logger = new SimpleLogger(_tempDir, consoleOutput: false);
    }

    [Fact]
    public void CreateLogonEvent_GeneratesValidNotificationEvent()
    {
        var detector = new BootDetector(_stateManager, _logger);
        var evt = detector.CreateLogonEvent();

        Assert.NotNull(evt);
        Assert.Equal(EventType.PC_BOOT, evt.Type);
        Assert.Contains("电脑已登录进入桌面", evt.Title);
        Assert.True(evt.Metadata.ContainsKey("TriggerTime"));
        Assert.True(evt.Metadata.ContainsKey("UptimeMinutes"));
    }

    [Fact]
    public void Start_WhenTriggerOnStartIsTrue_EmitsEvent()
    {
        var detector = new BootDetector(_stateManager, _logger, triggerOnStart: true);
        NotificationEvent? emittedEvt = null;
        detector.EventDetected += evt => emittedEvt = evt;

        detector.Start();

        Assert.NotNull(emittedEvt);
        Assert.Equal(EventType.PC_BOOT, emittedEvt.Type);
    }

    [Fact]
    public void Start_WhenTriggerOnStartIsFalse_DoesNotEmit()
    {
        var detector = new BootDetector(_stateManager, _logger, triggerOnStart: false);
        NotificationEvent? emittedEvt = null;
        detector.EventDetected += evt => emittedEvt = evt;

        detector.Start();

        Assert.Null(emittedEvt);
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
