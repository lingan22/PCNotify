using System.Diagnostics;
using System.Runtime.InteropServices;
using PCMonitor.Common;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Models;
using PCMonitor.Core.Services;
using PCMonitor.Core.State;
using PCMonitor.Forms;
using PCMonitor.Notifications;
using PCMonitor.Notifications.Providers;
using PCMonitor.Windows.Session;
using PCMonitor.Windows.SystemInfo;
using PCMonitor.Windows.TaskScheduler;

namespace PCMonitor;

/// <summary>
/// 后台守护与系统托盘管理上下文
/// </summary>
public class PCMonitorAppContext : ApplicationContext
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint RegisterWindowMessage(string lpString);

    private class WakeupNativeWindow : NativeWindow
    {
        private readonly uint _showMsg;
        private readonly Action _onWakeup;

        public WakeupNativeWindow(uint showMsg, Action onWakeup)
        {
            _showMsg = showMsg;
            _onWakeup = onWakeup;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)_showMsg && _showMsg != 0)
            {
                _onWakeup();
                return;
            }
            base.WndProc(ref m);
        }
    }

    private WakeupNativeWindow? _wakeupWindow;
    private const string ShowWindowEventName = @"Global\PCNotify_ShowWindow_Event";

    private readonly SimpleLogger _logger;
    private readonly ConfigManager _configManager;
    private readonly PCNotifyConfig _config;
    private readonly JsonBootStateManager _stateManager;
    private readonly EventDeduplicator _deduplicator;
    private readonly NotificationManager _notificationManager;
    private readonly SystemInfoCollector _infoCollector;
    private readonly WindowsSessionDetector _sessionDetector;
    private readonly BootDetector _bootDetector;
    private readonly TaskSchedulerHelper _taskScheduler;
    private readonly Control _uiInvoker = new Control();
    private readonly object _formLock = new();

    // 系统托盘与控制面板窗口
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly ToolStripMenuItem _menuItemMonitorStatus;
    private readonly ToolStripMenuItem _menuItemAutoStart;
    private ModernControlForm? _modernControlForm;

    public record ActivityEntry(DateTime Time, EventType Type, string Title, string Channels, bool Success, string Summary);
    public static readonly List<ActivityEntry> RecentActivities = new();

    private bool _isMonitoringActive = true;
    private EventWaitHandle? _showWindowEvent;
    private Thread? _eventListenThread;
    private bool _disposed;

    public PCMonitorAppContext(
        SimpleLogger? logger = null,
        ConfigManager? configManager = null,
        JsonBootStateManager? stateManager = null,
        bool triggerLogonOnStart = true,
        bool startWithWindow = false)
    {
        _logger = logger ?? new SimpleLogger();
        _configManager = configManager ?? new ConfigManager(_logger, securityProvider: new PCMonitor.Windows.Security.DpapiSecurityProvider(_logger));
        _config = _configManager.LoadOrCreate();

        _stateManager = stateManager ?? new JsonBootStateManager();
        _deduplicator = new EventDeduplicator(_stateManager, _logger);
        _taskScheduler = new TaskSchedulerHelper(_logger);

        // 绑定 UI 主线程 Win32 原生句柄，保障后续跨线程调度 100% 可达
        _ = _uiInvoker.Handle;

        var showMsg = RegisterWindowMessage(Program.ShowWindowMessageName);
        if (showMsg != 0)
        {
            _wakeupWindow = new WakeupNativeWindow(showMsg, () => ShowControlForm());
        }

        // 初始化通知中心与各 Provider
        _notificationManager = new NotificationManager(_logger, _config.Retry);
        _notificationManager.RegisterProvider(new LogNotificationProvider(_logger));

        if (_config.Ntfy.Enabled)
        {
            _notificationManager.RegisterProvider(new NtfyProvider(_config.Ntfy, _logger));
        }

        if (_config.WeCom.Enabled)
        {
            _notificationManager.RegisterProvider(new WeComWebhookProvider(_config.WeCom, _logger));
        }

        if (_config.Feishu.Enabled)
        {
            _notificationManager.RegisterProvider(new FeishuWebhookProvider(_config.Feishu, _logger));
        }

        if (_config.DingTalk.Enabled)
        {
            _notificationManager.RegisterProvider(new DingTalkWebhookProvider(_config.DingTalk, _logger));
        }

        if (_config.WxPusher.Enabled)
        {
            _notificationManager.RegisterProvider(new WxPusherProvider(_config.WxPusher, _logger));
        }

        if (_config.CustomWebhook.Enabled)
        {
            _notificationManager.RegisterProvider(new CustomWebhookProvider(_config.CustomWebhook, _logger));
        }

        if (_config.WeChat.Enabled)
        {
            _notificationManager.RegisterProvider(new WeChatProvider(_config.WeChat, _logger));
        }

        // 初始化硬件采集与会话探测器
        _infoCollector = new SystemInfoCollector(_logger, _config.SystemInfo);
        _sessionDetector = new WindowsSessionDetector(_logger);
        _bootDetector = new BootDetector(_stateManager, _logger, triggerLogonOnStart);

        // 订阅探测器事件
        _sessionDetector.EventDetected += OnEventDetected;
        _bootDetector.EventDetected += OnEventDetected;

        // 初始化托盘菜单
        _trayMenu = new ContextMenuStrip();
        var itemOpen = new ToolStripMenuItem("打开控制面板", null, (s, e) => ShowControlForm())
        {
            Font = new Font(_trayMenu.Font, FontStyle.Bold)
        };

        _menuItemMonitorStatus = new ToolStripMenuItem("监控状态: 🟢 运行中", null, (s, e) => ToggleMonitoring());
        _menuItemAutoStart = new ToolStripMenuItem("开机自动启动", null, (s, e) => ToggleAutoStart());
        var itemTestPush = new ToolStripMenuItem("发送测试推送", null, async (s, e) => await DoQuickTestPushAsync());
        var itemLogs = new ToolStripMenuItem("查看运行日志", null, (s, e) => OpenLogsFolder());
        var itemExit = new ToolStripMenuItem("退出 PCNotify", null, (s, e) => ExitApplication());

        _trayMenu.Items.AddRange(new ToolStripItem[]
        {
            itemOpen,
            new ToolStripSeparator(),
            _menuItemMonitorStatus,
            _menuItemAutoStart,
            itemTestPush,
            itemLogs,
            new ToolStripSeparator(),
            itemExit
        });

        // 初始化托盘图标
        _trayIcon = new NotifyIcon
        {
            Icon = CreateAppIcon(),
            Text = "PCNotify 电脑监控与手机推送",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        // 遵循 Windows 规范：仅响应 DoubleClick 打开主面板，移除 MouseClick 杜绝连击多发
        _trayIcon.DoubleClick += (s, e) => ShowControlForm();

        UpdateTrayMenuStatus();

        _logger.Info("==================================================");
        _logger.Info("[PCMonitor] 守护程序已启动，系统托盘与消息泵已就绪。");
        _logger.Info($"[PCMonitor] 配置文件: {_configManager.ConfigFilePath}");
        _logger.Info($"[PCMonitor] ntfy 渠道: {(_config.Ntfy.Enabled ? $"已启用 (Topic: {_config.Ntfy.Topic[..Math.Min(8, _config.Ntfy.Topic.Length)]}***)" : "已禁用")}");
        _logger.Info($"[PCMonitor] 微信渠道: {(_config.WeChat.Enabled ? "已启用" : "未启用")}");
        _logger.Info("==================================================");

        InitializeDetectors();
        StartNamedEventListener();

        if (startWithWindow)
        {
            ShowControlForm();
        }
    }

    private void InitializeDetectors()
    {
        try
        {
            _sessionDetector.Start();
            _bootDetector.Start();
        }
        catch (Exception ex)
        {
            _logger.Error("[PCMonitor] 初始化探测器异常", ex);
        }
    }

    private void OnEventDetected(NotificationEvent evt)
    {
        if (!_isMonitoringActive)
        {
            _logger.Info($"[PCMonitor] 当前监控处于暂停状态，已忽略事件 [{evt.Type}]: '{evt.Title}'。");
            return;
        }

        var liveConfig = _configManager.LoadOrCreate();
        if (evt.Type == EventType.PC_UNLOCK && !liveConfig.Triggers.MonitorUnlock)
        {
            _logger.Info("[PCMonitor] 用户已在设置中禁用解锁事件推送，已忽略。");
            return;
        }
        if (evt.Type == EventType.PC_BOOT && !liveConfig.Triggers.MonitorBoot)
        {
            _logger.Info("[PCMonitor] 用户已在设置中禁用开机登录事件推送，已忽略。");
            return;
        }

        _logger.Info($"[PCMonitor] 接收到原始事件 [{evt.Type}]: '{evt.Title}'");

        if (!_deduplicator.ShouldEmit(evt))
        {
            _logger.Info($"[PCMonitor] 事件 [{evt.Type}] 被防抖去重拦截，跳过推送。");
            return;
        }

        // 异步采集硬件状态并分发
        Task.Run(async () =>
        {
            try
            {
                var snapshot = await _infoCollector.CaptureAsync();
                var statusSummary = snapshot.ToPlainText();

                if (!string.IsNullOrWhiteSpace(statusSummary))
                {
                    evt.Body = $"{evt.Body}\n\n【系统状态】{statusSummary}";
                    evt.Metadata["SystemStatus"] = statusSummary;
                }

                var results = await _notificationManager.DispatchAsync(evt);
                var successCount = results.Count(r => r.Value);
                var channelSummary = string.Join(", ", results.Where(r => r.Value).Select(r => r.Key));
                if (string.IsNullOrWhiteSpace(channelSummary)) channelSummary = "无通道";

                lock (RecentActivities)
                {
                    RecentActivities.Insert(0, new ActivityEntry(DateTime.Now, evt.Type, evt.Title, channelSummary, successCount > 0, $"成功送达 {successCount} 个通道"));
                    if (RecentActivities.Count > 15) RecentActivities.RemoveAt(RecentActivities.Count - 1);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[PCMonitor] 派发通知事件 [{evt.Type}] 发生异常", ex);
            }
        });
    }

    public void ShowControlForm()
    {
        if (_uiInvoker.InvokeRequired)
        {
            _uiInvoker.BeginInvoke(ShowControlForm);
            return;
        }

        lock (_formLock)
        {
            if (_modernControlForm == null || _modernControlForm.IsDisposed)
            {
                _modernControlForm = new ModernControlForm(
                    _configManager,
                    _logger,
                    _taskScheduler,
                    _infoCollector,
                    _notificationManager,
                    () => _isMonitoringActive,
                    SetMonitoringActive);
            }

            _modernControlForm.ShowAndActivate();
        }
    }

    public void SetMonitoringActive(bool active)
    {
        _isMonitoringActive = active;
        UpdateTrayMenuStatus();
        _modernControlForm?.UpdateMonitorStatusUi();
        _logger.Info($"[PCMonitor] 监控状态已切换为: {(_isMonitoringActive ? "运行中" : "已暂停")}");
    }

    private void ToggleMonitoring()
    {
        SetMonitoringActive(!_isMonitoringActive);
    }

    private void ToggleAutoStart()
    {
        var isRegistered = _taskScheduler.IsTaskRegistered(out _);
        var targetExe = Environment.ProcessPath ?? Application.ExecutablePath;

        if (isRegistered)
        {
            _taskScheduler.UnregisterTask(out _);
        }
        else
        {
            _taskScheduler.RegisterTask(targetExe, out _);
        }

        UpdateTrayMenuStatus();
        _modernControlForm?.UpdateAutoStartStatusUi();
    }

    private void UpdateTrayMenuStatus()
    {
        _menuItemMonitorStatus.Text = _isMonitoringActive ? "监控状态: 🟢 运行中" : "监控状态: ⏸️ 已暂停";
        var isRegistered = _taskScheduler.IsTaskRegistered(out _);
        _menuItemAutoStart.Checked = isRegistered;
    }

    private async Task DoQuickTestPushAsync()
    {
        _trayIcon.ShowBalloonTip(2000, "PCNotify", "正在发送测试推送...", ToolTipIcon.Info);
        try
        {
            var snapshot = await _infoCollector.CaptureAsync();
            var testEvt = new NotificationEvent
            {
                Type = EventType.PC_UNLOCK,
                Title = "PCNotify 托盘快速测试推送",
                Body = $"【测试推送】由托盘菜单快速发起。\n\n【系统状态】{snapshot.ToPlainText()}",
                TimestampUtc = DateTime.UtcNow,
                DeviceName = Environment.MachineName
            };

            var results = await _notificationManager.DispatchAsync(testEvt);
            var successCount = results.Count(r => r.Value);
            var channelSummary = string.Join(", ", results.Where(r => r.Value).Select(r => r.Key));
            if (string.IsNullOrWhiteSpace(channelSummary)) channelSummary = "无通道";

            lock (RecentActivities)
            {
                RecentActivities.Insert(0, new ActivityEntry(DateTime.Now, testEvt.Type, testEvt.Title, channelSummary, successCount > 0, $"测试推送 ({successCount} 渠道成功)"));
                if (RecentActivities.Count > 15) RecentActivities.RemoveAt(RecentActivities.Count - 1);
            }

            _trayIcon.ShowBalloonTip(3000, "PCNotify", "测试推送已发出！请检查手机。", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(3000, "PCNotify", $"测试推送失败: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private void OpenLogsFolder()
    {
        if (Directory.Exists(_logger.LogDirectory))
        {
            Process.Start(new ProcessStartInfo { FileName = _logger.LogDirectory, UseShellExecute = true });
        }
    }

    private void StartNamedEventListener()
    {
        try
        {
            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
            _eventListenThread = new Thread(() =>
            {
                while (!_disposed)
                {
                    if (_showWindowEvent.WaitOne())
                    {
                        if (_disposed) break;
                        // 收到外部唤醒信号，通过 _uiInvoker 调度至 UI 主线程弹出控制面板
                        if (_uiInvoker.InvokeRequired)
                        {
                            _uiInvoker.BeginInvoke(ShowControlForm);
                        }
                        else
                        {
                            ShowControlForm();
                        }
                    }
                }
            })
            {
                IsBackground = true,
                Name = "PCNotify_ShowWindow_Listener"
            };
            _eventListenThread.Start();
        }
        catch (Exception ex)
        {
            _logger.Warn($"[PCMonitor] 注册窗口激活命名事件失败: {ex.Message}");
        }
    }

    private static Icon CreateAppIcon()
    {
        return AppIconHelper.GetAppIcon();
    }

    public void ExitApplication()
    {
        _logger.Info("[PCMonitor] 用户通过菜单选择完全退出程序。");
        _modernControlForm?.ForceExit();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            _sessionDetector.EventDetected -= OnEventDetected;
            _bootDetector.EventDetected -= OnEventDetected;
            _sessionDetector.Dispose();
            _bootDetector.Dispose();

            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayMenu.Dispose();
            _modernControlForm?.Dispose();
            _showWindowEvent?.Dispose();
            _wakeupWindow?.DestroyHandle();
            _uiInvoker.Dispose();
        }

        base.Dispose(disposing);
    }
}
