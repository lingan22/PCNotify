namespace PCMonitor.Core.Configuration;

/// <summary>
/// 全局配置模型根对象（支持多渠道推送聚合）
/// </summary>
public class PCNotifyConfig
{
    public NtfyConfig Ntfy { get; set; } = new();
    public WeComConfig WeCom { get; set; } = new();
    public FeishuConfig Feishu { get; set; } = new();
    public DingTalkConfig DingTalk { get; set; } = new();
    public WxPusherConfig WxPusher { get; set; } = new();
    public CustomWebhookConfig CustomWebhook { get; set; } = new();
    public WeChatConfig WeChat { get; set; } = new(); // 兼容 PushPlus
    public SystemInfoConfig SystemInfo { get; set; } = new();
    public RetryConfig Retry { get; set; } = new();
    public EventTriggerConfig Triggers { get; set; } = new();
    public string UiStyle { get; set; } = "Modern"; // "Modern" or "Classic"
}

public class EventTriggerConfig
{
    public bool MonitorUnlock { get; set; } = true;
    public bool MonitorBoot { get; set; } = true;
    public int DeduplicateSeconds { get; set; } = 30;
}

public class NtfyConfig
{
    public bool Enabled { get; set; } = true;
    public string ServerUrl { get; set; } = "https://ntfy.sh";
    public string Topic { get; set; } = string.Empty;
    public string Priority { get; set; } = "high"; // 默认提升为 high，触发安卓系统顶部大横幅
    public string Tags { get; set; } = "desktop_computer";
}

public class WeComConfig
{
    public bool Enabled { get; set; } = false;
    public string WebhookUrl { get; set; } = string.Empty;
}

public class FeishuConfig
{
    public bool Enabled { get; set; } = false;
    public string WebhookUrl { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}

public class DingTalkConfig
{
    public bool Enabled { get; set; } = false;
    public string WebhookUrl { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}

public class WxPusherConfig
{
    public bool Enabled { get; set; } = false;
    public string AppToken { get; set; } = string.Empty;
    public string Uids { get; set; } = string.Empty; // 多个以逗号分隔
}

public class CustomWebhookConfig
{
    public bool Enabled { get; set; } = false;
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
}

public class WeChatConfig
{
    public bool Enabled { get; set; } = false;
    public string Token { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = "https://www.pushplus.plus/send";
    public string Channel { get; set; } = "wechat";
    public string Template { get; set; } = "markdown";
}

public class SystemInfoConfig
{
    public bool Enabled { get; set; } = true;
    public bool IncludeCpu { get; set; } = true;
    public bool IncludeMemory { get; set; } = true;
    public bool IncludeDisks { get; set; } = false;
    public bool IncludeNetwork { get; set; } = true;
}

public class RetryConfig
{
    public int MaxRetries { get; set; } = 3;
    public int InitialDelaySeconds { get; set; } = 2;
    public double BackoffMultiplier { get; set; } = 2.0;
}
