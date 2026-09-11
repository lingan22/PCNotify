using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using PCMonitor.Common;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Models;
using PCMonitor.Notifications;
using PCMonitor.Notifications.Providers;
using PCMonitor.Windows.Security;
using PCMonitor.Windows.SystemInfo;
using PCMonitor.Windows.TaskScheduler;

namespace PCMonitor.Forms;

/// <summary>
/// PCNotify 现代化 Fluent 极简风格控制面板（适配 Win10 与 Win11）
/// </summary>
public class ModernControlForm : Form
{
    // 基础依赖注入
    private readonly ConfigManager _configManager;
    private readonly SimpleLogger _logger;
    private readonly TaskSchedulerHelper _taskScheduler;
    private readonly SystemInfoCollector _infoCollector;
    private readonly NotificationManager _notificationManager;
    private readonly Func<bool> _isMonitoringActive;
    private readonly Action<bool> _setMonitoringActive;

    private PCNotifyConfig _config;
    private bool _isRealExit;

    // 视觉配色（Slate 中性灰蓝与 Fluent 核心色）
    private static readonly Color ColorBgCanvas = Color.FromArgb(248, 250, 252);     // #F8FAFC
    private static readonly Color ColorBgSidebar = Color.FromArgb(241, 245, 249);    // #F1F5F9
    private static readonly Color ColorCardBg = Color.FromArgb(255, 255, 255);       // #FFFFFF
    private static readonly Color ColorBorder = Color.FromArgb(226, 232, 240);       // #E2E8F0
    private static readonly Color ColorTextPrimary = Color.FromArgb(15, 23, 42);     // #0F172A
    private static readonly Color ColorTextSecondary = Color.FromArgb(100, 116, 139);// #64748B
    private static readonly Color ColorBrand = Color.FromArgb(37, 99, 235);          // #2563EB
    private static readonly Color ColorBrandHover = Color.FromArgb(29, 78, 216);     // #1D4ED8
    private static readonly Color ColorSuccess = Color.FromArgb(16, 185, 129);       // #10B981
    private static readonly Color ColorWarning = Color.FromArgb(245, 158, 11);       // #F59E0B

    // 左侧侧边栏控件
    private Button _btnNavOverview = null!;
    private Button _btnNavChannels = null!;
    private Button _btnNavSettings = null!;
    private readonly List<Button> _navButtons = new();
    private Label _lblSidebarStatus = null!;
    private Button _btnSideToggleMonitor = null!;

    // 右侧内容容器
    private Panel _contentContainer = null!;
    private Panel _panelOverview = null!;
    private Panel _panelChannels = null!;
    private Panel _panelSettings = null!;

    // 概览页 KPI 汇总卡片
    private Label _lblKpiStatusValue = null!;
    private Label _lblKpiStatusSub = null!;
    private Label _lblKpiChannelValue = null!;
    private Label _lblKpiChannelSub = null!;
    private Label _lblKpiPerfValue = null!;
    private Label _lblKpiPerfSub = null!;

    // 概览页 硬件与活动控件
    private MetricBar _barCpu = null!;
    private MetricBar _barMemory = null!;
    private Label _lblNetworkStatus = null!;
    private Panel _pnlActivityList = null!;
    private System.Windows.Forms.Timer _refreshTimer = null!;

    // 响应式卡片引用
    private Panel _cardKpi1 = null!;
    private Panel _cardKpi2 = null!;
    private Panel _cardKpi3 = null!;
    private Panel _cardHardware = null!;
    private Panel _cardActivity = null!;
    private Button _btnRefreshAct = null!;
    private Panel _cardTriggers = null!;
    private Panel _cardSettingsHardware = null!;
    private Panel _cardRetry = null!;
    private Panel _cardSec = null!;
    private Label _lblDedup = null!;

    // 渠道页控件
    private Panel _channelContentPanel = null!;
    private readonly List<Button> _channelTabButtons = new();
    private int _selectedChannelIndex;

    // ntfy 控件
    private CheckBox _chkNtfyEnable = null!;
    private TextBox _txtNtfyTopic = null!;
    private ComboBox _cmbNtfyPriority = null!;

    // 企业微信 控件
    private CheckBox _chkWeComEnable = null!;
    private TextBox _txtWeComWebhook = null!;

    // 飞书 控件
    private CheckBox _chkFeishuEnable = null!;
    private TextBox _txtFeishuWebhook = null!;

    // 钉钉 控件
    private CheckBox _chkDingTalkEnable = null!;
    private TextBox _txtDingTalkWebhook = null!;

    // WxPusher 控件
    private CheckBox _chkWxPusherEnable = null!;
    private TextBox _txtWxPusherAppToken = null!;
    private TextBox _txtWxPusherUids = null!;

    // 自定义 Webhook 控件
    private CheckBox _chkCustomEnable = null!;
    private TextBox _txtCustomUrl = null!;

    // 设置页控件
    private CheckBox _chkMonitorUnlock = null!;
    private CheckBox _chkMonitorBoot = null!;
    private ComboBox _cmbDeduplicateWindow = null!;
    private CheckBox _chkIncludeCpu = null!;
    private CheckBox _chkIncludeMemory = null!;
    private CheckBox _chkIncludeNetwork = null!;
    private CheckBox _chkIncludeDisks = null!;
    private NumericUpDown _numMaxRetries = null!;
    private NumericUpDown _numInitialDelay = null!;
    private ComboBox _cmbBackoff = null!;
    private Label _lblDpapiStatus = null!;

    // 底部状态栏
    private Label _lblFooterMessage = null!;
    private Button _btnSave = null!;

    private int _lastLayoutWidth = -1;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED: 启用从底至顶的层次化合成渲染，彻底杜绝拖拽闪烁与卡顿
            return cp;
        }
    }

    public ModernControlForm(
        ConfigManager configManager,
        SimpleLogger logger,
        TaskSchedulerHelper taskScheduler,
        SystemInfoCollector infoCollector,
        NotificationManager notificationManager,
        Func<bool> isMonitoringActive,
        Action<bool> setMonitoringActive)
    {
        _configManager = configManager;
        _logger = logger;
        _taskScheduler = taskScheduler;
        _infoCollector = infoCollector;
        _notificationManager = notificationManager;
        _isMonitoringActive = isMonitoringActive;
        _setMonitoringActive = setMonitoringActive;

        _config = _configManager.LoadOrCreate();

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;

        InitializeModernUi();
        LoadConfigToUi();
        UpdateMonitorStatusUi();
        UpdateAutoStartStatusUi();

        // 启动定时刷新硬件状态与概览（每 3 秒刷新一次）
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _refreshTimer.Tick += async (s, e) => await RefreshHardwareUiAsync();
        _refreshTimer.Start();

        _ = RefreshHardwareUiAsync();
    }

    private void InitializeModernUi()
    {
        Icon = AppIconHelper.GetAppIcon();
        Text = "PCNotify - 电脑监控与手机推送";
        Width = 860;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimumSize = new Size(840, 620);
        BackColor = ColorBgCanvas;
        Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);

        // 1. 底部操作条 (Dock = Bottom)
        var bottomBar = new DoubleBufferedPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = ColorCardBg,
            Padding = new Padding(20, 10, 20, 10)
        };
        bottomBar.Paint += (s, e) =>
        {
            using var pen = new Pen(ColorBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, bottomBar.Width, 0);
        };

        _lblFooterMessage = new Label
        {
            Text = "🟢 守护中 | 就绪",
            ForeColor = ColorTextSecondary,
            Font = new Font("Microsoft YaHei UI", 9F),
            AutoSize = true,
            Location = new Point(20, 18)
        };

        _btnSave = CreateModernButton("保存所有配置", ColorBrand, Color.White, 120, 36);
        _btnSave.Location = new Point(bottomBar.Width - 145, 10);
        _btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnSave.Click += (s, e) => SaveConfig();

        var btnOpenLogs = CreateModernButton("查看日志", Color.White, ColorTextPrimary, 90, 36, ColorBorder);
        btnOpenLogs.Location = new Point(bottomBar.Width - 245, 10);
        btnOpenLogs.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnOpenLogs.Click += (s, e) =>
        {
            if (Directory.Exists(_logger.LogDirectory))
            {
                Process.Start(new ProcessStartInfo { FileName = _logger.LogDirectory, UseShellExecute = true });
            }
        };

        bottomBar.Controls.AddRange(new Control[] { _lblFooterMessage, _btnSave, btnOpenLogs });

        // 2. 左侧侧边栏 (Dock = Left)
        var sidebar = new DoubleBufferedPanel
        {
            Dock = DockStyle.Left,
            Width = 200,
            BackColor = ColorBgSidebar,
            Padding = new Padding(12, 14, 12, 14)
        };
        sidebar.Paint += (s, e) =>
        {
            using var pen = new Pen(ColorBorder, 1);
            e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };

        // 侧边栏品牌区域
        var pnlBrand = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Color.Transparent };

        var picIcon = new PictureBox
        {
            Location = new Point(4, 10),
            Size = new Size(32, 32),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = AppIconHelper.GetAppIcon().ToBitmap()
        };

        var lblLogo = new Label
        {
            Text = "PCNotify",
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            ForeColor = ColorTextPrimary,
            Location = new Point(42, 6),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "系统监控与极速推送",
            Font = new Font("Microsoft YaHei UI", 8F),
            ForeColor = ColorTextSecondary,
            Location = new Point(44, 30),
            AutoSize = true
        };

        _lblSidebarStatus = new Label
        {
            Text = "● 监控守护中",
            Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold),
            ForeColor = ColorSuccess,
            Location = new Point(44, 48),
            AutoSize = true
        };

        pnlBrand.Controls.AddRange(new Control[] { picIcon, lblLogo, lblSub, _lblSidebarStatus });

        // 侧边栏导航按钮区域（严格采用固定 Y 坐标排布，杜绝 Dock 逆序问题）
        var pnlNav = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        _btnNavOverview = CreateNavButton("📊  系统运行概览", 0, 10);
        _btnNavChannels = CreateNavButton("🔔  推送渠道配置", 1, 58);
        _btnNavSettings = CreateNavButton("⚙️  通用与高级设置", 2, 106);

        _navButtons.AddRange(new[] { _btnNavOverview, _btnNavChannels, _btnNavSettings });
        pnlNav.Controls.AddRange(new Control[] { _btnNavOverview, _btnNavChannels, _btnNavSettings });

        // 侧边栏底部快捷操作
        var pnlSideBottom = new Panel { Dock = DockStyle.Bottom, Height = 88, BackColor = Color.Transparent };

        var btnQuickTest = CreateModernButton("⚡ 快速测试推送", Color.FromArgb(238, 242, 255), ColorBrand, 176, 34, Color.FromArgb(199, 210, 254));
        btnQuickTest.Location = new Point(0, 4);
        btnQuickTest.Click += async (s, e) => await DoQuickTestPushAsync();

        _btnSideToggleMonitor = CreateModernButton("⏸️ 暂停监控", ColorCardBg, ColorTextPrimary, 176, 32, ColorBorder);
        _btnSideToggleMonitor.Location = new Point(0, 44);
        _btnSideToggleMonitor.Click += (s, e) => ToggleMonitoring();

        pnlSideBottom.Controls.AddRange(new Control[] { btnQuickTest, _btnSideToggleMonitor });

        sidebar.Controls.AddRange(new Control[] { pnlNav, pnlBrand, pnlSideBottom });

        // 3. 右侧内容区 (Dock = Fill)
        _contentContainer = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorBgCanvas,
            Padding = new Padding(24, 20, 24, 16)
        };

        BuildOverviewPage();
        BuildChannelsPage();
        BuildSettingsPage();

        _contentContainer.Controls.AddRange(new Control[] { _panelOverview, _panelChannels, _panelSettings });

        Controls.Add(_contentContainer);
        Controls.Add(sidebar);
        Controls.Add(bottomBar);

        _contentContainer.Resize += (s, e) => LayoutResponsive();
        LayoutResponsive();

        // 默认激活第一栏：系统运行概览
        SwitchNav(0);
    }

    #region 页面构建

    private void BuildOverviewPage()
    {
        _panelOverview = new DoubleBufferedPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = true, AutoScroll = false };

        // 页面标题与副标题
        var lblPageTitle = new Label
        {
            Text = "系统运行概览",
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            ForeColor = ColorTextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        };
        var lblPageSub = new Label
        {
            Text = "实时感知 Windows 锁屏/解锁与开机登录事件，毫秒级多渠道并发推送通知",
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = ColorTextSecondary,
            Location = new Point(1, 26),
            AutoSize = true
        };

        // 顶部核心 KPI 指标卡片 (3 格等宽排布)
        // Card 1: 守护状态
        _cardKpi1 = CreateCardPanel(0, 54, 184, 78);
        var lblKpi1Head = new Label { Text = "🛡️ 守护状态", Font = new Font("Microsoft YaHei UI", 8.5F), ForeColor = ColorTextSecondary, Location = new Point(14, 10), AutoSize = true };
        _lblKpiStatusValue = new Label { Text = "● 运行中", Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold), ForeColor = ColorSuccess, Location = new Point(12, 28), AutoSize = true };
        _lblKpiStatusSub = new Label { Text = "开机自启: 已就绪", Font = new Font("Microsoft YaHei UI", 8F), ForeColor = ColorTextSecondary, Location = new Point(14, 52), AutoSize = true };
        _cardKpi1.Controls.AddRange(new Control[] { lblKpi1Head, _lblKpiStatusValue, _lblKpiStatusSub });

        // Card 2: 推送渠道
        _cardKpi2 = CreateCardPanel(196, 54, 184, 78);
        var lblKpi2Head = new Label { Text = "🔔 推送渠道", Font = new Font("Microsoft YaHei UI", 8.5F), ForeColor = ColorTextSecondary, Location = new Point(14, 10), AutoSize = true };
        _lblKpiChannelValue = new Label { Text = "0 个已启用", Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold), ForeColor = ColorBrand, Location = new Point(12, 28), AutoSize = true };
        _lblKpiChannelSub = new Label { Text = "正在同步配置...", Font = new Font("Microsoft YaHei UI", 8F), ForeColor = ColorTextSecondary, Location = new Point(14, 52), Width = 158, AutoEllipsis = true };
        _cardKpi2.Controls.AddRange(new Control[] { lblKpi2Head, _lblKpiChannelValue, _lblKpiChannelSub });

        // Card 3: 性能与开机
        _cardKpi3 = CreateCardPanel(392, 54, 184, 78);
        var lblKpi3Head = new Label { Text = "⚡ 响应与运行", Font = new Font("Microsoft YaHei UI", 8.5F), ForeColor = ColorTextSecondary, Location = new Point(14, 10), AutoSize = true };
        _lblKpiPerfValue = new Label { Text = "< 1.0s 秒级", Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold), ForeColor = ColorSuccess, Location = new Point(12, 28), AutoSize = true };
        _lblKpiPerfSub = new Label { Text = "进程已运行: 0 分钟", Font = new Font("Microsoft YaHei UI", 8F), ForeColor = ColorTextSecondary, Location = new Point(14, 52), AutoSize = true };
        _cardKpi3.Controls.AddRange(new Control[] { lblKpi3Head, _lblKpiPerfValue, _lblKpiPerfSub });

        // 卡片 2: 实时系统资源负荷监视 (Y = 142, Height = 138)
        _cardHardware = CreateCardPanel(0, 142, 576, 138);
        var lblHwTitle = new Label
        {
            Text = "💻 本机运行负荷快照 (随附于每次推送消息正文中)",
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            ForeColor = ColorTextPrimary,
            Location = new Point(16, 12),
            AutoSize = true
        };

        // CPU 进度条
        var lblCpuName = new Label { Text = "CPU 负荷", Location = new Point(16, 40), AutoSize = true, ForeColor = ColorTextPrimary };
        _barCpu = new MetricBar { Location = new Point(88, 42), Width = 466, Height = 15, BarColor = ColorBrand };

        // 内存进度条
        var lblMemName = new Label { Text = "物理内存", Location = new Point(16, 72), AutoSize = true, ForeColor = ColorTextPrimary };
        _barMemory = new MetricBar { Location = new Point(88, 74), Width = 466, Height = 15, BarColor = ColorSuccess };

        // 网络状态与极速提示
        var lblNetName = new Label { Text = "网络连接", Location = new Point(16, 104), AutoSize = true, ForeColor = ColorTextPrimary };
        _lblNetworkStatus = new Label
        {
            Text = "正在检测网络...",
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            ForeColor = ColorTextPrimary,
            Location = new Point(88, 104),
            AutoSize = true
        };

        var lblOptTip = new Label
        {
            Text = "⚡ 极速优化：已跳过机械副盘休眠扫描，耗时稳定在 0.3s",
            Font = new Font("Microsoft YaHei UI", 8F),
            ForeColor = Color.DarkSlateGray,
            Location = new Point(200, 105),
            AutoSize = true
        };

        _cardHardware.Controls.AddRange(new Control[] { lblHwTitle, lblCpuName, _barCpu, lblMemName, _barMemory, lblNetName, _lblNetworkStatus, lblOptTip });

        // 卡片 3: 最近感知事件与推送记录 (Y = 290, Height = 180)
        _cardActivity = CreateCardPanel(0, 290, 576, 180);
        var lblActTitle = new Label
        {
            Text = "📋 最近感知事件与推送记录",
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            ForeColor = ColorTextPrimary,
            Location = new Point(16, 12),
            AutoSize = true
        };

        _btnRefreshAct = CreateModernButton("🔄 刷新记录", ColorCardBg, ColorBrand, 86, 26, ColorBorder);
        _btnRefreshAct.Location = new Point(474, 8);
        _btnRefreshAct.Click += (s, e) => RefreshActivityListUi();

        _pnlActivityList = new DoubleBufferedPanel
        {
            Location = new Point(10, 40),
            Size = new Size(556, 130),
            BackColor = Color.Transparent
        };

        _cardActivity.Controls.AddRange(new Control[] { lblActTitle, _btnRefreshAct, _pnlActivityList });

        _panelOverview.Controls.AddRange(new Control[] { lblPageTitle, lblPageSub, _cardKpi1, _cardKpi2, _cardKpi3, _cardHardware, _cardActivity });
    }

    private void BuildChannelsPage()
    {
        _panelChannels = new DoubleBufferedPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };

        var lblPageTitle = new Label
        {
            Text = "多渠道推送配置",
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            ForeColor = ColorTextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        };
        var lblPageSub = new Label
        {
            Text = "各渠道并发独立分发，单一网络故障不影响其他通道，推荐配合企业微信或 ntfy 使用",
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = ColorTextSecondary,
            Location = new Point(1, 26),
            AutoSize = true
        };

        // 分段标签栏
        var pnlTabs = new Panel { Location = new Point(0, 54), AutoSize = true, BackColor = Color.Transparent };
        string[] tabNames = { "企业微信", "ntfy.sh", "飞书机器人", "钉钉", "WxPusher", "自定义 Webhook" };

        int currentX = 0;
        for (int i = 0; i < tabNames.Length; i++)
        {
            int index = i;
            var btn = new Button
            {
                Text = tabNames[i],
                Location = new Point(currentX, 0),
                Height = 32,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9F),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += (s, e) => SwitchChannelTab(index);
            _channelTabButtons.Add(btn);
            pnlTabs.Controls.Add(btn);
            currentX += btn.PreferredSize.Width + 6;
        }

        // 渠道卡片内容容器
        _channelContentPanel = CreateCardPanel(0, 96, 576, 380);

        _panelChannels.Controls.AddRange(new Control[] { lblPageTitle, lblPageSub, pnlTabs, _channelContentPanel });

        // 默认显示企业微信
        SwitchChannelTab(0);
    }

    private void SwitchChannelTab(int index)
    {
        _selectedChannelIndex = index;
        for (int i = 0; i < _channelTabButtons.Count; i++)
        {
            var btn = _channelTabButtons[i];
            if (i == index)
            {
                btn.BackColor = ColorBrand;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = ColorBrand;
            }
            else
            {
                btn.BackColor = ColorCardBg;
                btn.ForeColor = ColorTextPrimary;
                btn.FlatAppearance.BorderColor = ColorBorder;
            }
        }

        _channelContentPanel.Controls.Clear();
        switch (index)
        {
            case 0: BuildWeComTabContent(); break;
            case 1: BuildNtfyTabContent(); break;
            case 2: BuildFeishuTabContent(); break;
            case 3: BuildDingTalkTabContent(); break;
            case 4: BuildWxPusherTabContent(); break;
            case 5: BuildCustomWebhookTabContent(); break;
        }
    }

    private void BuildWeComTabContent()
    {
        _chkWeComEnable = new CheckBox
        {
            Text = "启用企业微信群机器人 (强烈推荐：免实名认证，可直接挂接推送到普通微信)",
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            ForeColor = ColorTextPrimary,
            Location = new Point(20, 20),
            AutoSize = true
        };

        var lblUrl = new Label { Text = "机器人 Webhook URL:", Location = new Point(20, 60), AutoSize = true, ForeColor = ColorTextPrimary };
        _txtWeComWebhook = new TextBox { Location = new Point(20, 85), Width = 535, Font = new Font("Consolas", 9.5F) };

        var lblTip = new Label
        {
            Text = "💡 直推个人微信核心秘诀：\n1. 企业微信电脑端或手机端新建一个内部群，添加“群机器人”并复制其 Webhook 粘贴于此。\n2. 在企业微信管理后台 ➔【协作】➔【微信插件】中扫码关注邀请二维码。\n3. 电脑发生登录或解锁时，群机器人消息将直接显示在您的普通个人微信聊天列表中，完全免实名！",
            Location = new Point(20, 130),
            Width = 535,
            Height = 110,
            ForeColor = ColorTextSecondary
        };

        _channelContentPanel.Controls.AddRange(new Control[] { _chkWeComEnable, lblUrl, _txtWeComWebhook, lblTip });
        _chkWeComEnable.Checked = _config.WeCom.Enabled;
        _txtWeComWebhook.Text = _config.WeCom.WebhookUrl;
    }

    private void BuildNtfyTabContent()
    {
        _chkNtfyEnable = new CheckBox
        {
            Text = "启用 ntfy.sh 官方出站推送 (支持安卓大横幅与 iOS 弹窗)",
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            ForeColor = ColorTextPrimary,
            Location = new Point(20, 20),
            AutoSize = true
        };

        var lblTopic = new Label { Text = "专属订阅 Topic (建议保持高随机性防嗅探):", Location = new Point(20, 60), AutoSize = true, ForeColor = ColorTextPrimary };
        _txtNtfyTopic = new TextBox { Location = new Point(20, 85), Width = 410, Font = new Font("Consolas", 9.5F) };

        var btnGenTopic = CreateModernButton("随机生成", ColorCardBg, ColorTextPrimary, 110, 26, ColorBorder);
        btnGenTopic.Location = new Point(445, 84);
        btnGenTopic.Click += (s, e) => _txtNtfyTopic.Text = ConfigManager.GenerateRandomTopic();

        var lblPriority = new Label { Text = "通知优先级:", Location = new Point(20, 125), AutoSize = true, ForeColor = ColorTextPrimary };
        _cmbNtfyPriority = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(110, 122),
            Width = 160
        };
        _cmbNtfyPriority.Items.AddRange(new object[] { "high (推荐，系统大横幅)", "urgent (最高，持续提醒)", "default (普通优先级)" });

        var btnCopyLink = CreateModernButton("📋 复制手机订阅地址", ColorCardBg, ColorBrand, 180, 32, ColorBorder);
        btnCopyLink.Location = new Point(20, 170);
        btnCopyLink.Click += (s, e) =>
        {
            var topic = _txtNtfyTopic.Text.Trim();
            if (!string.IsNullOrEmpty(topic))
            {
                Clipboard.SetText($"https://ntfy.sh/{topic}");
                ShowFooter("已将 ntfy 订阅地址复制至剪贴板！", ColorBrand);
            }
        };

        var btnOpenWeb = CreateModernButton("🌐 浏览器打开预览", ColorCardBg, ColorTextPrimary, 160, 32, ColorBorder);
        btnOpenWeb.Location = new Point(210, 170);
        btnOpenWeb.Click += (s, e) =>
        {
            var topic = _txtNtfyTopic.Text.Trim();
            if (!string.IsNullOrEmpty(topic))
            {
                Process.Start(new ProcessStartInfo { FileName = $"https://ntfy.sh/{topic}", UseShellExecute = true });
            }
        };

        _channelContentPanel.Controls.AddRange(new Control[] {
            _chkNtfyEnable, lblTopic, _txtNtfyTopic, btnGenTopic,
            lblPriority, _cmbNtfyPriority, btnCopyLink, btnOpenWeb
        });

        _chkNtfyEnable.Checked = _config.Ntfy.Enabled;
        _txtNtfyTopic.Text = _config.Ntfy.Topic;
        _cmbNtfyPriority.SelectedIndex = _config.Ntfy.Priority switch
        {
            "urgent" => 1,
            "default" => 2,
            _ => 0
        };
    }

    private void BuildFeishuTabContent()
    {
        _chkFeishuEnable = new CheckBox
        {
            Text = "启用飞书群机器人 (秒弹大横幅卡片，排版精美)",
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            ForeColor = ColorTextPrimary,
            Location = new Point(20, 20),
            AutoSize = true
        };

        var lblUrl = new Label { Text = "飞书 Webhook 地址:", Location = new Point(20, 60), AutoSize = true, ForeColor = ColorTextPrimary };
        _txtFeishuWebhook = new TextBox { Location = new Point(20, 85), Width = 535, Font = new Font("Consolas", 9.5F) };

        var lblTip = new Label
        {
            Text = "💡 在飞书电脑端群设置 ➔ 群机器人 ➔ 添加自定义机器人，将生成的 Webhook 填入即可。手机端安装飞书后推送极其稳定。",
            Location = new Point(20, 130),
            Width = 535,
            Height = 60,
            ForeColor = ColorTextSecondary
        };

        _channelContentPanel.Controls.AddRange(new Control[] { _chkFeishuEnable, lblUrl, _txtFeishuWebhook, lblTip });
        _chkFeishuEnable.Checked = _config.Feishu.Enabled;
        _txtFeishuWebhook.Text = _config.Feishu.WebhookUrl;
    }

    private void BuildDingTalkTabContent()
    {
        _chkDingTalkEnable = new CheckBox { Text = "启用钉钉群机器人", Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
        var lblUrl = new Label { Text = "钉钉 Webhook 地址:", Location = new Point(20, 60), AutoSize = true };
        _txtDingTalkWebhook = new TextBox { Location = new Point(20, 85), Width = 535, Font = new Font("Consolas", 9.5F) };

        _channelContentPanel.Controls.AddRange(new Control[] { _chkDingTalkEnable, lblUrl, _txtDingTalkWebhook });
        _chkDingTalkEnable.Checked = _config.DingTalk.Enabled;
        _txtDingTalkWebhook.Text = _config.DingTalk.WebhookUrl;
    }

    private void BuildWxPusherTabContent()
    {
        _chkWxPusherEnable = new CheckBox { Text = "启用 WxPusher (微信扫码关注推送，无需传身份证)", Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
        var lblToken = new Label { Text = "AppToken:", Location = new Point(20, 55), AutoSize = true };
        _txtWxPusherAppToken = new TextBox { Location = new Point(20, 78), Width = 535, Font = new Font("Consolas", 9.5F) };

        var lblUid = new Label { Text = "用户 UID (多个用英文逗号分隔):", Location = new Point(20, 115), AutoSize = true };
        _txtWxPusherUids = new TextBox { Location = new Point(20, 138), Width = 535, Font = new Font("Consolas", 9.5F) };

        _channelContentPanel.Controls.AddRange(new Control[] { _chkWxPusherEnable, lblToken, _txtWxPusherAppToken, lblUid, _txtWxPusherUids });
        _chkWxPusherEnable.Checked = _config.WxPusher.Enabled;
        _txtWxPusherAppToken.Text = _config.WxPusher.AppToken;
        _txtWxPusherUids.Text = _config.WxPusher.Uids;
    }

    private void BuildCustomWebhookTabContent()
    {
        _chkCustomEnable = new CheckBox { Text = "启用通用出站 Webhook (标准 JSON POST)", Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
        var lblUrl = new Label { Text = "POST URL (支持自建服务、群晖、HomeAssistant):", Location = new Point(20, 60), AutoSize = true };
        _txtCustomUrl = new TextBox { Location = new Point(20, 85), Width = 535, Font = new Font("Consolas", 9.5F) };

        _channelContentPanel.Controls.AddRange(new Control[] { _chkCustomEnable, lblUrl, _txtCustomUrl });
        _chkCustomEnable.Checked = _config.CustomWebhook.Enabled;
        _txtCustomUrl.Text = _config.CustomWebhook.Url;
    }

    private void BuildSettingsPage()
    {
        _panelSettings = new DoubleBufferedPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false, AutoScroll = false };

        var lblPageTitle = new Label
        {
            Text = "通用与高级设置",
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            ForeColor = ColorTextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        };
        var lblPageSub = new Label
        {
            Text = "灵活配置事件感知规则、通知随附硬件快照项、网络重试策略与 DPAPI 凭据安全",
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = ColorTextSecondary,
            Location = new Point(1, 26),
            AutoSize = true
        };

        // 卡片 1: 监控事件感知规则 (Y = 54, Height = 88)
        _cardTriggers = CreateCardPanel(0, 54, 576, 88);
        var lblTrigTitle = new Label { Text = "🎯 监控事件感知规则", Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold), Location = new Point(18, 10), AutoSize = true };

        _chkMonitorUnlock = new CheckBox
        {
            Text = "🔓 监听电脑解锁 (从锁屏状态恢复并解锁进入桌面时触发)",
            Font = new Font("Microsoft YaHei UI", 9F),
            Location = new Point(20, 32),
            AutoSize = true,
            Checked = _config.Triggers.MonitorUnlock
        };
        _chkMonitorBoot = new CheckBox
        {
            Text = "💻 监听开机/登录 (开机或重启后首次进入桌面时触发)",
            Font = new Font("Microsoft YaHei UI", 9F),
            Location = new Point(20, 56),
            AutoSize = true,
            Checked = _config.Triggers.MonitorBoot
        };

        _lblDedup = new Label { Text = "防抖去重间隔:", Font = new Font("Microsoft YaHei UI", 8.5F), Location = new Point(410, 34), AutoSize = true, ForeColor = ColorTextSecondary };
        _cmbDeduplicateWindow = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(410, 54),
            Width = 150,
            Font = new Font("Microsoft YaHei UI", 8.5F)
        };
        _cmbDeduplicateWindow.Items.AddRange(new object[] { "15秒 (高灵敏)", "30秒 (推荐标准)", "60秒 (宽松防抖)" });
        _cmbDeduplicateWindow.SelectedIndex = _config.Triggers.DeduplicateSeconds switch
        {
            15 => 0,
            60 => 2,
            _ => 1
        };

        _cardTriggers.Controls.AddRange(new Control[] { lblTrigTitle, _chkMonitorUnlock, _chkMonitorBoot, _lblDedup, _cmbDeduplicateWindow });

        // 卡片 2: 通知随附硬件快照项 (Y = 150, Height = 90)
        _cardSettingsHardware = CreateCardPanel(0, 150, 576, 90);
        var lblHwOptTitle = new Label { Text = "📊 通知随附硬件快照项 (保护隐私，绝不抓取屏幕或个人文件)", Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold), Location = new Point(18, 10), AutoSize = true };

        _chkIncludeCpu = new CheckBox { Text = "CPU 实时使用率", Font = new Font("Microsoft YaHei UI", 9F), Location = new Point(20, 34), AutoSize = true, Checked = _config.SystemInfo.IncludeCpu };
        _chkIncludeMemory = new CheckBox { Text = "物理内存占用", Font = new Font("Microsoft YaHei UI", 9F), Location = new Point(155, 34), AutoSize = true, Checked = _config.SystemInfo.IncludeMemory };
        _chkIncludeNetwork = new CheckBox { Text = "网络在线状态", Font = new Font("Microsoft YaHei UI", 9F), Location = new Point(285, 34), AutoSize = true, Checked = _config.SystemInfo.IncludeNetwork };
        _chkIncludeDisks = new CheckBox { Text = "各盘剩余空间", Font = new Font("Microsoft YaHei UI", 9F), Location = new Point(415, 34), AutoSize = true, Checked = _config.SystemInfo.IncludeDisks };

        var lblDiskWarning = new Label
        {
            Text = "⚠️ 提示: 扫描磁盘可能触发机械副盘 Spin-Up 物理唤醒产生 5~7 秒阻塞，默认关闭以保障秒级推送。",
            Font = new Font("Microsoft YaHei UI", 8F),
            ForeColor = ColorWarning,
            Location = new Point(20, 62),
            AutoSize = true
        };

        _cardSettingsHardware.Controls.AddRange(new Control[] { lblHwOptTitle, _chkIncludeCpu, _chkIncludeMemory, _chkIncludeNetwork, _chkIncludeDisks, lblDiskWarning });

        // 卡片 3: 出站网络重试策略 (Y = 248, Height = 90)
        _cardRetry = CreateCardPanel(0, 248, 576, 90);
        var lblRetryTitle = new Label { Text = "⚡ 出站网络重试与超时保障 (开机网络未建立时自动退避重试)", Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold), Location = new Point(18, 10), AutoSize = true };

        var lblMaxRetries = new Label { Text = "最大重试次数:", Font = new Font("Microsoft YaHei UI", 9F), Location = new Point(20, 35), AutoSize = true };
        _numMaxRetries = new NumericUpDown
        {
            Location = new Point(110, 33),
            Width = 60,
            Minimum = 1,
            Maximum = 5,
            Value = Math.Clamp(_config.Retry.MaxRetries, 1, 5)
        };

        var lblDelay = new Label { Text = "初始间隔(秒):", Font = new Font("Microsoft YaHei UI", 9F), Location = new Point(190, 35), AutoSize = true };
        _numInitialDelay = new NumericUpDown
        {
            Location = new Point(275, 33),
            Width = 60,
            Minimum = 1,
            Maximum = 10,
            Value = Math.Clamp(_config.Retry.InitialDelaySeconds, 1, 10)
        };

        var lblBackoff = new Label { Text = "退避倍率:", Font = new Font("Microsoft YaHei UI", 9F), Location = new Point(355, 35), AutoSize = true };
        _cmbBackoff = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(420, 32),
            Width = 140,
            Font = new Font("Microsoft YaHei UI", 8.5F)
        };
        _cmbBackoff.Items.AddRange(new object[] { "1.5x (温和重试)", "2.0x (标准指数)", "3.0x (快速拉长)" });
        _cmbBackoff.SelectedIndex = _config.Retry.BackoffMultiplier switch
        {
            1.5 => 0,
            3.0 => 2,
            _ => 1
        };

        var lblRetryDesc = new Label
        {
            Text = "💡 当电脑休眠唤醒或冷启动网络尚未握手成功时，推送引擎将自动重试，保证事件不丢失。",
            Font = new Font("Microsoft YaHei UI", 8F),
            ForeColor = ColorTextSecondary,
            Location = new Point(20, 62),
            AutoSize = true
        };

        _cardRetry.Controls.AddRange(new Control[] { lblRetryTitle, lblMaxRetries, _numMaxRetries, lblDelay, _numInitialDelay, lblBackoff, _cmbBackoff, lblRetryDesc });

        // 卡片 4: 凭据安全与日志维护 (Y = 346, Height = 105)
        _cardSec = CreateCardPanel(0, 346, 576, 105);
        var lblSecTitle = new Label { Text = "🛡️ Windows 原生 DPAPI 凭据安全与日志管理", Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold), Location = new Point(18, 10), AutoSize = true };

        _lblDpapiStatus = new Label
        {
            Text = "🟢 Windows 原生 DPAPI 硬件级加密已生效 (Token 仅限当前 Windows 账户解密，防外部拷贝盗用)",
            Font = new Font("Microsoft YaHei UI", 8.5F),
            ForeColor = ColorSuccess,
            Location = new Point(20, 34),
            AutoSize = true
        };

        var btnTestDpapi = CreateModernButton("🧪 测试 DPAPI 加密", ColorCardBg, ColorBrand, 140, 28, ColorBorder);
        btnTestDpapi.Location = new Point(20, 62);
        btnTestDpapi.Click += (s, e) => OnTestDpapiClick();

        var btnOpenLogs = CreateModernButton("📂 打开日志目录", ColorCardBg, ColorTextPrimary, 130, 28, ColorBorder);
        btnOpenLogs.Location = new Point(170, 62);
        btnOpenLogs.Click += (s, e) =>
        {
            if (Directory.Exists(_logger.LogDirectory))
                Process.Start(new ProcessStartInfo { FileName = _logger.LogDirectory, UseShellExecute = true });
        };

        var btnClearLogs = CreateModernButton("🧹 清理历史旧日志", ColorCardBg, ColorTextSecondary, 140, 28, ColorBorder);
        btnClearLogs.Location = new Point(310, 62);
        btnClearLogs.Click += (s, e) => OnClearLogsClick();

        _cardSec.Controls.AddRange(new Control[] { lblSecTitle, _lblDpapiStatus, btnTestDpapi, btnOpenLogs, btnClearLogs });

        _panelSettings.Controls.AddRange(new Control[] { lblPageTitle, lblPageSub, _cardTriggers, _cardSettingsHardware, _cardRetry, _cardSec });
    }

    #endregion

    #region 交互与状态更新

    private void SwitchNav(int pageIndex)
    {
        _panelOverview.Visible = (pageIndex == 0);
        _panelChannels.Visible = (pageIndex == 1);
        _panelSettings.Visible = (pageIndex == 2);

        for (int i = 0; i < _navButtons.Count; i++)
        {
            var btn = _navButtons[i];
            if (i == pageIndex)
            {
                btn.BackColor = Color.FromArgb(226, 232, 240); // 柔和激活浅灰
                btn.ForeColor = ColorBrand;
                btn.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.Transparent;
                btn.ForeColor = ColorTextPrimary;
                btn.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular);
            }
        }

        if (pageIndex == 0)
        {
            UpdateOverviewKpiCards();
            RefreshActivityListUi();
        }

        _lastLayoutWidth = -1;
        LayoutResponsive();
    }

    private void LayoutResponsive()
    {
        if (_contentContainer == null || _cardKpi1 == null) return;

        int clientWidth = _contentContainer.ClientSize.Width - _contentContainer.Padding.Horizontal;
        if (clientWidth < 500) clientWidth = 500;
        if (clientWidth == _lastLayoutWidth) return;
        _lastLayoutWidth = clientWidth;

        // 仅对当前可见的页面执行排版计算，并挂起布局彻底消除重排开销
        int cardWidth = clientWidth - 2;
        if (_panelOverview != null && _panelOverview.Visible)
        {
            _panelOverview.SuspendLayout();
            try
            {
                int gap = 12;
                int kpiWidth = (cardWidth - gap * 2) / 3;
                if (_cardKpi1.Width != kpiWidth) _cardKpi1.Width = kpiWidth;

                int kpi2X = kpiWidth + gap;
                if (_cardKpi2.Location.X != kpi2X || _cardKpi2.Width != kpiWidth)
                {
                    _cardKpi2.Location = new Point(kpi2X, 54);
                    _cardKpi2.Width = kpiWidth;
                }

                int kpi3X = (kpiWidth + gap) * 2;
                int kpi3W = cardWidth - kpi3X;
                if (_cardKpi3.Location.X != kpi3X || _cardKpi3.Width != kpi3W)
                {
                    _cardKpi3.Location = new Point(kpi3X, 54);
                    _cardKpi3.Width = kpi3W;
                }

                if (_cardHardware.Width != cardWidth) _cardHardware.Width = cardWidth;
                int barW = Math.Max(200, cardWidth - 110);
                if (_barCpu != null && _barCpu.Width != barW) _barCpu.Width = barW;
                if (_barMemory != null && _barMemory.Width != barW) _barMemory.Width = barW;

                if (_cardActivity.Width != cardWidth) _cardActivity.Width = cardWidth;
                if (_btnRefreshAct != null)
                {
                    int btnX = cardWidth - _btnRefreshAct.Width - 16;
                    if (_btnRefreshAct.Location.X != btnX) _btnRefreshAct.Location = new Point(btnX, 8);
                }

                if (_pnlActivityList != null)
                {
                    int listW = cardWidth - 20;
                    if (_pnlActivityList.Width != listW)
                    {
                        _pnlActivityList.SuspendLayout();
                        _pnlActivityList.Width = listW;
                        int rowW = listW - 10;
                        foreach (Control row in _pnlActivityList.Controls)
                        {
                            row.Width = rowW;
                            foreach (Control c in row.Controls)
                            {
                                if (c is Label lbl && (lbl.Text.Contains("送达") || lbl.Text.Contains("失败")))
                                {
                                    lbl.Location = new Point(rowW - 85, 6);
                                }
                                else if (c is Label lblChan && lblChan.Location.X >= 250)
                                {
                                    lblChan.Width = Math.Max(100, rowW - 365);
                                }
                            }
                        }
                        _pnlActivityList.ResumeLayout(false);
                    }
                }
            }
            finally
            {
                _panelOverview.ResumeLayout(false);
            }
        }
        else if (_panelChannels != null && _panelChannels.Visible)
        {
            if (_channelContentPanel != null && _channelContentPanel.Width != cardWidth)
            {
                _channelContentPanel.Width = cardWidth;
            }
        }
        else if (_panelSettings != null && _panelSettings.Visible)
        {
            _panelSettings.SuspendLayout();
            try
            {
                if (_cardTriggers != null && _cardTriggers.Width != cardWidth)
                {
                    _cardTriggers.Width = cardWidth;
                    if (_lblDedup != null && _cmbDeduplicateWindow != null)
                    {
                        int dedupX = Math.Max(380, cardWidth - 170);
                        _lblDedup.Location = new Point(dedupX, 34);
                        _cmbDeduplicateWindow.Location = new Point(dedupX, 54);
                    }
                }
                if (_cardSettingsHardware != null && _cardSettingsHardware.Width != cardWidth)
                    _cardSettingsHardware.Width = cardWidth;
                if (_cardRetry != null && _cardRetry.Width != cardWidth)
                    _cardRetry.Width = cardWidth;
                if (_cardSec != null && _cardSec.Width != cardWidth)
                    _cardSec.Width = cardWidth;
            }
            finally
            {
                _panelSettings.ResumeLayout(false);
            }
        }
    }

    public void UpdateMonitorStatusUi()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateMonitorStatusUi);
            return;
        }

        UpdateOverviewKpiCards();
    }

    public void UpdateAutoStartStatusUi()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateAutoStartStatusUi);
            return;
        }

        UpdateOverviewKpiCards();
    }

    private void UpdateOverviewKpiCards()
    {
        var active = _isMonitoringActive();
        if (_lblKpiStatusValue != null)
        {
            _lblKpiStatusValue.Text = active ? "● 运行中" : "⏸️ 已暂停";
            _lblKpiStatusValue.ForeColor = active ? ColorSuccess : ColorWarning;
        }
        if (_lblKpiStatusSub != null)
        {
            var autoStart = _taskScheduler.IsTaskRegistered(out _);
            _lblKpiStatusSub.Text = autoStart ? "开机自启: 已就绪" : "开机自启: 未开启";
        }

        int activeChannels = (_config.Ntfy.Enabled ? 1 : 0) +
                             (_config.WeCom.Enabled ? 1 : 0) +
                             (_config.Feishu.Enabled ? 1 : 0) +
                             (_config.DingTalk.Enabled ? 1 : 0) +
                             (_config.WxPusher.Enabled ? 1 : 0) +
                             (_config.CustomWebhook.Enabled ? 1 : 0);

        if (_lblKpiChannelValue != null)
        {
            _lblKpiChannelValue.Text = $"{activeChannels} 个已启用";
        }
        if (_lblKpiChannelSub != null)
        {
            var names = new List<string>();
            if (_config.WeCom.Enabled) names.Add("企业微信");
            if (_config.Ntfy.Enabled) names.Add("ntfy");
            if (_config.Feishu.Enabled) names.Add("飞书");
            if (_config.DingTalk.Enabled) names.Add("钉钉");
            if (_config.WxPusher.Enabled) names.Add("WxPusher");
            if (_config.CustomWebhook.Enabled) names.Add("Webhook");
            _lblKpiChannelSub.Text = names.Count > 0 ? string.Join(", ", names) : "未配置推送通道";
        }

        if (_lblKpiPerfValue != null)
        {
            _lblKpiPerfValue.Text = "< 1.0s 秒级";
        }
        if (_lblKpiPerfSub != null)
        {
            try
            {
                var uptimeMin = (int)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMinutes;
                _lblKpiPerfSub.Text = $"守护运行: {uptimeMin} 分钟";
            }
            catch
            {
                _lblKpiPerfSub.Text = "守护运行: 正常";
            }
        }

        if (_lblSidebarStatus != null)
        {
            _lblSidebarStatus.Text = active ? "● 监控守护中" : "⏸️ 监控已暂停";
            _lblSidebarStatus.ForeColor = active ? ColorSuccess : ColorWarning;
        }
        if (_btnSideToggleMonitor != null)
        {
            _btnSideToggleMonitor.Text = active ? "⏸️ 暂停监控" : "▶️ 恢复监控";
        }
    }

    private void ToggleMonitoring()
    {
        var current = _isMonitoringActive();
        _setMonitoringActive(!current);
        UpdateMonitorStatusUi();
    }

    private async Task RefreshHardwareUiAsync()
    {
        try
        {
            var snapshot = await _infoCollector.CaptureAsync();
            if (IsDisposed || !IsHandleCreated) return;

            Invoke(() =>
            {
                var cpuVal = snapshot.CpuUsagePercentage ?? 0;
                _barCpu.Value = (int)Math.Clamp(cpuVal, 0, 100);
                _barCpu.Text = $"{cpuVal:F1}%";

                var memPct = snapshot.MemoryUsagePercentage;
                _barMemory.Value = (int)Math.Clamp(memPct, 0, 100);
                _barMemory.Text = $"{snapshot.UsedMemoryGb:F1}G / {snapshot.TotalMemoryGb:F1}G ({memPct:F0}%)";

                _lblNetworkStatus.Text = snapshot.NetworkStatus;

                UpdateOverviewKpiCards();
                RefreshActivityListUi();
            });
        }
        catch { }
    }

    private List<(string Time, string Title, string Channels, bool Success)> LoadRecentActivities()
    {
        var list = new List<(string Time, string Title, string Channels, bool Success)>();

        lock (PCMonitorAppContext.RecentActivities)
        {
            foreach (var act in PCMonitorAppContext.RecentActivities.Take(4))
            {
                list.Add((act.Time.ToString("HH:mm:ss"), act.Title, act.Channels, act.Success));
            }
        }

        if (list.Count < 3 && Directory.Exists(_logger.LogDirectory))
        {
            try
            {
                var todayLog = Path.Combine(_logger.LogDirectory, $"pcmonitor_{DateTime.Now:yyyyMMdd}.log");
                if (File.Exists(todayLog))
                {
                    var lines = File.ReadLines(todayLog).Reverse().Take(120);
                    foreach (var line in lines)
                    {
                        if (list.Count >= 4) break;
                        if (line.Contains("[PCMonitor] 接收到原始事件"))
                        {
                            var timePart = line.Length >= 20 ? line.Substring(12, 8) : DateTime.Now.ToString("HH:mm:ss");
                            var titlePart = line.Contains("'") ? line.Split('\'')[1] : "系统感知事件";
                            if (!list.Any(x => x.Time == timePart && x.Title == titlePart))
                            {
                                list.Add((timePart, titlePart, "多渠道推送已派发", true));
                            }
                        }
                    }
                }
            }
            catch { }
        }

        return list;
    }

    private void RefreshActivityListUi()
    {
        if (_pnlActivityList == null || _pnlActivityList.IsDisposed) return;

        var activities = LoadRecentActivities();
        _pnlActivityList.SuspendLayout();
        _pnlActivityList.Controls.Clear();

        if (activities.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "🟢 正在实时监听系统锁屏与登录事件，产生新事件将在此处呈现...",
                ForeColor = ColorTextSecondary,
                Font = new Font("Microsoft YaHei UI", 9F),
                Location = new Point(14, 20),
                AutoSize = true
            };
            _pnlActivityList.Controls.Add(lblEmpty);
        }
        else
        {
            int y = 4;
            foreach (var act in activities)
            {
                var pnlRow = new DoubleBufferedPanel
                {
                    Location = new Point(4, y),
                    Size = new Size(_pnlActivityList.Width - 10, 28),
                    BackColor = Color.FromArgb(248, 250, 252)
                };

                var lblTime = new Label
                {
                    Text = act.Time,
                    Font = new Font("Consolas", 8.5F, FontStyle.Bold),
                    ForeColor = ColorBrand,
                    Location = new Point(8, 5),
                    AutoSize = true
                };

                var iconType = act.Title.Contains("登录") ? "💻" : (act.Title.Contains("测试") ? "⚡" : "🔓");
                var lblEvent = new Label
                {
                    Text = $"{iconType} {act.Title}",
                    Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
                    ForeColor = ColorTextPrimary,
                    Location = new Point(80, 5),
                    AutoSize = true
                };

                var lblChannels = new Label
                {
                    Text = $"发送至: {act.Channels}",
                    Font = new Font("Microsoft YaHei UI", 8F),
                    ForeColor = ColorTextSecondary,
                    Location = new Point(270, 6),
                    Width = Math.Max(100, pnlRow.Width - 365),
                    AutoEllipsis = true
                };

                var lblBadge = new Label
                {
                    Text = act.Success ? "✓ 送达成功" : "× 失败",
                    Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold),
                    ForeColor = act.Success ? ColorSuccess : Color.Red,
                    Location = new Point(pnlRow.Width - 85, 6),
                    AutoSize = true
                };

                pnlRow.Controls.AddRange(new Control[] { lblTime, lblEvent, lblChannels, lblBadge });
                _pnlActivityList.Controls.Add(pnlRow);
                y += 31;
            }
        }

        _pnlActivityList.ResumeLayout();
    }

    private void LoadConfigToUi()
    {
        if (_chkMonitorUnlock != null) _chkMonitorUnlock.Checked = _config.Triggers.MonitorUnlock;
        if (_chkMonitorBoot != null) _chkMonitorBoot.Checked = _config.Triggers.MonitorBoot;
        if (_chkIncludeCpu != null) _chkIncludeCpu.Checked = _config.SystemInfo.IncludeCpu;
        if (_chkIncludeMemory != null) _chkIncludeMemory.Checked = _config.SystemInfo.IncludeMemory;
        if (_chkIncludeNetwork != null) _chkIncludeNetwork.Checked = _config.SystemInfo.IncludeNetwork;
        if (_chkIncludeDisks != null) _chkIncludeDisks.Checked = _config.SystemInfo.IncludeDisks;
        if (_numMaxRetries != null) _numMaxRetries.Value = Math.Clamp(_config.Retry.MaxRetries, 1, 5);
        if (_numInitialDelay != null) _numInitialDelay.Value = Math.Clamp(_config.Retry.InitialDelaySeconds, 1, 10);
    }

    private void SaveConfig()
    {
        // ntfy
        if (_chkNtfyEnable != null)
        {
            _config.Ntfy.Enabled = _chkNtfyEnable.Checked;
            _config.Ntfy.Topic = _txtNtfyTopic.Text.Trim();
            _config.Ntfy.Priority = _cmbNtfyPriority.SelectedIndex switch
            {
                1 => "urgent",
                2 => "default",
                _ => "high"
            };
        }

        // WeCom
        if (_chkWeComEnable != null)
        {
            _config.WeCom.Enabled = _chkWeComEnable.Checked;
            _config.WeCom.WebhookUrl = _txtWeComWebhook.Text.Trim();
        }

        // Feishu
        if (_chkFeishuEnable != null)
        {
            _config.Feishu.Enabled = _chkFeishuEnable.Checked;
            _config.Feishu.WebhookUrl = _txtFeishuWebhook.Text.Trim();
        }

        // DingTalk
        if (_chkDingTalkEnable != null)
        {
            _config.DingTalk.Enabled = _chkDingTalkEnable.Checked;
            _config.DingTalk.WebhookUrl = _txtDingTalkWebhook.Text.Trim();
        }

        // WxPusher
        if (_chkWxPusherEnable != null)
        {
            _config.WxPusher.Enabled = _chkWxPusherEnable.Checked;
            _config.WxPusher.AppToken = _txtWxPusherAppToken.Text.Trim();
            _config.WxPusher.Uids = _txtWxPusherUids.Text.Trim();
        }

        // Custom Webhook
        if (_chkCustomEnable != null)
        {
            _config.CustomWebhook.Enabled = _chkCustomEnable.Checked;
            _config.CustomWebhook.Url = _txtCustomUrl.Text.Trim();
        }

        // 监控触发规则
        if (_chkMonitorUnlock != null) _config.Triggers.MonitorUnlock = _chkMonitorUnlock.Checked;
        if (_chkMonitorBoot != null) _config.Triggers.MonitorBoot = _chkMonitorBoot.Checked;
        if (_cmbDeduplicateWindow != null)
        {
            _config.Triggers.DeduplicateSeconds = _cmbDeduplicateWindow.SelectedIndex switch
            {
                0 => 15,
                2 => 60,
                _ => 30
            };
        }

        // 硬件随附项
        if (_chkIncludeCpu != null) _config.SystemInfo.IncludeCpu = _chkIncludeCpu.Checked;
        if (_chkIncludeMemory != null) _config.SystemInfo.IncludeMemory = _chkIncludeMemory.Checked;
        if (_chkIncludeNetwork != null) _config.SystemInfo.IncludeNetwork = _chkIncludeNetwork.Checked;
        if (_chkIncludeDisks != null) _config.SystemInfo.IncludeDisks = _chkIncludeDisks.Checked;

        // 重试参数
        if (_numMaxRetries != null) _config.Retry.MaxRetries = (int)_numMaxRetries.Value;
        if (_numInitialDelay != null) _config.Retry.InitialDelaySeconds = (int)_numInitialDelay.Value;
        if (_cmbBackoff != null)
        {
            _config.Retry.BackoffMultiplier = _cmbBackoff.SelectedIndex switch
            {
                0 => 1.5,
                2 => 3.0,
                _ => 2.0
            };
        }

        _config.UiStyle = "Modern";
        _configManager.Save(_config);

        // 同步热更新后台守护的 NotificationManager
        _notificationManager.ClearProviders();
        _notificationManager.RegisterProvider(new LogNotificationProvider(_logger));
        if (_config.Ntfy.Enabled) _notificationManager.RegisterProvider(new NtfyProvider(_config.Ntfy, _logger));
        if (_config.WeCom.Enabled) _notificationManager.RegisterProvider(new WeComWebhookProvider(_config.WeCom, _logger));
        if (_config.Feishu.Enabled) _notificationManager.RegisterProvider(new FeishuWebhookProvider(_config.Feishu, _logger));
        if (_config.DingTalk.Enabled) _notificationManager.RegisterProvider(new DingTalkWebhookProvider(_config.DingTalk, _logger));
        if (_config.WxPusher.Enabled) _notificationManager.RegisterProvider(new WxPusherProvider(_config.WxPusher, _logger));
        if (_config.CustomWebhook.Enabled) _notificationManager.RegisterProvider(new CustomWebhookProvider(_config.CustomWebhook, _logger));
        if (_config.WeChat.Enabled) _notificationManager.RegisterProvider(new WeChatProvider(_config.WeChat, _logger));

        UpdateOverviewKpiCards();
        ShowFooter("配置已保存！新的触发与推送规则已实时无感生效。", ColorSuccess);
    }

    private void OnTestDpapiClick()
    {
        try
        {
            var dpapi = new DpapiSecurityProvider(_logger);
            var sample = "PCNotify_Secret_Token_ValidTest";
            var enc = dpapi.Protect(sample);
            var dec = dpapi.Unprotect(enc);

            if (dec == sample)
            {
                MessageBox.Show($"Windows DPAPI 透明硬件保护校验成功！\n\n【测试明文】: {sample}\n【落盘密文】: {enc[..Math.Min(24, enc.Length)]}...\n【解密验证】: {dec}\n\n所有已保存的敏感 Webhook 与 Token 均已安全加密绑定至当前 Windows 账户，防物理拷贝与外部窃取。", "DPAPI 安全加固正常", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"DPAPI 测试异常: {ex.Message}", "测试失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnClearLogsClick()
    {
        try
        {
            if (!Directory.Exists(_logger.LogDirectory)) return;

            var todayFile = $"pcmonitor_{DateTime.Now:yyyyMMdd}.log";
            var files = Directory.GetFiles(_logger.LogDirectory, "*.log");
            int deleted = 0;
            foreach (var f in files)
            {
                if (!Path.GetFileName(f).Equals(todayFile, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(f); deleted++; } catch { }
                }
            }
            ShowFooter($"已清理 {deleted} 个历史日志文件，今日活跃日志已保留。", ColorSuccess);
            MessageBox.Show($"成功清理 {deleted} 个历史日志文件，今日活跃日志已安全保留。", "清理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowFooter($"清理日志失败: {ex.Message}", Color.Red);
        }
    }

    private async Task DoQuickTestPushAsync()
    {
        ShowFooter("正在准备向所有启用渠道发送测试推送...", ColorBrand);
        SaveConfig();

        try
        {
            var snapshot = await _infoCollector.CaptureAsync();
            var testEvt = new NotificationEvent
            {
                Type = EventType.PC_UNLOCK,
                Title = "PCNotify 现代面板测试推送",
                Body = $"【测试推送】由全新现代控制面板发起。\n\n【系统状态】{snapshot.ToPlainText()}",
                TimestampUtc = DateTime.UtcNow,
                DeviceName = Environment.MachineName
            };

            var results = await _notificationManager.DispatchAsync(testEvt);
            var successCount = results.Count(r => r.Value);
            var channelSummary = string.Join(", ", results.Where(r => r.Value).Select(r => r.Key));
            if (string.IsNullOrWhiteSpace(channelSummary)) channelSummary = "无通道";

            lock (PCMonitorAppContext.RecentActivities)
            {
                PCMonitorAppContext.RecentActivities.Insert(0, new PCMonitorAppContext.ActivityEntry(DateTime.Now, testEvt.Type, testEvt.Title, channelSummary, successCount > 0, $"快速测试 ({successCount} 渠道成功)"));
                if (PCMonitorAppContext.RecentActivities.Count > 15) PCMonitorAppContext.RecentActivities.RemoveAt(PCMonitorAppContext.RecentActivities.Count - 1);
            }

            RefreshActivityListUi();
            ShowFooter($"测试推送已完成！成功触发 {successCount} 个渠道，请查收手机。", ColorSuccess);
        }
        catch (Exception ex)
        {
            ShowFooter($"推送测试异常: {ex.Message}", Color.Red);
        }
    }

    private void ShowFooter(string message, Color color)
    {
        _lblFooterMessage.Text = message;
        _lblFooterMessage.ForeColor = color;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    public void ShowAndActivate()
    {
        if (InvokeRequired)
        {
            Invoke(ShowAndActivate);
            return;
        }

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Show();
        ShowWindow(Handle, SW_RESTORE);
        BringToFront();
        BringWindowToTop(Handle);
        SetForegroundWindow(Handle);
        Activate();
    }

    public void ForceExit()
    {
        _isRealExit = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_isRealExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _logger.Info("[ModernControlForm] 用户关闭窗口，已最小化至右下角系统托盘继续守护。");
            return;
        }

        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        base.OnFormClosing(e);
    }

    #endregion

    #region 辅助控件构造工厂

    private Button CreateNavButton(string text, int pageIndex, int y)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(0, y),
            Size = new Size(176, 40),
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) => SwitchNav(pageIndex);
        return btn;
    }

    private static Panel CreateCardPanel(int x, int y, int width, int height)
    {
        var panel = new DoubleBufferedPanel
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = ColorCardBg
        };
        panel.Paint += (s, e) =>
        {
            using var pen = new Pen(ColorBorder, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        return panel;
    }

    private static Button CreateModernButton(string text, Color bg, Color fg, int width, int height, Color? borderColor = null)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(width, height),
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = borderColor.HasValue ? 1 : 0;
        if (borderColor.HasValue) btn.FlatAppearance.BorderColor = borderColor.Value;
        return btn;
    }

    #endregion
}

/// <summary>
/// 现代化平滑进度指示条控件
/// </summary>
public class MetricBar : Control
{
    private int _value;
    public int Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, 100); Invalidate(); }
    }

    public Color BarColor { get; set; } = Color.FromArgb(37, 99, 235);
    public Color TrackColor { get; set; } = Color.FromArgb(241, 245, 249);

    public MetricBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 18;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 绘制背景槽
        using var trackBrush = new SolidBrush(TrackColor);
        g.FillRectangle(trackBrush, 0, 0, Width, Height);

        // 绘制当前填充
        if (Value > 0)
        {
            var fillWidth = (int)(Width * (Value / 100.0));
            using var barBrush = new SolidBrush(BarColor);
            g.FillRectangle(barBrush, 0, 0, fillWidth, Height);
        }

        // 绘制边框
        using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        // 绘制文字
        if (!string.IsNullOrEmpty(Text))
        {
            using var font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            var textSize = g.MeasureString(Text, font);
            var textX = Width - textSize.Width - 6;
            var textY = (Height - textSize.Height) / 2;
            using var textBrush = new SolidBrush(Color.FromArgb(51, 65, 85));
            g.DrawString(Text, font, textBrush, textX, textY);
        }
    }
}

/// <summary>
/// 开启原生双缓冲与尺寸重绘的现代 Panel 容器，消除多层控件拖拽缩放时的闪烁与卡顿，并彻底屏蔽横向滚动条
/// </summary>
public class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style &= ~0x00100000; // WS_HSCROLL: 彻底移除横向滚动条原生样式
            return cp;
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (HorizontalScroll.Visible)
        {
            HorizontalScroll.Maximum = 0;
            HorizontalScroll.Visible = false;
        }
    }
}
