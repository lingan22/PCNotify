using System.Net;
using PCMonitor.Core.Common;
using PCMonitor.Core.Configuration;
using PCMonitor.Core.Models;
using PCMonitor.Notifications;
using PCMonitor.Notifications.Providers;
using Xunit;

namespace PCMonitor.Tests;

public class AggregatedProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SimpleLogger _logger;

    public AggregatedProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pcnotify_aggtests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _logger = new SimpleLogger(_tempDir, consoleOutput: false);
    }

    [Fact]
    public async Task WeComWebhookProvider_SendsMarkdownPayload_Success()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var mockHandler = new MockHttpMessageHandler(async req =>
        {
            capturedRequest = req;
            if (req.Content != null)
            {
                capturedBody = await req.Content.ReadAsStringAsync();
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"errcode\":0,\"errmsg\":\"ok\"}")
            };
        });

        var client = new HttpClient(mockHandler);
        var config = new WeComConfig
        {
            Enabled = true,
            WebhookUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=test-wecom-key"
        };

        var provider = new WeComWebhookProvider(config, _logger, client);
        var evt = new NotificationEvent
        {
            Type = EventType.PC_UNLOCK,
            Title = "电脑屏幕已解锁",
            Body = "CPU: 15% | 内存: 8GB",
            DeviceName = "TEST-PC"
        };

        var result = await provider.SendAsync(evt);

        Assert.True(result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=test-wecom-key", capturedRequest.RequestUri?.ToString());
        Assert.Contains("markdown", capturedBody);
        Assert.Contains("电脑屏幕已解锁", capturedBody);
    }

    [Fact]
    public async Task WeComWebhookProvider_ReturnsFalse_OnErrorResponse()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"errcode\":93000,\"errmsg\":\"invalid webhook\"}")
            });

        var client = new HttpClient(mockHandler);
        var config = new WeComConfig
        {
            Enabled = true,
            WebhookUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=invalid"
        };

        var provider = new WeComWebhookProvider(config, _logger, client);
        var evt = new NotificationEvent { Type = EventType.PC_UNLOCK, Title = "测试", Body = "内容" };

        var result = await provider.SendAsync(evt);
        Assert.False(result);
    }

    [Fact]
    public async Task FeishuWebhookProvider_SendsCardPayload_Success()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var mockHandler = new MockHttpMessageHandler(async req =>
        {
            capturedRequest = req;
            if (req.Content != null)
            {
                capturedBody = await req.Content.ReadAsStringAsync();
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":0,\"msg\":\"success\"}")
            };
        });

        var client = new HttpClient(mockHandler);
        var config = new FeishuConfig
        {
            Enabled = true,
            WebhookUrl = "https://open.feishu.cn/open-apis/bot/v2/hook/test-feishu-key"
        };

        var provider = new FeishuWebhookProvider(config, _logger, client);
        var evt = new NotificationEvent
        {
            Type = EventType.PC_BOOT,
            Title = "电脑开机并登录",
            Body = "状态正常",
            DeviceName = "FEISHU-PC"
        };

        var result = await provider.SendAsync(evt);

        Assert.True(result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://open.feishu.cn/open-apis/bot/v2/hook/test-feishu-key", capturedRequest.RequestUri?.ToString());
        Assert.Contains("interactive", capturedBody);
        Assert.Contains("电脑开机并登录", capturedBody);
    }

    [Fact]
    public async Task DingTalkWebhookProvider_SendsMarkdownPayload_Success()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var mockHandler = new MockHttpMessageHandler(async req =>
        {
            capturedRequest = req;
            if (req.Content != null)
            {
                capturedBody = await req.Content.ReadAsStringAsync();
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"errcode\":0,\"errmsg\":\"ok\"}")
            };
        });

        var client = new HttpClient(mockHandler);
        var config = new DingTalkConfig
        {
            Enabled = true,
            WebhookUrl = "https://oapi.dingtalk.com/robot/send?access_token=test-ding-token"
        };

        var provider = new DingTalkWebhookProvider(config, _logger, client);
        var evt = new NotificationEvent
        {
            Type = EventType.PC_UNLOCK,
            Title = "钉钉测试",
            Body = "详细数据"
        };

        var result = await provider.SendAsync(evt);

        Assert.True(result);
        Assert.NotNull(capturedRequest);
        Assert.Contains("markdown", capturedBody);
        Assert.Contains("钉钉测试", capturedBody);
    }

    [Fact]
    public async Task WxPusherProvider_SendsPayload_Success()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var mockHandler = new MockHttpMessageHandler(async req =>
        {
            capturedRequest = req;
            if (req.Content != null)
            {
                capturedBody = await req.Content.ReadAsStringAsync();
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":1000,\"msg\":\"处理成功\",\"data\":[]}")
            };
        });

        var client = new HttpClient(mockHandler);
        var config = new WxPusherConfig
        {
            Enabled = true,
            AppToken = "AT_test_token_xyz",
            Uids = "UID_111, UID_222"
        };

        var provider = new WxPusherProvider(config, _logger, client);
        var evt = new NotificationEvent
        {
            Type = EventType.PC_UNLOCK,
            Title = "WxPusher 标题",
            Body = "微信推送正文"
        };

        var result = await provider.SendAsync(evt);

        Assert.True(result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://wxpusher.zjiecode.com/api/send/message", capturedRequest.RequestUri?.ToString());
        Assert.Contains("AT_test_token_xyz", capturedBody);
        Assert.Contains("UID_111", capturedBody);
        Assert.Contains("UID_222", capturedBody);
    }

    [Fact]
    public async Task CustomWebhookProvider_SendsJsonPayload_Success()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var mockHandler = new MockHttpMessageHandler(async req =>
        {
            capturedRequest = req;
            if (req.Content != null)
            {
                capturedBody = await req.Content.ReadAsStringAsync();
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"accepted\"}")
            };
        });

        var client = new HttpClient(mockHandler);
        var config = new CustomWebhookConfig
        {
            Enabled = true,
            Url = "https://api.example.com/notifications/webhook"
        };

        var provider = new CustomWebhookProvider(config, _logger, client);
        var evt = new NotificationEvent
        {
            Type = EventType.PC_UNLOCK,
            Title = "自定义 Webhook 标题",
            Body = "事件内容",
            DeviceName = "CUSTOM-PC"
        };

        var result = await provider.SendAsync(evt);

        Assert.True(result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://api.example.com/notifications/webhook", capturedRequest.RequestUri?.ToString());
        Assert.Contains("CUSTOM-PC", capturedBody);
        Assert.Contains("自定义 Webhook 标题", capturedBody);
    }

    [Fact]
    public async Task NotificationManager_ClearProviders_And_MultiChannelDispatch()
    {
        var weComDispatched = false;
        var feishuDispatched = false;

        var mockHandler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains("wecom") == true) weComDispatched = true;
            if (req.RequestUri?.ToString().Contains("feishu") == true) feishuDispatched = true;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"errcode\":0,\"code\":0}")
            };
        });

        var client = new HttpClient(mockHandler);
        var manager = new NotificationManager(_logger);

        // 注册初始 Provider
        manager.RegisterProvider(new WeComWebhookProvider(new WeComConfig { Enabled = true, WebhookUrl = "https://example.com/wecom" }, _logger, client));
        Assert.Single(manager.Providers);

        // 清空
        manager.ClearProviders();
        Assert.Empty(manager.Providers);

        // 重新注册两个多渠道 Provider
        manager.RegisterProvider(new WeComWebhookProvider(new WeComConfig { Enabled = true, WebhookUrl = "https://example.com/wecom" }, _logger, client));
        manager.RegisterProvider(new FeishuWebhookProvider(new FeishuConfig { Enabled = true, WebhookUrl = "https://example.com/feishu" }, _logger, client));

        var evt = new NotificationEvent
        {
            Type = EventType.PC_UNLOCK,
            Title = "聚合并发测试",
            Body = "内容"
        };

        var results = await manager.DispatchAsync(evt);

        Assert.Equal(2, results.Count);
        Assert.True(weComDispatched);
        Assert.True(feishuDispatched);
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
