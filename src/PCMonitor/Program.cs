using System.Runtime.InteropServices;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Models;
using PCMonitor.Core.State;
using PCMonitor.Notifications;
using PCMonitor.Notifications.Providers;
using PCMonitor.Windows.SystemInfo;
using PCMonitor.Windows.TaskScheduler;

namespace PCMonitor;

internal static class Program
{
    private const string MutexName = @"Global\PCNotify_PCMonitor_SingleInstanceMutex";
    private const string ShowWindowEventName = @"Global\PCNotify_ShowWindow_Event";
    private static Mutex? _singleInstanceMutex;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);
    private const uint ATTACH_PARENT_PROCESS = 0x0ffffffff;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public const string ShowWindowMessageName = "PCNotify_ShowWindow_Message";
    private static readonly IntPtr HWND_BROADCAST = (IntPtr)0xffff;
    private const int SW_RESTORE = 9;

    [STAThread]
    static void Main(string[] args)
    {
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "";

        // 非 GUI 命令尝试附加控制台输出
        if (command != "" && command != "--gui" && command != "--run")
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            try
            {
                var stdOutStream = Console.OpenStandardOutput();
                if (stdOutStream != Stream.Null)
                {
                    var writer = new StreamWriter(stdOutStream, Console.OutputEncoding ?? System.Text.Encoding.UTF8) { AutoFlush = true };
                    Console.SetOut(writer);
                    Console.SetError(writer);
                }
            }
            catch { }
        }

        switch (command)
        {
            case "--register-task":
                HandleRegisterTask();
                return;

            case "--unregister-task":
                HandleUnregisterTask();
                return;

            case "--status":
                HandleStatus();
                return;

            case "--test-unlock":
                HandleTestUnlock();
                return;

            case "--test-boot":
                HandleTestBoot();
                return;

            case "--test-push":
                HandleTestPush();
                return;

            case "--help":
            case "-h":
            case "/?":
                PrintHelp();
                return;

            case "--run":
                // 任务计划程序登录拉起：纯静默托盘模式
                RunApplication(startWithWindow: false);
                return;

            case "--gui":
            case "":
            default:
                // 用户双击运行或显式指定 --gui：弹出控制面板
                RunApplication(startWithWindow: true);
                return;
        }
    }

    private static void RunApplication(bool startWithWindow)
    {
        bool createdNew;
        try
        {
            _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
        }
        catch
        {
            // 在受限权限环境下若 Global 命名空间不可用，退化为 Session 本地互斥
            _singleInstanceMutex = new Mutex(true, @"Local\PCNotify_PCMonitor_SingleInstanceMutex", out createdNew);
        }

        if (!createdNew)
        {
            if (startWithWindow)
            {
                // 1. 若已有窗口句柄可见/最小化，直接 Win32 强行恢复并置顶
                var existingHwnd = FindWindow(null, "PCNotify - 电脑监控与手机推送");
                if (existingHwnd != IntPtr.Zero)
                {
                    ShowWindowAsync(existingHwnd, SW_RESTORE);
                    SetForegroundWindow(existingHwnd);
                }

                // 2. 通过 Win32 原生 Registered Message 广播唤醒后台守护进程在 UI 线程弹出窗口
                var msg = RegisterWindowMessage(ShowWindowMessageName);
                if (msg != 0)
                {
                    PostMessage(HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
                }

                try
                {
                    // 唤醒后台正在运行的实例弹出窗口
                    using var evt = EventWaitHandle.OpenExisting(ShowWindowEventName);
                    evt.Set();
                }
                catch
                {
                    // 若事件未能打开，提示并退出
                }
            }

            var logger = new SimpleLogger();
            logger.Warn("[PCMonitor] 已检测到已有 PCMonitor 实例正在运行，已唤醒已有窗口并拒绝重复启动。");
            return;
        }

        var appLogger = new SimpleLogger();
        try
        {
            ApplicationConfiguration.Initialize();
            using var appContext = new PCMonitorAppContext(appLogger, startWithWindow: startWithWindow);
            Application.Run(appContext);
        }
        catch (Exception ex)
        {
            appLogger.Error("[PCMonitor] 主循环发生严重异常", ex);
        }
        finally
        {
            if (_singleInstanceMutex != null)
            {
                try { _singleInstanceMutex.ReleaseMutex(); } catch { }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
        }
    }

    private static void HandleRegisterTask()
    {
        var logger = new SimpleLogger();
        var helper = new TaskSchedulerHelper(logger);
        var currentExe = Environment.ProcessPath ?? Application.ExecutablePath;

        Console.WriteLine($"[PCNotify] 正在注册 Windows 任务计划程序任务 (目标程序: {currentExe})...");
        var success = helper.RegisterTask(currentExe, out var output);
        if (success)
        {
            Console.WriteLine($"[PCNotify] 任务计划注册成功！");
            Console.WriteLine($"  任务名称: {TaskSchedulerHelper.DefaultTaskName}");
            Console.WriteLine($"  触发条件: 当前用户登录桌面自动拉起");
            Console.WriteLine($"  运行模式: 静默后台守护 + 系统托盘常驻");
        }
        else
        {
            Console.WriteLine($"[PCNotify] 任务计划注册失败: {output}");
        }
    }

    private static void HandleUnregisterTask()
    {
        var logger = new SimpleLogger();
        var helper = new TaskSchedulerHelper(logger);

        Console.WriteLine($"[PCNotify] 正在注销任务计划程序任务...");
        var success = helper.UnregisterTask(out var output);
        if (success)
        {
            Console.WriteLine("[PCNotify] 任务计划已成功卸载。");
        }
        else
        {
            Console.WriteLine($"[PCNotify] 任务计划卸载提示: {output}");
        }
    }

    private static void HandleStatus()
    {
        var logger = new SimpleLogger();
        var configManager = new ConfigManager(logger, securityProvider: new PCMonitor.Windows.Security.DpapiSecurityProvider(logger));
        var config = configManager.LoadOrCreate();
        var helper = new TaskSchedulerHelper(logger);
        var stateManager = new JsonBootStateManager();
        var state = stateManager.Load();

        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var bootTime = DateTime.Now - uptime;

        var collector = new SystemInfoCollector(logger, config.SystemInfo);
        var snapshot = collector.CaptureAsync().GetAwaiter().GetResult();

        Console.WriteLine("==================================================");
        Console.WriteLine(" PCNotify 系统运行与配置状态检查");
        Console.WriteLine("==================================================");
        Console.WriteLine($"计算机名:          {Environment.MachineName}");
        Console.WriteLine($"当前时间:          {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"系统已运行时间:    {uptime.TotalMinutes:F1} 分钟 (推算开机: {bootTime:yyyy-MM-dd HH:mm:ss})");
        Console.WriteLine($"配置文件路径:      {configManager.ConfigFilePath}");
        Console.WriteLine($"状态文件路径:      {stateManager.StateFilePath}");
        Console.WriteLine($"日志目录:          {logger.LogDirectory}");
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine("【推送聚合渠道配置】");
        Console.WriteLine($"  ntfy 渠道:       {(config.Ntfy.Enabled ? $"已启用 (优先级: {config.Ntfy.Priority}, Topic: {config.Ntfy.Topic})" : "已禁用")}");
        Console.WriteLine($"  企业微信机器人:  {(config.WeCom.Enabled ? "已启用" : "未启用")}");
        Console.WriteLine($"  飞书机器人:      {(config.Feishu.Enabled ? "已启用" : "未启用")}");
        Console.WriteLine($"  钉钉机器人:      {(config.DingTalk.Enabled ? "已启用" : "未启用")}");
        Console.WriteLine($"  WxPusher 微信:   {(config.WxPusher.Enabled ? "已启用" : "未启用")}");
        Console.WriteLine($"  自定义 Webhook:  {(config.CustomWebhook.Enabled ? "已启用" : "未启用")}");
        Console.WriteLine($"  微信 (PushPlus): {(config.WeChat.Enabled ? "已启用" : "未启用")}");
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine("【实时硬件与网络状态】");
        if (snapshot.CpuUsagePercentage.HasValue)
        {
            Console.WriteLine($"  CPU 使用率:      {snapshot.CpuUsagePercentage.Value:F1}%");
        }
        Console.WriteLine($"  内存使用:        {snapshot.UsedMemoryGb:F1} GB / {snapshot.TotalMemoryGb:F1} GB ({snapshot.MemoryUsagePercentage:F0}%)");
        if (snapshot.Disks.Count > 0)
        {
            foreach (var d in snapshot.Disks)
            {
                Console.WriteLine($"  磁盘 {d.DriveName}:        可用 {d.AvailableGb:F1} GB / 总计 {d.TotalGb:F1} GB ({d.FreePercentage:F0}% 空闲)");
            }
        }
        Console.WriteLine($"  网络状态:        {snapshot.NetworkStatus}");
        Console.WriteLine("--------------------------------------------------");
        var isTaskRegistered = helper.IsTaskRegistered(out var taskDetails);
        Console.WriteLine($"任务计划注册状态:  {(isTaskRegistered ? "已注册 (Active)" : "未注册")}");
        if (isTaskRegistered && !string.IsNullOrWhiteSpace(taskDetails))
        {
            Console.WriteLine($"任务详情摘要:      {taskDetails}");
        }
        Console.WriteLine("==================================================");
    }

    private static void HandleTestPush()
    {
        var logger = new SimpleLogger();
        var configManager = new ConfigManager(logger, securityProvider: new PCMonitor.Windows.Security.DpapiSecurityProvider(logger));
        var config = configManager.LoadOrCreate();

        Console.WriteLine("==================================================");
        Console.WriteLine("[PCNotify] 正在采集硬件状态并向已启用渠道发起真实推送测试...");
        Console.WriteLine($"目标 ntfy Topic:   {config.Ntfy.Topic}");
        Console.WriteLine($"手机查看地址:      {config.Ntfy.ServerUrl.TrimEnd('/')}/{config.Ntfy.Topic}");
        Console.WriteLine("==================================================");

        var collector = new SystemInfoCollector(logger, config.SystemInfo);
        var snapshot = collector.CaptureAsync().GetAwaiter().GetResult();
        var statusStr = snapshot.ToPlainText();

        var testEvt = new NotificationEvent
        {
            Type = EventType.PC_UNLOCK,
            Title = "PCNotify 实机推送测试",
            Body = $"【测试推送】此消息由 PCNotify 客户端发出，测试多渠道推送连通性。\n\n【系统状态】{statusStr}",
            TimestampUtc = DateTime.UtcNow,
            DeviceName = Environment.MachineName,
            Metadata =
            {
                ["TriggerTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["Test"] = "true"
            }
        };

        var manager = new NotificationManager(logger, config.Retry);
        manager.RegisterProvider(new LogNotificationProvider(logger));

        if (config.Ntfy.Enabled) manager.RegisterProvider(new NtfyProvider(config.Ntfy, logger));
        if (config.WeCom.Enabled) manager.RegisterProvider(new WeComWebhookProvider(config.WeCom, logger));
        if (config.Feishu.Enabled) manager.RegisterProvider(new FeishuWebhookProvider(config.Feishu, logger));
        if (config.DingTalk.Enabled) manager.RegisterProvider(new DingTalkWebhookProvider(config.DingTalk, logger));
        if (config.WxPusher.Enabled) manager.RegisterProvider(new WxPusherProvider(config.WxPusher, logger));
        if (config.CustomWebhook.Enabled) manager.RegisterProvider(new CustomWebhookProvider(config.CustomWebhook, logger));
        if (config.WeChat.Enabled) manager.RegisterProvider(new WeChatProvider(config.WeChat, logger));

        var results = manager.DispatchAsync(testEvt).GetAwaiter().GetResult();

        Console.WriteLine();
        Console.WriteLine("【推送结果摘要】:");
        foreach (var (provider, success) in results)
        {
            var statusTag = success ? "成功 [PASS]" : "未成功/已跳过 [FAIL/SKIP]";
            Console.WriteLine($"  - 渠道 [{provider}]: {statusTag}");
        }
        Console.WriteLine();
        Console.WriteLine($"提示: 如果您在手机端安装了 ntfy App 并订阅了 '{config.Ntfy.Topic}'，或者在浏览器打开上述地址，即可看到本条推送！");
    }

    private static void HandleTestUnlock()
    {
        var logger = new SimpleLogger();
        Console.WriteLine("[PCNotify] 正在模拟触发解锁测试事件...");

        var testEvt = new NotificationEvent
        {
            Type = EventType.PC_UNLOCK,
            Title = "电脑已解锁 (测试模拟)",
            Body = $"模拟测试：电脑已从锁屏恢复并解锁进入桌面（设备: {Environment.MachineName}）",
            TimestampUtc = DateTime.UtcNow,
            DeviceName = Environment.MachineName,
            Metadata =
            {
                ["Simulated"] = "true",
                ["TriggerTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }
        };

        var stateManager = new JsonBootStateManager();
        var deduplicator = new Core.Services.EventDeduplicator(stateManager, logger, TimeSpan.FromSeconds(2));

        if (deduplicator.ShouldEmit(testEvt))
        {
            var provider = new Notifications.Providers.LogNotificationProvider(logger);
            provider.SendAsync(testEvt).GetAwaiter().GetResult();
            Console.WriteLine("[PCNotify] 测试解锁事件已成功派发并写入本地日志！");
        }
        else
        {
            Console.WriteLine("[PCNotify] 测试解锁事件被去重拦截（防抖生效）。");
        }
    }

    private static void HandleTestBoot()
    {
        var logger = new SimpleLogger();
        Console.WriteLine("[PCNotify] 正在模拟触发登录/开机测试事件...");

        var testEvt = new NotificationEvent
        {
            Type = EventType.PC_BOOT,
            Title = "电脑已登录进入桌面 (测试模拟)",
            Body = $"模拟测试：用户已登录进入桌面（设备: {Environment.MachineName}）",
            TimestampUtc = DateTime.UtcNow,
            DeviceName = Environment.MachineName,
            Metadata =
            {
                ["Simulated"] = "true",
                ["TriggerTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }
        };

        var stateManager = new JsonBootStateManager();
        var deduplicator = new Core.Services.EventDeduplicator(stateManager, logger, bootDebounceWindow: TimeSpan.FromSeconds(2));

        if (deduplicator.ShouldEmit(testEvt))
        {
            var provider = new Notifications.Providers.LogNotificationProvider(logger);
            provider.SendAsync(testEvt).GetAwaiter().GetResult();
            Console.WriteLine("[PCNotify] 测试登录/开机事件已成功派发并写入本地日志！");
        }
        else
        {
            Console.WriteLine("[PCNotify] 测试登录/开机事件被去重拦截（防抖生效）。");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("PCNotify (PCMonitor) 电脑状态监控与通知守护程序");
        Console.WriteLine();
        Console.WriteLine("用法:");
        Console.WriteLine("  PCMonitor.exe [command]");
        Console.WriteLine();
        Console.WriteLine("支持的命令:");
        Console.WriteLine("  (无参数)            打开 PCNotify 图形控制面板与托盘");
        Console.WriteLine("  --gui               显式启动并打开图形控制面板");
        Console.WriteLine("  --run               以静默托盘模式在后台运行监控守护进程 (任务计划开机自启专用)");
        Console.WriteLine("  --register-task     自动注册 Windows 任务计划程序自启动任务");
        Console.WriteLine("  --unregister-task   注销 Windows 任务计划程序自启动任务");
        Console.WriteLine("  --status            查询系统运行时间、配置、实时硬件状态与任务计划注册情况");
        Console.WriteLine("  --test-push         立即向 ntfy / 微信渠道发起一次真实网络推送测试");
        Console.WriteLine("  --test-unlock       模拟单次解锁事件测试，验证日志记录与防抖");
        Console.WriteLine("  --test-boot         模拟单次登录/开机事件测试，验证日志记录与防抖");
        Console.WriteLine("  --help, -h          显示帮助信息");
    }
}
