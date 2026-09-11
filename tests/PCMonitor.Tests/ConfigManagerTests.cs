using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using Xunit;

namespace PCMonitor.Tests;

public class ConfigManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly SimpleLogger _logger;

    public ConfigManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pcnotify_cfgtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
        _logger = new SimpleLogger(_tempDir, consoleOutput: false);
    }

    [Fact]
    public void LoadOrCreate_GeneratesDefaultConfig_WithValidTopic()
    {
        var manager = new ConfigManager(_logger, _configPath);
        var config = manager.LoadOrCreate();

        Assert.NotNull(config);
        Assert.True(config.Ntfy.Enabled);
        Assert.StartsWith("pcnotify_", config.Ntfy.Topic);
        Assert.True(File.Exists(_configPath));
    }

    [Fact]
    public void SaveAndReload_PreservesCustomSettings()
    {
        var manager = new ConfigManager(_logger, _configPath);
        var config = manager.LoadOrCreate();

        config.Ntfy.Topic = "custom_topic_12345";
        config.WeChat.Enabled = true;
        config.WeChat.Token = "pushplus_test_token";
        manager.Save(config);

        var reloaded = manager.LoadOrCreate();
        Assert.Equal("custom_topic_12345", reloaded.Ntfy.Topic);
        Assert.True(reloaded.WeChat.Enabled);
        Assert.Equal("pushplus_test_token", reloaded.WeChat.Token);
    }

    [Fact]
    public void SaveAndLoad_WithDpapiSecurityProvider_TransparentlyProtectsSensitiveFieldsOnDisk()
    {
        var security = new PCMonitor.Windows.Security.DpapiSecurityProvider(_logger);
        var manager = new ConfigManager(_logger, _configPath, security);

        var config = manager.LoadOrCreate();
        config.WeCom.Enabled = true;
        config.WeCom.WebhookUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=secret_key_123";
        config.Feishu.Secret = "my_feishu_secret";

        manager.Save(config);

        // 1. 验证活跃内存对象未被加密串污染（仍然是明文）
        Assert.Equal("https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=secret_key_123", config.WeCom.WebhookUrl);

        // 2. 验证磁盘上的物理 JSON 文件已包含 enc: 前缀密文
        var rawJson = File.ReadAllText(_configPath);
        Assert.Contains("enc:", rawJson);
        Assert.DoesNotContain("secret_key_123", rawJson);
        Assert.DoesNotContain("my_feishu_secret", rawJson);

        // 3. 验证重新读取时透明解密为明文
        var reloaded = manager.LoadOrCreate();
        Assert.Equal("https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=secret_key_123", reloaded.WeCom.WebhookUrl);
        Assert.Equal("my_feishu_secret", reloaded.Feishu.Secret);
    }

    [Fact]
    public void LoadOrCreate_WithLegacyPlaintextConfig_SeamlesslyReadsPlaintext()
    {
        var security = new PCMonitor.Windows.Security.DpapiSecurityProvider(_logger);
        var legacyJson = @"{
            ""Ntfy"": { ""Enabled"": true, ""Topic"": ""legacy_topic"" },
            ""WeCom"": { ""Enabled"": true, ""WebhookUrl"": ""https://qyapi.weixin.qq.com/legacy_plain"" }
        }";
        File.WriteAllText(_configPath, legacyJson);

        var manager = new ConfigManager(_logger, _configPath, security);
        var config = manager.LoadOrCreate();

        Assert.Equal("legacy_topic", config.Ntfy.Topic);
        Assert.Equal("https://qyapi.weixin.qq.com/legacy_plain", config.WeCom.WebhookUrl);
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
