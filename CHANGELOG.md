# PCNotify 变更记录 (CHANGELOG)

所有重要变更均严格按 `YYYY-MM-DD HH:mm:ss` 时间戳倒序记录。

---

## [v0.1.0] - 2026-09-11 - PCNotify 0.1 官方首发版本 (Official First Release)
- **单例模式与窗口独一性全面加固**：
  - 将全局 Mutex 提升为应用程序生命周期静态强引用，彻底杜绝 .NET GC 垃圾回收导致互斥锁丢失产生多实例的问题。
  - 引入 Win32 `RegisterWindowMessage` 广播管道与原生窗体唤醒（`ShowWindowAsync(SW_RESTORE)` + `SetForegroundWindow`），重复启动时毫秒级唤醒置顶已有窗口并秒退新进程。
  - 重构 UI 调度器与线程安全锁，消灭跨线程弹窗冲突与托盘双击重复实例化缺陷。
- **界面与交互体验全面升维**：
  - 彻底根除窗口拉大拉小出现的不必要横向/纵向滑块（从底层 Win32 Style 剥除 `WS_HSCROLL` 结合流式自适应布局）。
  - 支持平滑窗口拉伸缩放与 60+ FPS 流畅拖拽。
- **品牌视觉全新换装 (Windows 矩形几何风)**：
  - 采用致敬 Windows Terminal / PowerToys 官方开发者工具的“重叠双窗格”现代几何图标。
  - GDI+ 自适应数学矢量生成，包含 16x16、24x24、32x32、48x48、64x64、128x128、256x256 全尺寸规范的高清 `app.ico` 与 `app.png`。
- **发布与打包**：
  - 单文件发布体系收敛生成纯单一执行文件 `dist/PCNotify.exe`（643 KB）。
  - 开源合规：配置标准化 `.gitignore` 与 MIT 开源协议 `LICENSE`。
  - 24 项全量单元测试 100% 通过。

---
- **移除旧版界面**：
  - 彻底移除老旧的 `MainControlForm.cs`，全面统一收敛至 Win10/11 Fluent 极简现代卡片界面（`ModernControlForm.cs`）。
  - 清理 `PCMonitorAppContext.cs` 与托盘右键菜单中的界面切换入口，保持代码结构清晰轻量。
  - 在“高级设置”中新增“关于软件与 DPAPI 凭据安全”展示卡片，信息更充实规范。
- **统一全局高清图标体系 (App Icon Unification)**：
  - 解决任务栏图标与系统托盘图标不一致问题（此前任务栏使用默认窗口图标，托盘使用 Shield 盾牌图标）。
  - 新增 `scripts/generate-icon.ps1`，程序化绘制 Windows 11 Fluent 风格的统一多分辨率品牌图标（PC 监控荧幕 + 翡翠绿脉冲通知徽标）。
  - 生成 16x16、24x24、32x32、48x48、64x64、128x128、256x256 完整尺寸集，输出至 `src/PCMonitor/Assets/app.ico` 与 `app.png`。
  - 在 `PCMonitor.csproj` 中配置 `<ApplicationIcon>Assets\app.ico</ApplicationIcon>` 并作为嵌入式资源编译。
  - 新增 `AppIconHelper.cs`，确保 `ModernControlForm`（任务栏和窗口标题栏）、`NotifyIcon`（系统托盘）、`PCNotify.exe`（资源管理器与桌面/开始菜单快捷方式）**全场景 100% 呈现完全一致的高清图标**。
- **测试与验证**：
  - 全量 24 项自动化单元测试 100% 通过（**24 passed, 0 failed**）。
  - 单文件发布打包与一键安装脚本已全量更新。

---

## [2026-09-10 21:30:00] - 阶段四全量交付：单文件独立打包、一键安装向导与 Windows DPAPI 凭据安全加固
- **单文件独立打包发布**：
  - 新增 `scripts/publish.ps1`，配置 Release 单文件打包管线，一键编译生成独立的 `dist/PCNotify.exe`。
  - 体积仅 **319 KB**，零零散散的依赖 DLL 全部收敛合一，无缝便携。
- **Windows DPAPI 凭据安全存储**：
  - 新增 `DpapiSecurityProvider.cs`，使用 Windows 原生安全数据保护接口（`CryptProtectData` / `CryptUnprotectData`），零第三方外部依赖。
  - `ConfigManager.cs` 接入透明加解密管道：写入磁盘的 `config.json` 中的 Webhook 地址与 Token 自动以 `enc:` 密文形式存储；内存中保持明文活跃对象；支持向后兼容无感平滑迁移明文配置。
- **一键安装与卸载向导**：
  - 新增 `scripts/install.ps1`：自动部署至 `%LOCALAPPDATA%\Programs\PCNotify\`，生成桌面与开始菜单快捷方式，并自动完成登录自启注册。
  - 新增 `scripts/uninstall.ps1`：安全停止守护进程、注销任务计划、清理快捷方式与程序文件，友好保留用户配置。
- **自动化单元测试**：
  - 新增 `SaveAndLoad_WithDpapiSecurityProvider_TransparentlyProtectsSensitiveFieldsOnDisk` 与 `LoadOrCreate_WithLegacyPlaintextConfig_SeamlesslyReadsPlaintext` 测试，全量测试套件增至 **24 项全部通过（24 passed, 0 failed）**。

---

## [2026-09-10 19:40:00] - 根除休眠唤醒延迟：禁用磁盘容量遍历，实现全天候秒级极速推送
- **根因定位与排查**：通过日志精准发现，系统长时间空闲后解锁延迟由 2~3s 增至 6~7s 的根因并非网络服务，而是 `SystemInfoCollector.CaptureDisks()` 触发了已休眠副盘/机械硬盘的物理唤醒（Spin-Up），Windows 内核 I/O 阻塞导致延迟 7 秒。
- **配置与通知优化**：
  - 将通知中的硬盘信息剔除，`PCNotifyConfig.SystemInfo.IncludeDisks` 默认值设为 `false`。
  - 同步更新用户正在使用的 `config.json`，彻底跳过 `DriveInfo.GetDrives()` 磁盘扫描。
  - 调整 `MainControlForm.cs`：在未采集磁盘信息时显示友好优化提示。
- **实测性能突破**：
  - 解锁与启动通知的前置采集耗时由 7.18 秒暴降至 380 毫秒。
  - 企业微信与 ntfy 推送从事件发生到手机送达全链路总耗时稳定在 0.7~1 秒。
- **自动化测试**：新增 `CaptureAsync_WhenIncludeDisksIsFalse_ExcludesDisksFromSnapshot` 单元测试，全量测试套件增至 22 项全部通过（22 passed, 0 failed）。

---

## [2026-09-10 17:00:00] - 推出多渠道推送聚合引擎 (Push Aggregator) 与免实名方案
- **免实名微信推送落地**：新增 `WeComWebhookProvider.cs`（企业微信机器人），无需营业执照、免身份证实名认证，群机器人可挂接微信插件直接将电脑通知推送到个人普通微信聊天列表。
- **极速大横幅推送落地**：新增 `FeishuWebhookProvider.cs`（飞书自定义机器人），采用 `interactive` 富文本卡片格式，手机端极速弹出系统大横幅。
- **多渠道聚合扩展**：
  - 新增 `DingTalkWebhookProvider.cs`（钉钉机器人）。
  - 新增 `WxPusherProvider.cs`（WxPusher 微信扫码关注推送，免传身份证）。
  - 新增 `CustomWebhookProvider.cs`（通用 JSON POST Webhook）。
  - 升级 `NtfyProvider.cs`：默认通知优先级提升为 `high`，强行唤起手机大横幅，彻底解决 Android 静默入通知栏不弹窗问题。
- **控制面板升级**：
  - `MainControlForm.cs` 全面改造为 TabControl 多渠道选项卡布局，清晰划分 6 大渠道。
  - 支持配置热重载：点击保存配置后，后台守护线程通过 `NotificationManager.ClearProviders()` 动态无感生效。
- **自动化测试**：新增 `AggregatedProviderTests.cs`，涵盖各渠道 Mock HTTP 测试与并发隔离测试，测试套件增加至 21 个，全部通过（21 passed, 0 failed）。

---

## [2026-09-10 12:26:00] - 推出图形控制面板 (GUI) 与系统托盘 (Tray) 交互体系
- 新增 `src/PCMonitor/Forms/MainControlForm.cs`：
  - 现代化控制面板，包含监控主开关（运行中/已暂停）、开机自启联动复选框。
  - ntfy 卡片：Topic 输入、一键复制手机订阅链接、一键打开浏览器查看、随机生成 Topic。
  - 微信卡片：Token 输入框、密码显隐切换、免费申请链接快捷跳转。
  - 实时状态卡片：动态展示 CPU 负荷、内存占比、各盘余量与网络状态。
  - 快捷操作：保存配置、立即测试推送、打开日志目录、最小化到托盘。
- 增强 `PCMonitorAppContext.cs`：
  - 集成 `NotifyIcon` 系统托盘，支持双击弹出控制面板，右键菜单快速操作。
  - 引入 `Global\PCNotify_ShowWindow_Event` 命名事件：当已有实例运行时，再次启动自动唤出主面板。
  - 点击红叉默认最小化至托盘，防止用户误操作关闭后台守护。
- 新增脚本 `scripts/open-gui.ps1`：一键快速拉起图形控制面板。
- 自动化测试（14/14）全量通过。

---

## [2026-09-10 12:14:00] - 第二阶段 (Phase 2) 状态采集与双通道出站推送全量交付
- 新增 `ConfigManager.cs` 与配置模型；新增 `SystemInfoCollector.cs` 采集 CPU/内存/磁盘/网络。
- 接入 `NtfyProvider.cs` 与 `WeChatProvider.cs`；实现双通道并发与有限重试。
- 实机测试向 `ntfy.sh` 推送成功。

---

## [2026-09-10 11:50:00] - 核心事件机制重构：废除死板 Uptime 规则，确保解锁与登录 100% 触发
- 重构会话与登录检测，以“进入桌面”为唯一基准。

---

## [2026-09-10 11:40:00] - 第一阶段 (Phase 1) 核心工程与事件检测全量实现与验证
- 建立文档体系、分层工程架构、无窗口静默后台运行与任务计划管理。
