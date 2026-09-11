using System.Text.Json;
using PCMonitor.Core.Common;

namespace PCMonitor.Core.Configuration;

/// <summary>
/// 配置文件管理器（支持自动生成默认模板、随机 Topic 与持久化）
/// </summary>
public class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configFilePath;
    private readonly SimpleLogger _logger;
    private readonly Interfaces.ISecurityProvider? _securityProvider;
    private readonly object _lock = new();

    public string ConfigFilePath => _configFilePath;

    public ConfigManager(SimpleLogger logger, string? customConfigPath = null, Interfaces.ISecurityProvider? securityProvider = null)
    {
        _logger = logger;
        _securityProvider = securityProvider;
        if (!string.IsNullOrWhiteSpace(customConfigPath))
        {
            _configFilePath = customConfigPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _configFilePath = Path.Combine(appData, "PCNotify", "config.json");
        }
    }

    /// <summary>
    /// 加载配置，若不存在则创建并保存默认配置
    /// </summary>
    public PCNotifyConfig LoadOrCreate()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<PCNotifyConfig>(json, JsonOptions);
                    if (config != null)
                    {
                        // 透明解密敏感字段
                        DecryptSensitiveFields(config);

                        // 确保 Topic 存在
                        if (string.IsNullOrWhiteSpace(config.Ntfy.Topic))
                        {
                            config.Ntfy.Topic = GenerateRandomTopic();
                            Save(config);
                        }
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[ConfigManager] 读取配置文件异常，将重新生成默认配置: {_configFilePath}", ex);
            }

            var defaultConfig = new PCNotifyConfig();
            defaultConfig.Ntfy.Topic = GenerateRandomTopic();
            Save(defaultConfig);
            return defaultConfig;
        }
    }

    /// <summary>
    /// 持久化保存配置（透明加密敏感字段后再写入磁盘，保护内存活跃对象不受影响）
    /// </summary>
    public void Save(PCNotifyConfig config)
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var diskConfig = EncryptSensitiveFields(config);
                var json = JsonSerializer.Serialize(diskConfig, JsonOptions);
                File.WriteAllText(_configFilePath, json);
                _logger.Info($"[ConfigManager] 配置已成功安全保存至 {_configFilePath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[ConfigManager] 保存配置文件失败: {_configFilePath}", ex);
            }
        }
    }

    private PCNotifyConfig EncryptSensitiveFields(PCNotifyConfig source)
    {
        if (_securityProvider == null) return source;

        try
        {
            var json = JsonSerializer.Serialize(source, JsonOptions);
            var clone = JsonSerializer.Deserialize<PCNotifyConfig>(json, JsonOptions) ?? new();

            if (!string.IsNullOrWhiteSpace(clone.WeCom.WebhookUrl))
                clone.WeCom.WebhookUrl = _securityProvider.Protect(clone.WeCom.WebhookUrl);

            if (!string.IsNullOrWhiteSpace(clone.Feishu.WebhookUrl))
                clone.Feishu.WebhookUrl = _securityProvider.Protect(clone.Feishu.WebhookUrl);

            if (!string.IsNullOrWhiteSpace(clone.Feishu.Secret))
                clone.Feishu.Secret = _securityProvider.Protect(clone.Feishu.Secret);

            if (!string.IsNullOrWhiteSpace(clone.DingTalk.WebhookUrl))
                clone.DingTalk.WebhookUrl = _securityProvider.Protect(clone.DingTalk.WebhookUrl);

            if (!string.IsNullOrWhiteSpace(clone.DingTalk.Secret))
                clone.DingTalk.Secret = _securityProvider.Protect(clone.DingTalk.Secret);

            if (!string.IsNullOrWhiteSpace(clone.WxPusher.AppToken))
                clone.WxPusher.AppToken = _securityProvider.Protect(clone.WxPusher.AppToken);

            if (!string.IsNullOrWhiteSpace(clone.CustomWebhook.Url))
                clone.CustomWebhook.Url = _securityProvider.Protect(clone.CustomWebhook.Url);

            if (!string.IsNullOrWhiteSpace(clone.WeChat.Token))
                clone.WeChat.Token = _securityProvider.Protect(clone.WeChat.Token);

            return clone;
        }
        catch (Exception ex)
        {
            _logger.Warn($"[ConfigManager] 敏感数据安全加密过程异常: {ex.Message}，将直接使用原值写入。");
            return source;
        }
    }

    private void DecryptSensitiveFields(PCNotifyConfig config)
    {
        if (_securityProvider == null) return;

        try
        {
            config.WeCom.WebhookUrl = _securityProvider.Unprotect(config.WeCom.WebhookUrl);
            config.Feishu.WebhookUrl = _securityProvider.Unprotect(config.Feishu.WebhookUrl);
            config.Feishu.Secret = _securityProvider.Unprotect(config.Feishu.Secret);
            config.DingTalk.WebhookUrl = _securityProvider.Unprotect(config.DingTalk.WebhookUrl);
            config.DingTalk.Secret = _securityProvider.Unprotect(config.DingTalk.Secret);
            config.WxPusher.AppToken = _securityProvider.Unprotect(config.WxPusher.AppToken);
            config.CustomWebhook.Url = _securityProvider.Unprotect(config.CustomWebhook.Url);
            config.WeChat.Token = _securityProvider.Unprotect(config.WeChat.Token);
        }
        catch (Exception ex)
        {
            _logger.Warn($"[ConfigManager] 敏感数据解密异常: {ex.Message}");
        }
    }

    public static string GenerateRandomTopic()
    {
        return $"pcnotify_{Guid.NewGuid():N}"[..20];
    }
}
